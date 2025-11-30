using System.Text.Json;
using System.Text.Json.Serialization;
using Polly;
using Polly.Retry;
using WhatsAppArchiver.Application.Services;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Specifications;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Infrastructure.State;

/// <summary>
/// Repository for persisting processing state to a JSON file.
/// </summary>
/// <remarks>
/// This implementation uses System.Text.Json for serialization and implements
/// atomic writes (write to temp file, then rename) for data integrity.
/// File locking is used to prevent concurrent access issues.
/// </remarks>
/// <example>
/// <code>
/// var repository = new JsonFileStateRepository("state.json");
/// var checkpoint = await repository.GetCheckpointAsync("doc-123");
/// checkpoint.MarkAsProcessed(messageId);
/// await repository.SaveCheckpointAsync(checkpoint);
/// </code>
/// </example>
public sealed class JsonFileStateRepository : IProcessingStateService
{
    private readonly string _filePath;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFileStateRepository"/> class.
    /// </summary>
    /// <param name="filePath">The path to the state JSON file.</param>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    public JsonFileStateRepository(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        }

        _filePath = filePath;

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new DateTimeOffsetConverter() }
        };

        _resiliencePipeline = CreateDefaultResiliencePipeline();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFileStateRepository"/> class with a custom resilience pipeline.
    /// </summary>
    /// <param name="filePath">The path to the state JSON file.</param>
    /// <param name="resiliencePipeline">The resilience pipeline to use for retries.</param>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when resiliencePipeline is null.</exception>
    public JsonFileStateRepository(string filePath, ResiliencePipeline resiliencePipeline)
        : this(filePath)
    {
        ArgumentNullException.ThrowIfNull(resiliencePipeline);

        _resiliencePipeline = resiliencePipeline;
    }

    /// <inheritdoc/>
    public async Task<ProcessingCheckpoint> GetCheckpointAsync(
        string documentId,
        SenderFilter? senderFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(documentId));
        }

        await _fileLock.WaitAsync(cancellationToken);

        try
        {
            var state = await LoadStateAsync(cancellationToken);
            var checkpointKey = CreateCheckpointKey(documentId, senderFilter);

            if (state.Checkpoints.TryGetValue(checkpointKey, out var dto))
            {
                return dto.ToEntity(senderFilter);
            }

            return ProcessingCheckpoint.Create(documentId, senderFilter);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException">Thrown when checkpoint is null.</exception>
    public async Task SaveCheckpointAsync(
        ProcessingCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        await _fileLock.WaitAsync(cancellationToken);

        try
        {
            var state = await LoadStateAsync(cancellationToken);
            var checkpointKey = CreateCheckpointKey(checkpoint.DocumentId, checkpoint.SenderFilter);

            state.Checkpoints[checkpointKey] = CheckpointDto.FromEntity(checkpoint);

            await SaveStateAtomicallyAsync(state, cancellationToken);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <summary>
    /// Creates a unique key for a checkpoint based on document ID and sender filter.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="senderFilter">The optional sender filter.</param>
    /// <returns>A unique key string.</returns>
    private static string CreateCheckpointKey(string documentId, SenderFilter? senderFilter)
    {
        return senderFilter is null
            ? documentId
            : $"{documentId}:{senderFilter.SenderName.ToLowerInvariant()}";
    }

    /// <summary>
    /// Loads the state from the JSON file.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The loaded state or a new state if the file doesn't exist.</returns>
    private async Task<StateContainer> LoadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new StateContainer();
        }

        return await _resiliencePipeline.ExecuteAsync(
            async ct =>
            {
                await using var stream = new FileStream(
                    _filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);

                var state = await JsonSerializer.DeserializeAsync<StateContainer>(stream, _jsonOptions, ct);

                return state ?? new StateContainer();
            },
            cancellationToken);
    }

    /// <summary>
    /// Saves the state atomically by writing to a temp file and then renaming.
    /// </summary>
    /// <param name="state">The state to save.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task SaveStateAtomicallyAsync(StateContainer state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempFilePath = $"{_filePath}.{Guid.NewGuid()}.tmp";

        try
        {
            await _resiliencePipeline.ExecuteAsync(
                async ct =>
                {
                    await using var stream = new FileStream(
                        tempFilePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None);

                    await JsonSerializer.SerializeAsync(stream, state, _jsonOptions, ct);
                    await stream.FlushAsync(ct);
                },
                cancellationToken);

            // Atomic rename
            File.Move(tempFilePath, _filePath, overwrite: true);
        }
        catch
        {
            // Clean up temp file on failure
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            throw;
        }
    }

    /// <summary>
    /// Creates the default resilience pipeline with retry policy.
    /// </summary>
    /// <returns>A configured resilience pipeline.</returns>
    private static ResiliencePipeline CreateDefaultResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<IOException>()
            })
            .Build();
    }

    /// <summary>
    /// Container for all state data.
    /// </summary>
    private sealed class StateContainer
    {
        public Dictionary<string, CheckpointDto> Checkpoints { get; set; } = [];
    }

    /// <summary>
    /// Data transfer object for serializing/deserializing checkpoints.
    /// </summary>
    private sealed class CheckpointDto
    {
        public Guid Id { get; set; }

        public string DocumentId { get; set; } = string.Empty;

        public DateTimeOffset? LastProcessedTimestamp { get; set; }

        public List<MessageIdDto> ProcessedMessageIds { get; set; } = [];

        public string? SenderFilterName { get; set; }

        public static CheckpointDto FromEntity(ProcessingCheckpoint checkpoint)
        {
            return new CheckpointDto
            {
                Id = checkpoint.Id,
                DocumentId = checkpoint.DocumentId,
                LastProcessedTimestamp = checkpoint.LastProcessedTimestamp,
                ProcessedMessageIds = checkpoint.ProcessedMessageIds
                    .Select(MessageIdDto.FromEntity)
                    .ToList(),
                SenderFilterName = checkpoint.SenderFilter?.SenderName
            };
        }

        public ProcessingCheckpoint ToEntity(SenderFilter? senderFilter)
        {
            var messageIds = ProcessedMessageIds.Select(dto => dto.ToEntity());

            return new ProcessingCheckpoint(
                Id,
                DocumentId,
                LastProcessedTimestamp,
                messageIds,
                senderFilter ?? (SenderFilterName is not null ? SenderFilter.Create(SenderFilterName) : null));
        }
    }

    /// <summary>
    /// Data transfer object for serializing/deserializing message IDs.
    /// </summary>
    private sealed class MessageIdDto
    {
        public DateTimeOffset Timestamp { get; set; }

        public string ContentHash { get; set; } = string.Empty;

        public static MessageIdDto FromEntity(MessageId messageId)
        {
            return new MessageIdDto
            {
                Timestamp = messageId.Timestamp,
                ContentHash = messageId.ContentHash
            };
        }

        public MessageId ToEntity()
        {
            return new MessageId(Timestamp, ContentHash);
        }
    }

    /// <summary>
    /// Custom JSON converter for DateTimeOffset to ensure consistent serialization.
    /// </summary>
    private sealed class DateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return DateTimeOffset.Parse(reader.GetString() ?? string.Empty);
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString("O"));
        }
    }
}
