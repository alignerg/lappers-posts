using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Polly;
using Polly.Retry;

using WhatsAppArchiver.Application.Services;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Specifications;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Infrastructure.Repositories;

/// <summary>
/// File-based implementation of <see cref="IProcessingStateService"/> using JSON serialization.
/// </summary>
/// <remarks>
/// <para>
/// This repository persists processing checkpoints to the file system using JSON format.
/// It provides reliable state persistence with the following features:
/// </para>
/// <list type="bullet">
/// <item><description>Atomic writes using temporary file + rename pattern</description></item>
/// <item><description>File locking with exclusive access to prevent concurrent modifications</description></item>
/// <item><description>Retry policy using Polly for transient I/O failures</description></item>
/// </list>
/// <para>
/// The checkpoint files are named using the pattern: {documentId}__{senderName}.json
/// where both documentId and senderName are normalized to lowercase with invalid 
/// filename characters and spaces replaced by underscores.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var repository = new JsonFileStateRepository("/path/to/checkpoints");
/// var checkpoint = await repository.GetCheckpointAsync("doc-123", senderFilter);
/// checkpoint.MarkAsProcessed(messageId);
/// await repository.SaveCheckpointAsync(checkpoint);
/// </code>
/// </example>
public sealed class JsonFileStateRepository : IProcessingStateService
{
    private const int MaxRetryAttempts = 3;
    private const int MaxFileNameComponentLength = 100;
    private const int HashLength = 8;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly string _basePath;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<JsonFileStateRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFileStateRepository"/> class.
    /// </summary>
    /// <param name="basePath">The base directory path where checkpoint files will be stored.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentException">Thrown when basePath is null or whitespace.</exception>
    public JsonFileStateRepository(string basePath, ILogger<JsonFileStateRepository>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            throw new ArgumentException("Base path cannot be null or whitespace.", nameof(basePath));
        }

