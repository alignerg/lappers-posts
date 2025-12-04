using System.Runtime.CompilerServices;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Services;
using Polly;
using Polly.Retry;
using WhatsAppArchiver.Application.Services;

[assembly: InternalsVisibleTo("WhatsAppArchiver.Infrastructure.Tests")]

namespace WhatsAppArchiver.Infrastructure;

/// <summary>
/// Implements Google Docs operations using service account authentication.
/// </summary>
/// <remarks>
/// This adapter wraps the Google Docs API and provides resilient operations
/// with exponential backoff retry for transient failures.
/// </remarks>
/// <example>
/// <code>
/// using var adapter = new GoogleDocsServiceAccountAdapter(
///     credentialFilePath: "/path/to/credentials.json",
///     clientFactory: new GoogleDocsClientFactory());
/// await adapter.UploadAsync("doc-123", "Hello World");
/// </code>
/// </example>
public sealed class GoogleDocsServiceAccountAdapter : IGoogleDocsService, IDisposable
{
    private readonly IGoogleDocsClientWrapper _clientWrapper;
    private readonly ResiliencePipeline _resiliencePipeline;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleDocsServiceAccountAdapter"/> class.
    /// </summary>
    /// <param name="credentialFilePath">The path to the service account JSON key file.</param>
    /// <param name="clientFactory">The factory for creating Google Docs API clients.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="credentialFilePath"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clientFactory"/> is null.</exception>
    public GoogleDocsServiceAccountAdapter(
        string credentialFilePath,
        IGoogleDocsClientFactory clientFactory)
    {
        if (string.IsNullOrWhiteSpace(credentialFilePath))
        {
            throw new ArgumentException(
                "Credential file path cannot be null or empty.",
                nameof(credentialFilePath));
        }

        ArgumentNullException.ThrowIfNull(clientFactory);

        _clientWrapper = clientFactory.Create(credentialFilePath);
        _resiliencePipeline = CreateResiliencePipeline();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleDocsServiceAccountAdapter"/> class for testing.
    /// </summary>
    /// <param name="clientWrapper">The pre-configured client wrapper.</param>
    /// <param name="resiliencePipeline">The resilience pipeline for retry logic.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    internal GoogleDocsServiceAccountAdapter(
        IGoogleDocsClientWrapper clientWrapper,
        ResiliencePipeline resiliencePipeline)
    {
        ArgumentNullException.ThrowIfNull(clientWrapper);
        ArgumentNullException.ThrowIfNull(resiliencePipeline);

        _clientWrapper = clientWrapper;
        _resiliencePipeline = resiliencePipeline;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when <paramref name="documentId"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    public async Task UploadAsync(
        string documentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateDocumentId(documentId);
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        var requests = CreateBatchInsertRequests(content, clearExisting: true);

        await _resiliencePipeline.ExecuteAsync(
            async token =>
            {
                token.ThrowIfCancellationRequested();
                await _clientWrapper.BatchUpdateAsync(documentId, requests, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">Thrown when <paramref name="documentId"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="content"/> is null.</exception>
    public async Task AppendAsync(
        string documentId,
        string content,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateDocumentId(documentId);
        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        var requests = CreateBatchInsertRequests(content, clearExisting: false);

        await _resiliencePipeline.ExecuteAsync(
            async token =>
            {
                token.ThrowIfCancellationRequested();
                await _clientWrapper.BatchUpdateAsync(documentId, requests, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates batch insert requests for the content, split into paragraphs.
    /// </summary>
    /// <param name="content">The content to insert.</param>
    /// <param name="clearExisting">Whether to clear existing content before inserting.</param>
    /// <returns>A list of requests for batch update.</returns>
    private static List<Request> CreateBatchInsertRequests(string content, bool clearExisting)
    {
        // Split content into paragraphs for batch insertion
        var paragraphs = content.Split(["\r\n", "\n"], StringSplitOptions.None);
        
        // Pre-allocate list with initial capacity for performance
        var requests = new List<Request>(paragraphs.Length + (clearExisting ? 1 : 0));

        if (clearExisting)
        {
            // Add delete request to clear existing content
            // The range starts at index 1 (after document start) to preserve the document
            // Note: The API will automatically adjust EndIndex if it exceeds document length
            requests.Add(new Request
            {
                DeleteContentRange = new DeleteContentRangeRequest
                {
                    Range = new Google.Apis.Docs.v1.Data.Range
                    {
                        StartIndex = 1,
                        EndIndex = int.MaxValue
                    }
                }
            });
        }

        // Insert paragraphs in reverse order since we're inserting at index 1
        // This maintains the correct order in the final document
        for (var i = paragraphs.Length - 1; i >= 0; i--)
        {
            var paragraphText = paragraphs[i];

            // Add newline for all paragraphs except the last one
            // This correctly preserves the original content structure
            if (i < paragraphs.Length - 1)
            {
                paragraphText += "\n";
            }

            requests.Add(new Request
            {
                InsertText = new InsertTextRequest
                {
                    Text = paragraphText,
                    Location = new Location
                    {
                        Index = 1
                    }
                }
            });
        }

        return requests;
    }

    /// <summary>
    /// Creates the resilience pipeline with exponential backoff retry policy.
    /// </summary>
    /// <returns>A configured resilience pipeline.</returns>
    private static ResiliencePipeline CreateResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Google.GoogleApiException>(ex =>
                        IsTransientError(ex.HttpStatusCode))
                    .Handle<HttpRequestException>()
                    // Retry TaskCanceledException only if it's a timeout, not user cancellation
                    // Timeouts have CancellationToken.None, while user cancellation has the caller's token
                    .Handle<TaskCanceledException>(ex => 
                        ex.CancellationToken == CancellationToken.None)
            })
            .Build();
    }

    /// <summary>
    /// Determines if the HTTP status code indicates a transient error.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <returns>True if the error is transient and should be retried.</returns>
    private static bool IsTransientError(System.Net.HttpStatusCode statusCode) =>
        statusCode switch
        {
            System.Net.HttpStatusCode.RequestTimeout => true,
            System.Net.HttpStatusCode.TooManyRequests => true,
            System.Net.HttpStatusCode.InternalServerError => true,
            System.Net.HttpStatusCode.BadGateway => true,
            System.Net.HttpStatusCode.ServiceUnavailable => true,
            System.Net.HttpStatusCode.GatewayTimeout => true,
            _ => false
        };

    /// <summary>
    /// Validates the document ID parameter.
    /// </summary>
    /// <param name="documentId">The document ID to validate.</param>
    /// <exception cref="ArgumentException">Thrown when the document ID is null or empty.</exception>
    private static void ValidateDocumentId(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException(
                "Document ID cannot be null or empty.",
                nameof(documentId));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _clientWrapper.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Factory interface for creating Google Docs API clients.
/// </summary>
/// <remarks>
/// This abstraction enables testing by allowing mock implementations.
/// </remarks>
public interface IGoogleDocsClientFactory
{
    /// <summary>
    /// Creates a Google Docs client wrapper using the specified credentials.
    /// </summary>
    /// <param name="credentialFilePath">The path to the service account JSON key file.</param>
    /// <returns>A configured client wrapper.</returns>
    IGoogleDocsClientWrapper Create(string credentialFilePath);
}

/// <summary>
/// Wrapper interface for Google Docs API operations.
/// </summary>
/// <remarks>
/// This abstraction enables testing by allowing mock implementations.
/// </remarks>
public interface IGoogleDocsClientWrapper : IDisposable
{
    /// <summary>
    /// Executes a batch update on a Google Docs document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="requests">The list of update requests.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BatchUpdateAsync(
        string documentId,
        IList<Request> requests,
        CancellationToken cancellationToken);
}

/// <summary>
/// Default implementation of <see cref="IGoogleDocsClientFactory"/>.
/// </summary>
public sealed class GoogleDocsClientFactory : IGoogleDocsClientFactory
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="credentialFilePath"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="credentialFilePath"/> is empty or contains directory traversal patterns.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the credential file does not exist.</exception>
    public IGoogleDocsClientWrapper Create(string credentialFilePath)
    {
        ArgumentNullException.ThrowIfNull(credentialFilePath);

        if (string.IsNullOrWhiteSpace(credentialFilePath))
        {
            throw new ArgumentException(
                "Credential file path cannot be empty or whitespace.",
                nameof(credentialFilePath));
        }

        // Validate against directory traversal attacks in the original input
        // We check the original path because Path.GetFullPath resolves .. sequences
        if (credentialFilePath.Contains(".."))
        {
            throw new ArgumentException(
                "Credential file path cannot contain directory traversal patterns.",
                nameof(credentialFilePath));
        }

        // Normalize path for consistent file access
        var normalizedPath = Path.GetFullPath(credentialFilePath);

        // Verify file exists before attempting to open
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException(
                $"Credential file not found: {normalizedPath}",
                normalizedPath);
        }

        using var stream = new FileStream(
            normalizedPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);

        // Use ServiceAccountCredential for secure credential loading
        // The FromServiceAccountData method returns a credential that can be converted to GoogleCredential
        var serviceAccountCredential = ServiceAccountCredential
            .FromServiceAccountData(stream);

        var credential = GoogleCredential
            .FromServiceAccountCredential(serviceAccountCredential)
            .CreateScoped(DocsService.Scope.Documents);

        var service = new DocsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "WhatsAppArchiver"
        });

        return new GoogleDocsClientWrapper(service);
    }
}

/// <summary>
/// Default implementation of <see cref="IGoogleDocsClientWrapper"/>.
/// </summary>
internal sealed class GoogleDocsClientWrapper : IGoogleDocsClientWrapper
{
    private readonly DocsService _docsService;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleDocsClientWrapper"/> class.
    /// </summary>
    /// <param name="docsService">The Google Docs service.</param>
    public GoogleDocsClientWrapper(DocsService docsService)
    {
        _docsService = docsService ?? throw new ArgumentNullException(nameof(docsService));
    }

    /// <inheritdoc />
    public async Task BatchUpdateAsync(
        string documentId,
        IList<Request> requests,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var batchUpdateRequest = new BatchUpdateDocumentRequest
        {
            Requests = requests
        };

        var request = _docsService.Documents.BatchUpdate(batchUpdateRequest, documentId);
        await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _docsService.Dispose();
        _disposed = true;
    }
}
