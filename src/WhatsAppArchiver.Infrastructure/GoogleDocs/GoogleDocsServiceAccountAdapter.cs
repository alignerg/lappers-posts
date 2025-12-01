using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Services;
using Polly;
using Polly.Retry;
using WhatsAppArchiver.Application.Services;

namespace WhatsAppArchiver.Infrastructure.GoogleDocs;

/// <summary>
/// Adapter for Google Docs API using service account authentication.
/// </summary>
/// <remarks>
/// This adapter implements the <see cref="IGoogleDocsService"/> interface using
/// Google Docs API v1 with service account credentials. It provides retry policies
/// with exponential backoff for transient API failures.
/// Implements both <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/> for proper resource cleanup.
/// </remarks>
/// <example>
/// <code>
/// await using var adapter = new GoogleDocsServiceAccountAdapter("path/to/credentials.json");
/// await adapter.AppendAsync("document-id", "New content");
/// </code>
/// </example>
public sealed class GoogleDocsServiceAccountAdapter : IGoogleDocsService, IDisposable, IAsyncDisposable
{
    private readonly DocsService _docsService;
    private readonly ResiliencePipeline _resiliencePipeline;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleDocsServiceAccountAdapter"/> class.
    /// </summary>
    /// <param name="credentialsFilePath">The path to the service account credentials JSON file.</param>
    /// <exception cref="ArgumentException">Thrown when credentialsFilePath is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the credentials file does not exist.</exception>
    public GoogleDocsServiceAccountAdapter(string credentialsFilePath)
    {
        if (string.IsNullOrWhiteSpace(credentialsFilePath))
        {
            throw new ArgumentException("Credentials file path cannot be null or whitespace.", nameof(credentialsFilePath));
        }

        if (!File.Exists(credentialsFilePath))
        {
            throw new FileNotFoundException("The credentials file was not found.", credentialsFilePath);
        }

        using var stream = new FileStream(credentialsFilePath, FileMode.Open, FileAccess.Read);
        var serviceAccountCredential = ServiceAccountCredential.FromServiceAccountData(stream);
        var credential = GoogleCredential.FromServiceAccountCredential(serviceAccountCredential)
            .CreateScoped(DocsService.Scope.Documents);

        _docsService = new DocsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "WhatsApp Archiver"
        });

        _resiliencePipeline = CreateDefaultResiliencePipeline();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleDocsServiceAccountAdapter"/> class with custom dependencies.
    /// </summary>
    /// <param name="docsService">The Google Docs service instance.</param>
    /// <param name="resiliencePipeline">The resilience pipeline for retry policies.</param>
    /// <exception cref="ArgumentNullException">Thrown when docsService or resiliencePipeline is null.</exception>
    public GoogleDocsServiceAccountAdapter(DocsService docsService, ResiliencePipeline resiliencePipeline)
    {
        ArgumentNullException.ThrowIfNull(docsService);
        ArgumentNullException.ThrowIfNull(resiliencePipeline);

        _docsService = docsService;
        _resiliencePipeline = resiliencePipeline;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when documentId is null or empty.</exception>
    public async Task UploadAsync(string documentId, string content, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateDocumentId(documentId);

        var requests = new List<Request>
        {
            // First, delete all content
            new Request
            {
                DeleteContentRange = new DeleteContentRangeRequest
                {
                    Range = new Google.Apis.Docs.v1.Data.Range
                    {
                        StartIndex = 1,
                        EndIndex = int.MaxValue
                    }
                }
            }
        };

        // Then insert new content
        if (!string.IsNullOrEmpty(content))
        {
            requests.Add(CreateInsertTextRequest(content, 1));
        }

        await ExecuteBatchUpdateAsync(documentId, requests, cancellationToken);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when documentId is null or empty.</exception>
    public async Task AppendAsync(string documentId, string content, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateDocumentId(documentId);

        if (string.IsNullOrEmpty(content))
        {
            return;
        }

        // Get the document to find the end index
        var document = await _resiliencePipeline.ExecuteAsync(
            async ct =>
            {
                var request = _docsService.Documents.Get(documentId);

                return await request.ExecuteAsync(ct);
            },
            cancellationToken);

        var endIndex = document.Body?.Content?.LastOrDefault()?.EndIndex ?? 1;

        // Insert at end of document (before the last newline)
        var insertIndex = Math.Max(1, endIndex - 1);

        var requests = new List<Request>
        {
            CreateInsertTextRequest("\n" + content, insertIndex)
        };

        await ExecuteBatchUpdateAsync(documentId, requests, cancellationToken);
    }

    /// <summary>
    /// Disposes the resources used by this adapter.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _docsService.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Asynchronously disposes the resources used by this adapter.
    /// </summary>
    /// <returns>A task representing the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Creates a default resilience pipeline with exponential backoff retry policy.
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
                ShouldHandle = new PredicateBuilder()
                    .Handle<Google.GoogleApiException>(ex =>
                        ex.HttpStatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                                          or System.Net.HttpStatusCode.TooManyRequests
                                          or System.Net.HttpStatusCode.InternalServerError)
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
            })
            .Build();
    }

    /// <summary>
    /// Creates an insert text request for batch updates.
    /// </summary>
    /// <param name="text">The text to insert.</param>
    /// <param name="index">The index at which to insert the text.</param>
    /// <returns>A Request object for text insertion.</returns>
    private static Request CreateInsertTextRequest(string text, int index)
    {
        return new Request
        {
            InsertText = new InsertTextRequest
            {
                Text = text,
                Location = new Location
                {
                    Index = index
                }
            }
        };
    }

    /// <summary>
    /// Executes a batch update request against the Google Docs API.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="requests">The requests to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task ExecuteBatchUpdateAsync(
        string documentId,
        IList<Request> requests,
        CancellationToken cancellationToken)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var batchUpdateRequest = new BatchUpdateDocumentRequest
        {
            Requests = requests
        };

        await _resiliencePipeline.ExecuteAsync(
            async ct =>
            {
                var request = _docsService.Documents.BatchUpdate(batchUpdateRequest, documentId);

                await request.ExecuteAsync(ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Validates that the document ID is not null or empty.
    /// </summary>
    /// <param name="documentId">The document ID to validate.</param>
    /// <exception cref="ArgumentException">Thrown when documentId is null or empty.</exception>
    private static void ValidateDocumentId(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(documentId));
        }
    }
}
