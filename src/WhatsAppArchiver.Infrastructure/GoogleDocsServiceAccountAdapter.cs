using System.Runtime.CompilerServices;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Docs.v1;
using Google.Apis.Docs.v1.Data;
using Google.Apis.Services;
using Polly;
using Polly.Retry;
using WhatsAppArchiver.Application.Services;
using WhatsAppArchiver.Domain.Formatting;

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
    private const string HorizontalRuleText = "━━━━━━━━━━━━━━━━━━━━\n";
    
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
    /// Inserts a structured document with rich formatting at the beginning of a Google Docs document.
    /// </summary>
    /// <param name="documentId">The unique identifier of the Google Docs document.</param>
    /// <param name="document">The structured document to insert with rich formatting.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous insert operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="documentId"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    /// <remarks>
    /// This method inserts content at index 1, which is the beginning of the document content in the Google Docs API.
    /// To append to the end of a document, you would need to first retrieve the document length and use that as the start index.
    /// </remarks>
    public async Task InsertRichAsync(
        string documentId,
        GoogleDocsDocument document,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateDocumentId(documentId);
        ArgumentNullException.ThrowIfNull(document);

        cancellationToken.ThrowIfCancellationRequested();

        // Insert at index 1, which is the beginning of the document content in Google Docs API
        var requests = CreateRichContentRequests(document, startIndex: 1);

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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    public async Task AppendRichAsync(
        string documentId,
        GoogleDocsDocument document,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateDocumentId(documentId);
        ArgumentNullException.ThrowIfNull(document);

        cancellationToken.ThrowIfCancellationRequested();

        // Retrieve the document to determine the correct end index for appending
        var googleDoc = await _resiliencePipeline.ExecuteAsync(
            async token =>
            {
                token.ThrowIfCancellationRequested();
                return await _clientWrapper.GetDocumentAsync(documentId, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);

        if (googleDoc?.Body?.Content is null)
        {
            throw new InvalidOperationException($"Unable to retrieve document content for documentId '{documentId}'.");
        }

        // The end index of the document is the last element's endIndex
        var endIndex = googleDoc.Body.Content
            .Where(e => e.EndIndex.HasValue)
            .Select(e => e.EndIndex!.Value)
            .DefaultIfEmpty(1)
            .Max();

        var requests = CreateRichContentRequests(document, startIndex: endIndex - 1);

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

            // Skip empty paragraphs as Google Docs API requires non-empty text
            if (string.IsNullOrEmpty(paragraphText))
            {
                continue;
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
    /// Creates batch requests for rich formatted content from a GoogleDocsDocument.
    /// </summary>
    /// <param name="document">The structured document with formatting sections.</param>
    /// <param name="startIndex">The index at which to start inserting content.</param>
    /// <returns>A list of requests for batch update with text and formatting.</returns>
    private static List<Request> CreateRichContentRequests(GoogleDocsDocument document, int startIndex)
    {
        var requests = new List<Request>();
        var currentIndex = startIndex;

        foreach (var section in document.Sections)
        {
            switch (section)
            {
                case HeadingSection heading:
                    {
                        var textWithNewline = heading.Text + "\n";
                        var textLength = textWithNewline.Length;

                        // Insert text
                        requests.Add(new Request
                        {
                            InsertText = new InsertTextRequest
                            {
                                Text = textWithNewline,
                                Location = new Location { Index = currentIndex }
                            }
                        });

                        // Map heading levels 1-6 to Google Docs named style types
                        var namedStyleType = heading.Level switch
                        {
                            1 => "HEADING_1",
                            2 => "HEADING_2",
                            3 => "HEADING_3",
                            4 => "HEADING_4",
                            5 => "HEADING_5",
                            6 => "HEADING_6",
                            _ => throw new ArgumentOutOfRangeException(nameof(heading.Level), heading.Level, "Unsupported heading level. Only levels 1-6 are supported.")
                        };
                        
                        requests.Add(new Request
                        {
                            UpdateParagraphStyle = new UpdateParagraphStyleRequest
                            {
                                Range = new Google.Apis.Docs.v1.Data.Range
                                {
                                    StartIndex = currentIndex,
                                    EndIndex = currentIndex + textLength
                                },
                                ParagraphStyle = new ParagraphStyle
                                {
                                    NamedStyleType = namedStyleType
                                },
                                Fields = "namedStyleType"
                            }
                        });

                        currentIndex += textLength;
                        break;
                    }

                case BoldTextSection bold:
                    {
                        var textLength = bold.Text.Length;

                        // Insert text
                        requests.Add(new Request
                        {
                            InsertText = new InsertTextRequest
                            {
                                Text = bold.Text,
                                Location = new Location { Index = currentIndex }
                            }
                        });

                        // Apply bold style
                        requests.Add(new Request
                        {
                            UpdateTextStyle = new UpdateTextStyleRequest
                            {
                                Range = new Google.Apis.Docs.v1.Data.Range
                                {
                                    StartIndex = currentIndex,
                                    EndIndex = currentIndex + textLength
                                },
                                TextStyle = new TextStyle
                                {
                                    Bold = true
                                },
                                Fields = "bold"
                            }
                        });

                        currentIndex += textLength;
                        break;
                    }

                case ParagraphSection paragraph:
                    {
                        var textWithNewline = paragraph.Text + "\n";
                        var textLength = textWithNewline.Length;

                        // Insert text
                        requests.Add(new Request
                        {
                            InsertText = new InsertTextRequest
                            {
                                Text = textWithNewline,
                                Location = new Location { Index = currentIndex }
                            }
                        });

                        currentIndex += textLength;
                        break;
                    }

                case HorizontalRuleSection:
                    {
                        var textLength = HorizontalRuleText.Length;

                        // Insert horizontal rule
                        requests.Add(new Request
                        {
                            InsertText = new InsertTextRequest
                            {
                                Text = HorizontalRuleText,
                                Location = new Location { Index = currentIndex }
                            }
                        });

                        currentIndex += textLength;
                        break;
                    }

                case MetadataSection metadata:
                    {
                        var labelText = metadata.Label + ": ";
                        var valueText = metadata.Value + "\n";
                        var labelLength = labelText.Length;

                        // Insert label
                        requests.Add(new Request
                        {
                            InsertText = new InsertTextRequest
                            {
                                Text = labelText,
                                Location = new Location { Index = currentIndex }
                            }
                        });

                        // Apply bold to label
                        requests.Add(new Request
                        {
                            UpdateTextStyle = new UpdateTextStyleRequest
                            {
                                Range = new Google.Apis.Docs.v1.Data.Range
                                {
                                    StartIndex = currentIndex,
                                    EndIndex = currentIndex + labelLength
                                },
                                TextStyle = new TextStyle
                                {
                                    Bold = true
                                },
                                Fields = "bold"
                            }
                        });

                        currentIndex += labelLength;

                        // Insert value
                        requests.Add(new Request
                        {
                            InsertText = new InsertTextRequest
                            {
                                Text = valueText,
                                Location = new Location { Index = currentIndex }
                            }
                        });

                        currentIndex += valueText.Length;
                        break;
                    }

                default:
                    throw new NotSupportedException(
                        $"Unsupported DocumentSection type: {section.GetType().FullName}");
            }
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

    /// <summary>
    /// Retrieves a Google Docs document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The Google Docs document.</returns>
    Task<Document> GetDocumentAsync(
        string documentId,
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
        if (credentialFilePath.Contains("../") || credentialFilePath.Contains("..\\"))
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
    public async Task<Document> GetDocumentAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var request = _docsService.Documents.Get(documentId);
        return await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
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