        _basePath = basePath;
        _resiliencePipeline = CreateResiliencePipeline();
        _jsonOptions = CreateJsonSerializerOptions();
        _logger = logger ?? NullLogger<JsonFileStateRepository>.Instance;
    }

    /// <summary>
    /// Retrieves the processing checkpoint for the specified document and sender.
    /// </summary>
    /// <param name="documentId">The unique identifier of the document being processed.</param>
    /// <param name="senderFilter">Optional sender filter to scope the checkpoint.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The existing checkpoint if found, or a new checkpoint if none exists for the given parameters.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when documentId is null or whitespace.</exception>
    /// <exception cref="JsonException">Thrown when the checkpoint file contains invalid JSON.</exception>
    public async Task<ProcessingCheckpoint> GetCheckpointAsync(
        string documentId,
        SenderFilter? senderFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(documentId));
        }

        var filePath = GetFilePath(documentId, senderFilter);

        if (!File.Exists(filePath))
        {
            return ProcessingCheckpoint.Create(documentId, senderFilter);
        }

        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            return await LoadCheckpointAsync(filePath, senderFilter, token);
        }, cancellationToken);
    }

    /// <summary>
    /// Persists the processing checkpoint state using atomic file write operations.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to persist.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <exception cref="ArgumentNullException">Thrown when checkpoint is null.</exception>
    public async Task SaveCheckpointAsync(
        ProcessingCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var filePath = GetFilePath(checkpoint.DocumentId, checkpoint.SenderFilter);
        var directoryPath = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrEmpty(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            await SaveCheckpointAtomicallyAsync(checkpoint, filePath, token);
        }, cancellationToken);
    }

    private async Task<ProcessingCheckpoint> LoadCheckpointAsync(
        string filePath,
        SenderFilter? senderFilter,
        CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var dto = await JsonSerializer.DeserializeAsync<CheckpointDto>(
            fileStream,
            _jsonOptions,
            cancellationToken) ?? throw new JsonException($"Failed to deserialize checkpoint from '{filePath}': result was null.");

        return dto.ToDomain(senderFilter);
    }

    private async Task SaveCheckpointAtomicallyAsync(
        ProcessingCheckpoint checkpoint,
        string filePath,
        CancellationToken cancellationToken)
    {
        var tempFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var fileStream = new FileStream(
                tempFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                var dto = CheckpointDto.FromDomain(checkpoint);
                await JsonSerializer.SerializeAsync(fileStream, dto, _jsonOptions, cancellationToken);
                await fileStream.FlushAsync(cancellationToken);
            }

            File.Move(tempFilePath, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch (Exception cleanupEx)
                {
                    // Intentionally ignored: cleanup is best-effort and should not mask the original exception.
                    // The temporary file may be cleaned up on next operation or by the OS.
                    _logger.LogDebug(cleanupEx, "Failed to cleanup temporary file '{TempFilePath}' during error recovery", tempFilePath);
                }
            }

            throw;
        }
    }

    private string GetFilePath(string documentId, SenderFilter? senderFilter)
    {
        var sanitizedDocumentId = SanitizeFileName(documentId);
        var fileName = senderFilter is not null
            ? $"{sanitizedDocumentId}__{SanitizeFileName(senderFilter.SenderName)}.json"
            : $"{sanitizedDocumentId}.json";

        return Path.Combine(_basePath, fileName);
    }

    /// <summary>
    /// Sanitizes a string for safe use as a file name by replacing invalid characters and spaces with underscores.
    /// Long names are truncated and appended with a hash to ensure uniqueness while staying within path limits.
    /// </summary>
    /// <param name="name">The input string to sanitize.</param>
    /// <returns>A sanitized, lowercase string safe for use as a file name.</returns>
    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var invalidCharSet = new HashSet<char>(invalidChars);
        var sb = new StringBuilder(name.Length);

        foreach (var c in name.ToLowerInvariant())
        {
            sb.Append(c == ' ' || invalidCharSet.Contains(c) ? '_' : c);
        }

        var sanitized = sb.ToString();

        if (sanitized.Length <= MaxFileNameComponentLength)
        {
            return sanitized;
        }

        var hash = ComputeShortHash(name);
        var truncatedLength = MaxFileNameComponentLength - HashLength - 1;

        return $"{sanitized[..truncatedLength]}_{hash}";
    }

    /// <summary>
    /// Computes a short hash of the input string for filename uniqueness.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A short hexadecimal hash string.</returns>
    private static string ComputeShortHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);

        return Convert.ToHexString(hashBytes)[..HashLength].ToLowerInvariant();
    }

    private static ResiliencePipeline CreateResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = MaxRetryAttempts,
                Delay = RetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<IOException>()
            })
            .Build();
    }

    private static JsonSerializerOptions CreateJsonSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    /// <summary>
    /// Data transfer object for serializing ProcessingCheckpoint to JSON.
    /// </summary>
    private sealed class CheckpointDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("documentId")]
        public string DocumentId { get; set; } = string.Empty;

        [JsonPropertyName("lastProcessedTimestamp")]
        public DateTimeOffset? LastProcessedTimestamp { get; set; }

        [JsonPropertyName("processedMessageIds")]
        public List<MessageIdDto> ProcessedMessageIds { get; set; } = [];

        [JsonPropertyName("senderName")]
        public string? SenderName { get; set; }

        public static CheckpointDto FromDomain(ProcessingCheckpoint checkpoint)
        {
            return new CheckpointDto
            {
                Id = checkpoint.Id,
                DocumentId = checkpoint.DocumentId,
                LastProcessedTimestamp = checkpoint.LastProcessedTimestamp,
                ProcessedMessageIds = checkpoint.ProcessedMessageIds
                    .Select(MessageIdDto.FromDomain)
                    .ToList(),
                SenderName = checkpoint.SenderFilter?.SenderName
            };
        }

        public ProcessingCheckpoint ToDomain(SenderFilter? providedSenderFilter)
        {
            var messageIds = ProcessedMessageIds
                .Select(dto => dto.ToDomain())
                .ToList();

            // Give precedence to persisted SenderName for consistency.
            var senderFilter = !string.IsNullOrWhiteSpace(SenderName)
                ? new SenderFilter(SenderName)
                : providedSenderFilter;

            return new ProcessingCheckpoint(
                Id,
                DocumentId,
                LastProcessedTimestamp,
                messageIds,
                senderFilter);
        }
    }

    /// <summary>
    /// Data transfer object for serializing MessageId to JSON.
    /// </summary>
    private sealed class MessageIdDto
    {
        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; }

        [JsonPropertyName("contentHash")]
        public string ContentHash { get; set; } = string.Empty;

        public static MessageIdDto FromDomain(MessageId messageId)
        {
            return new MessageIdDto
            {
                Timestamp = messageId.Timestamp,
                ContentHash = messageId.ContentHash
            };
        }

        public MessageId ToDomain()
        {
            return new MessageId(Timestamp, ContentHash);
        }
    }
}
