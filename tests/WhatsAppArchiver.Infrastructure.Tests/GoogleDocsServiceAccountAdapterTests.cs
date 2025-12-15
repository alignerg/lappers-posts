using FluentAssertions;
using Google.Apis.Docs.v1.Data;
using Moq;
using Polly;
using WhatsAppArchiver.Infrastructure;
using WhatsAppArchiver.Domain.Formatting;

namespace WhatsAppArchiver.Infrastructure.Tests;

public class GoogleDocsServiceAccountAdapterTests
{
    private readonly Mock<IGoogleDocsClientWrapper> _clientWrapperMock;
    private readonly ResiliencePipeline _noRetryPipeline;
    private readonly GoogleDocsServiceAccountAdapter _adapter;

    public GoogleDocsServiceAccountAdapterTests()
    {
        _clientWrapperMock = new Mock<IGoogleDocsClientWrapper>();
        _noRetryPipeline = ResiliencePipeline.Empty;
        _adapter = new GoogleDocsServiceAccountAdapter(
            _clientWrapperMock.Object,
            _noRetryPipeline);
    }

    [Fact(DisplayName = "Constructor with valid credentials initializes successfully")]
    public void Constructor_ValidCredentials_InitializesSuccessfully()
    {
        var clientWrapperMock = new Mock<IGoogleDocsClientWrapper>();
        var pipeline = ResiliencePipeline.Empty;

        var adapter = new GoogleDocsServiceAccountAdapter(
            clientWrapperMock.Object,
            pipeline);

        adapter.Should().NotBeNull();
    }

    [Fact(DisplayName = "Constructor with null client wrapper throws ArgumentNullException")]
    public void Constructor_NullClientWrapper_ThrowsArgumentNullException()
    {
        var pipeline = ResiliencePipeline.Empty;

        var act = () => new GoogleDocsServiceAccountAdapter(
            null!,
            pipeline);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("clientWrapper");
    }

    [Fact(DisplayName = "Constructor with null resilience pipeline throws ArgumentNullException")]
    public void Constructor_NullResiliencePipeline_ThrowsArgumentNullException()
    {
        var clientWrapperMock = new Mock<IGoogleDocsClientWrapper>();

        var act = () => new GoogleDocsServiceAccountAdapter(
            clientWrapperMock.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("resiliencePipeline");
    }

    [Fact(DisplayName = "Constructor with null credential file path throws ArgumentException")]
    public void Constructor_NullCredentialFilePath_ThrowsArgumentException()
    {
        var factoryMock = new Mock<IGoogleDocsClientFactory>();
        factoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(Mock.Of<IGoogleDocsClientWrapper>());

        var act = () => new GoogleDocsServiceAccountAdapter(
            null!,
            factoryMock.Object);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("credentialFilePath");
    }

    [Fact(DisplayName = "Constructor with empty credential file path throws ArgumentException")]
    public void Constructor_EmptyCredentialFilePath_ThrowsArgumentException()
    {
        var factoryMock = new Mock<IGoogleDocsClientFactory>();
        factoryMock.Setup(f => f.Create(It.IsAny<string>())).Returns(Mock.Of<IGoogleDocsClientWrapper>());

        var act = () => new GoogleDocsServiceAccountAdapter(
            string.Empty,
            factoryMock.Object);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("credentialFilePath");
    }

    [Fact(DisplayName = "Constructor with null client factory throws ArgumentNullException")]
    public void Constructor_NullClientFactory_ThrowsArgumentNullException()
    {
        var act = () => new GoogleDocsServiceAccountAdapter(
            "/path/to/credentials.json",
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("clientFactory");
    }

    [Fact(DisplayName = "UploadAsync with valid content calls API successfully")]
    public async Task UploadAsync_ValidContent_CallsApiSuccessfully()
    {
        var documentId = "test-doc-123";
        var content = "Hello World";

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _adapter.UploadAsync(documentId, content);

        _clientWrapperMock.Verify(
            x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "UploadAsync with null document ID throws ArgumentException")]
    public async Task UploadAsync_NullDocumentId_ThrowsArgumentException()
    {
        var act = () => _adapter.UploadAsync(null!, "content");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "UploadAsync with empty document ID throws ArgumentException")]
    public async Task UploadAsync_EmptyDocumentId_ThrowsArgumentException()
    {
        var act = () => _adapter.UploadAsync(string.Empty, "content");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "UploadAsync with whitespace document ID throws ArgumentException")]
    public async Task UploadAsync_WhitespaceDocumentId_ThrowsArgumentException()
    {
        var act = () => _adapter.UploadAsync("   ", "content");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "UploadAsync with null content throws ArgumentNullException")]
    public async Task UploadAsync_NullContent_ThrowsArgumentNullException()
    {
        var act = () => _adapter.UploadAsync("doc-123", null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("content");
    }

    [Fact(DisplayName = "UploadAsync with API error retries with exponential backoff")]
    public async Task UploadAsync_ApiError_RetriesWithExponentialBackoff()
    {
        var callCount = 0;
        var clientWrapperMock = new Mock<IGoogleDocsClientWrapper>();
        
        clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                It.IsAny<string>(),
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount < 3)
                {
                    throw new HttpRequestException("Transient error");
                }
                return Task.CompletedTask;
            });

        // MaxRetryAttempts = 3 allows up to 4 total calls (1 initial + 3 retries)
        // In this test, we succeed on the 3rd attempt (1 initial + 2 retries)
        var retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(10),
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
            })
            .Build();

        var adapter = new GoogleDocsServiceAccountAdapter(
            clientWrapperMock.Object,
            retryPipeline);

        await adapter.UploadAsync("doc-123", "content");

        // 3 calls: 1 initial + 2 retries, succeeded on the 3rd attempt
        callCount.Should().Be(3);
    }

    [Fact(DisplayName = "AppendAsync with valid content appends to document")]
    public async Task AppendAsync_ValidContent_AppendsToDocument()
    {
        var documentId = "test-doc-123";
        var content = "Appended content";
        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.AppendAsync(documentId, content);

        _clientWrapperMock.Verify(
            x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        capturedRequests.Should().NotBeNull();
        capturedRequests.Should().NotContain(r => r.DeleteContentRange != null);
    }

    [Fact(DisplayName = "AppendAsync with null document ID throws ArgumentException")]
    public async Task AppendAsync_NullDocumentId_ThrowsArgumentException()
    {
        var act = () => _adapter.AppendAsync(null!, "content");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "AppendAsync with empty document ID throws ArgumentException")]
    public async Task AppendAsync_EmptyDocumentId_ThrowsArgumentException()
    {
        var act = () => _adapter.AppendAsync(string.Empty, "content");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "AppendAsync with whitespace document ID throws ArgumentException")]
    public async Task AppendAsync_WhitespaceDocumentId_ThrowsArgumentException()
    {
        var act = () => _adapter.AppendAsync("   ", "content");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "AppendAsync with null content throws ArgumentNullException")]
    public async Task AppendAsync_NullContent_ThrowsArgumentNullException()
    {
        var act = () => _adapter.AppendAsync("doc-123", null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("content");
    }

    [Fact(DisplayName = "UploadAsync with cancellation requested throws OperationCanceledException")]
    public async Task UploadAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _adapter.UploadAsync("doc-123", "content", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "AppendAsync with cancellation requested throws OperationCanceledException")]
    public async Task AppendAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => _adapter.AppendAsync("doc-123", "content", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact(DisplayName = "UploadAsync with batch paragraphs inserts all content")]
    public async Task UploadAsync_BatchParagraphs_InsertsAllContent()
    {
        var documentId = "test-doc-123";
        var content = "Line 1\nLine 2\nLine 3";
        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.UploadAsync(documentId, content);

        capturedRequests.Should().NotBeNull();
        
        var insertTextRequests = capturedRequests!
            .Where(r => r.InsertText != null)
            .Select(r => r.InsertText!.Text)
            .ToList();

        insertTextRequests.Should().HaveCount(3);
        
        // Requests are inserted in reverse order (Line 3, Line 2, Line 1) for correct document assembly
        // When reversed and concatenated, they should produce the original content
        var combinedText = string.Concat(insertTextRequests.AsEnumerable().Reverse());
        combinedText.Should().Be("Line 1\nLine 2\nLine 3");
    }

    [Fact(DisplayName = "UploadAsync includes delete request to clear existing content")]
    public async Task UploadAsync_IncludesDeleteRequest_ToClearExistingContent()
    {
        var documentId = "test-doc-123";
        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.UploadAsync(documentId, "new content");

        capturedRequests.Should().NotBeNull();
        capturedRequests.Should().Contain(r => r.DeleteContentRange != null);
    }

    [Fact(DisplayName = "UploadAsync passes cancellation token to client wrapper")]
    public async Task UploadAsync_PassesCancellationToken_ToClientWrapper()
    {
        using var cts = new CancellationTokenSource();
        var documentId = "test-doc-123";

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                cts.Token))
            .Returns(Task.CompletedTask);

        await _adapter.UploadAsync(documentId, "content", cts.Token);

        _clientWrapperMock.Verify(
            x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                cts.Token),
            Times.Once);
    }

    [Fact(DisplayName = "AppendAsync passes cancellation token to client wrapper")]
    public async Task AppendAsync_PassesCancellationToken_ToClientWrapper()
    {
        using var cts = new CancellationTokenSource();
        var documentId = "test-doc-123";

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                cts.Token))
            .Returns(Task.CompletedTask);

        await _adapter.AppendAsync(documentId, "content", cts.Token);

        _clientWrapperMock.Verify(
            x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                cts.Token),
            Times.Once);
    }

    #region Content Edge Cases Tests

    [Fact(DisplayName = "UploadAsync with content ending with newline preserves trailing newline")]
    public async Task UploadAsync_ContentEndingWithNewline_PreservesTrailingNewline()
    {
        var documentId = "test-doc-123";
        var content = "Line 1\nLine 2\n";
        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.UploadAsync(documentId, content);

        capturedRequests.Should().NotBeNull();

        var insertTextRequests = capturedRequests!
            .Where(r => r.InsertText != null)
            .Select(r => r.InsertText!.Text)
            .ToList();

        // Requests are inserted in reverse order for correct document assembly
        // When reversed and concatenated, they should produce the original content exactly
        var combinedText = string.Concat(insertTextRequests.AsEnumerable().Reverse());
        combinedText.Should().Be(content);
    }

    [Fact(DisplayName = "UploadAsync with empty content calls API successfully")]
    public async Task UploadAsync_EmptyContent_CallsApiSuccessfully()
    {
        var documentId = "test-doc-123";
        var content = "";
        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.UploadAsync(documentId, content);

        _clientWrapperMock.Verify(
            x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        capturedRequests.Should().NotBeNull();
        // Should have delete request and one insert for empty string
        capturedRequests.Should().Contain(r => r.DeleteContentRange != null);
    }

    [Fact(DisplayName = "AppendAsync with empty content calls API successfully")]
    public async Task AppendAsync_EmptyContent_CallsApiSuccessfully()
    {
        var documentId = "test-doc-123";
        var content = "";

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _adapter.AppendAsync(documentId, content);

        _clientWrapperMock.Verify(
            x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "UploadAsync with whitespace-only content handles correctly")]
    public async Task UploadAsync_WhitespaceOnlyContent_HandlesCorrectly()
    {
        var documentId = "test-doc-123";
        var content = "   ";
        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.UploadAsync(documentId, content);

        capturedRequests.Should().NotBeNull();

        var insertTextRequests = capturedRequests!
            .Where(r => r.InsertText != null)
            .Select(r => r.InsertText!.Text)
            .ToList();

        var combinedText = string.Concat(insertTextRequests.AsEnumerable().Reverse());
        combinedText.Should().Be("   ");
    }

    [Fact(DisplayName = "UploadAsync with only newlines content handles correctly")]
    public async Task UploadAsync_OnlyNewlinesContent_HandlesCorrectly()
    {
        var documentId = "test-doc-123";
        var content = "\n\n\n";
        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.UploadAsync(documentId, content);

        capturedRequests.Should().NotBeNull();

        var insertTextRequests = capturedRequests!
            .Where(r => r.InsertText != null)
            .Select(r => r.InsertText!.Text)
            .ToList();

        // Requests are inserted in reverse order for correct document assembly
        // When reversed and concatenated, they should produce the original content exactly
        var combinedText = string.Concat(insertTextRequests.AsEnumerable().Reverse());
        combinedText.Should().Be(content);
    }

    [Fact(DisplayName = "UploadAsync with multiple consecutive newlines does not create empty insert requests")]
    public async Task UploadAsync_MultipleConsecutiveNewlines_DoesNotCreateEmptyInsertRequests()
    {
        var documentId = "test-doc-123";
        var content = "Line 1\n\n\nLine 2";
        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.UploadAsync(documentId, content);

        capturedRequests.Should().NotBeNull();
        
        var insertTextRequests = capturedRequests!
            .Where(r => r.InsertText != null)
            .Select(r => r.InsertText!.Text)
            .ToList();

        // Verify no empty or null text is inserted
        insertTextRequests.Should().NotContain(text => string.IsNullOrEmpty(text));
        
        // Requests are inserted in reverse order for correct document assembly
        // When reversed and concatenated, they should produce the original content exactly
        var combinedText = string.Concat(insertTextRequests.AsEnumerable().Reverse());
        combinedText.Should().Be(content);
    }

    #endregion

    #region IDisposable Tests

    [Fact(DisplayName = "Dispose disposes underlying client wrapper")]
    public void Dispose_DisposesUnderlyingClientWrapper()
    {
        var clientWrapperMock = new Mock<IGoogleDocsClientWrapper>();
        var pipeline = ResiliencePipeline.Empty;

        var adapter = new GoogleDocsServiceAccountAdapter(
            clientWrapperMock.Object,
            pipeline);

        adapter.Dispose();

        clientWrapperMock.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact(DisplayName = "UploadAsync after disposal throws ObjectDisposedException")]
    public async Task UploadAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        var clientWrapperMock = new Mock<IGoogleDocsClientWrapper>();
        var pipeline = ResiliencePipeline.Empty;

        var adapter = new GoogleDocsServiceAccountAdapter(
            clientWrapperMock.Object,
            pipeline);

        adapter.Dispose();

        var act = () => adapter.UploadAsync("doc-123", "content");

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact(DisplayName = "AppendAsync after disposal throws ObjectDisposedException")]
    public async Task AppendAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        var clientWrapperMock = new Mock<IGoogleDocsClientWrapper>();
        var pipeline = ResiliencePipeline.Empty;

        var adapter = new GoogleDocsServiceAccountAdapter(
            clientWrapperMock.Object,
            pipeline);

        adapter.Dispose();

        var act = () => adapter.AppendAsync("doc-123", "content");

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact(DisplayName = "Dispose called multiple times is safe (idempotent)")]
    public void Dispose_CalledMultipleTimes_IsSafe()
    {
        var clientWrapperMock = new Mock<IGoogleDocsClientWrapper>();
        var pipeline = ResiliencePipeline.Empty;

        var adapter = new GoogleDocsServiceAccountAdapter(
            clientWrapperMock.Object,
            pipeline);

        // Call Dispose multiple times - should not throw
        adapter.Dispose();
        adapter.Dispose();
        adapter.Dispose();

        // Dispose should only be called once on the underlying wrapper
        clientWrapperMock.Verify(x => x.Dispose(), Times.Once);
    }

    #endregion

    #region GoogleDocsClientFactory Tests

    [Fact(DisplayName = "Factory Create with null credential file path throws ArgumentNullException")]
    public void FactoryCreate_NullCredentialFilePath_ThrowsArgumentNullException()
    {
        var factory = new GoogleDocsClientFactory();

        var act = () => factory.Create(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("credentialFilePath");
    }

    [Fact(DisplayName = "Factory Create with empty credential file path throws ArgumentException")]
    public void FactoryCreate_EmptyCredentialFilePath_ThrowsArgumentException()
    {
        var factory = new GoogleDocsClientFactory();

        var act = () => factory.Create(string.Empty);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("credentialFilePath");
    }

    [Fact(DisplayName = "Factory Create with whitespace credential file path throws ArgumentException")]
    public void FactoryCreate_WhitespaceCredentialFilePath_ThrowsArgumentException()
    {
        var factory = new GoogleDocsClientFactory();

        var act = () => factory.Create("   ");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("credentialFilePath");
    }

    [Fact(DisplayName = "Factory Create with non-existent file throws FileNotFoundException")]
    public void FactoryCreate_NonExistentFile_ThrowsFileNotFoundException()
    {
        var factory = new GoogleDocsClientFactory();
        var nonExistentPath = "/path/to/non-existent/credentials.json";

        var act = () => factory.Create(nonExistentPath);

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact(DisplayName = "Factory Create with directory traversal throws ArgumentException")]
    public void FactoryCreate_DirectoryTraversal_ThrowsArgumentException()
    {
        var factory = new GoogleDocsClientFactory();
        var traversalPath = "/path/../to/../credentials.json";

        var act = () => factory.Create(traversalPath);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("credentialFilePath");
    }

    #endregion

    #region InsertRichAsync Tests

    [Fact(DisplayName = "InsertRichAsync with null document ID throws ArgumentException")]
    public async Task InsertRichAsync_NullDocumentId_ThrowsArgumentException()
    {
        var document = new GoogleDocsDocument();

        var act = () => _adapter.InsertRichAsync(null!, document);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "InsertRichAsync with empty document ID throws ArgumentException")]
    public async Task InsertRichAsync_EmptyDocumentId_ThrowsArgumentException()
    {
        var document = new GoogleDocsDocument();

        var act = () => _adapter.InsertRichAsync(string.Empty, document);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "InsertRichAsync with null document throws ArgumentNullException")]
    public async Task InsertRichAsync_NullDocument_ThrowsArgumentNullException()
    {
        var act = () => _adapter.InsertRichAsync("doc-123", null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("document");
    }

    [Fact(DisplayName = "InsertRichAsync after disposal throws ObjectDisposedException")]
    public async Task InsertRichAsync_AfterDisposal_ThrowsObjectDisposedException()
    {
        var clientWrapperMock = new Mock<IGoogleDocsClientWrapper>();
        var pipeline = ResiliencePipeline.Empty;

        var adapter = new GoogleDocsServiceAccountAdapter(
            clientWrapperMock.Object,
            pipeline);

        adapter.Dispose();

        var document = new GoogleDocsDocument();
        var act = () => adapter.InsertRichAsync("doc-123", document);

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact(DisplayName = "InsertRichAsync with heading sections creates heading style requests")]
    public async Task InsertRichAsync_WithHeadingSections_CreatesHeadingStyleRequests()
    {
        var documentId = "test-doc-123";
        var document = new GoogleDocsDocument();
        document.Add(new HeadingSection(1, "Main Heading"));
        document.Add(new HeadingSection(2, "Sub Heading"));

        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.InsertRichAsync(documentId, document);

        capturedRequests.Should().NotBeNull();
        
        var paragraphStyleRequests = capturedRequests!
            .Where(r => r.UpdateParagraphStyle != null)
            .Select(r => r.UpdateParagraphStyle)
            .ToList();

        paragraphStyleRequests.Should().HaveCount(2);
        paragraphStyleRequests[0]!.ParagraphStyle.NamedStyleType.Should().Be("HEADING_1");
        paragraphStyleRequests[1]!.ParagraphStyle.NamedStyleType.Should().Be("HEADING_2");
    }

    [Fact(DisplayName = "InsertRichAsync with all heading levels creates correct style requests")]
    public async Task InsertRichAsync_WithAllHeadingLevels_CreatesCorrectStyleRequests()
    {
        var documentId = "test-doc-123";
        var document = new GoogleDocsDocument();
        document.Add(new HeadingSection(1, "Heading 1"));
        document.Add(new HeadingSection(2, "Heading 2"));
        document.Add(new HeadingSection(3, "Heading 3"));
        document.Add(new HeadingSection(4, "Heading 4"));
        document.Add(new HeadingSection(5, "Heading 5"));
        document.Add(new HeadingSection(6, "Heading 6"));

        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.InsertRichAsync(documentId, document);

        capturedRequests.Should().NotBeNull();
        
        var paragraphStyleRequests = capturedRequests!
            .Where(r => r.UpdateParagraphStyle != null)
            .Select(r => r.UpdateParagraphStyle)
            .ToList();

        paragraphStyleRequests.Should().HaveCount(6);
        paragraphStyleRequests[0]!.ParagraphStyle.NamedStyleType.Should().Be("HEADING_1");
        paragraphStyleRequests[1]!.ParagraphStyle.NamedStyleType.Should().Be("HEADING_2");
        paragraphStyleRequests[2]!.ParagraphStyle.NamedStyleType.Should().Be("HEADING_3");
        paragraphStyleRequests[3]!.ParagraphStyle.NamedStyleType.Should().Be("HEADING_4");
        paragraphStyleRequests[4]!.ParagraphStyle.NamedStyleType.Should().Be("HEADING_5");
        paragraphStyleRequests[5]!.ParagraphStyle.NamedStyleType.Should().Be("HEADING_6");
    }

    [Fact(DisplayName = "InsertRichAsync with bold sections creates bold text style requests")]
    public async Task InsertRichAsync_WithBoldSections_CreatesBoldTextStyleRequests()
    {
        var documentId = "test-doc-123";
        var document = new GoogleDocsDocument();
        document.Add(new BoldTextSection("Important Text"));

        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.InsertRichAsync(documentId, document);

        capturedRequests.Should().NotBeNull();
        
        var textStyleRequests = capturedRequests!
            .Where(r => r.UpdateTextStyle != null)
            .Select(r => r.UpdateTextStyle)
            .ToList();

        textStyleRequests.Should().HaveCount(1);
        textStyleRequests[0]!.TextStyle.Bold.Should().BeTrue();
        textStyleRequests[0]!.Fields.Should().Be("bold");
    }

    [Fact(DisplayName = "InsertRichAsync with horizontal rule inserts unicode line")]
    public async Task InsertRichAsync_WithHorizontalRule_InsertsUnicodeLine()
    {
        var documentId = "test-doc-123";
        var document = new GoogleDocsDocument();
        document.Add(new HorizontalRuleSection());

        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.InsertRichAsync(documentId, document);

        capturedRequests.Should().NotBeNull();
        
        var insertTextRequests = capturedRequests!
            .Where(r => r.InsertText != null)
            .Select(r => r.InsertText!.Text)
            .ToList();

        insertTextRequests.Should().HaveCount(1);
        insertTextRequests[0].Should().StartWith("━");
        insertTextRequests[0].Should().EndWith("\n");
    }

    [Fact(DisplayName = "InsertRichAsync with metadata applies bold to labels")]
    public async Task InsertRichAsync_WithMetadata_AppliesBoldToLabels()
    {
        var documentId = "test-doc-123";
        var document = new GoogleDocsDocument();
        document.Add(new MetadataSection("Author", "John Doe"));
        document.Add(new MetadataSection("Date", "2024-01-01"));

        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.InsertRichAsync(documentId, document);

        capturedRequests.Should().NotBeNull();
        
        var textStyleRequests = capturedRequests!
            .Where(r => r.UpdateTextStyle != null)
            .Select(r => r.UpdateTextStyle)
            .ToList();

        textStyleRequests.Should().HaveCount(2);
        textStyleRequests.Should().AllSatisfy(req =>
        {
            req.Should().NotBeNull();
            req!.TextStyle.Bold.Should().BeTrue();
            req.Fields.Should().Be("bold");
        });
    }

    [Fact(DisplayName = "InsertRichAsync complex document calculates indices correctly")]
    public async Task InsertRichAsync_ComplexDocument_CalculatesIndicesCorrectly()
    {
        var documentId = "test-doc-123";
        var document = new GoogleDocsDocument();
        document.Add(new HeadingSection(1, "Title"));
        document.Add(new ParagraphSection("First paragraph"));
        document.Add(new BoldTextSection("Bold"));
        document.Add(new MetadataSection("Key", "Value"));

        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.InsertRichAsync(documentId, document);

        capturedRequests.Should().NotBeNull();

        var insertRequests = capturedRequests!
            .Where(r => r.InsertText != null)
            .Select(r => r.InsertText)
            .ToList();

        insertRequests.Should().HaveCount(5);
        
        var currentIndex = 1;
        insertRequests[0]!.Location.Index.Should().Be(currentIndex);
        currentIndex += insertRequests[0]!.Text.Length;
        
        insertRequests[1]!.Location.Index.Should().Be(currentIndex);
        currentIndex += insertRequests[1]!.Text.Length;
        
        insertRequests[2]!.Location.Index.Should().Be(currentIndex);
        currentIndex += insertRequests[2]!.Text.Length;
        
        insertRequests[3]!.Location.Index.Should().Be(currentIndex);
        currentIndex += insertRequests[3]!.Text.Length;
        
        insertRequests[4]!.Location.Index.Should().Be(currentIndex);
    }

    [Fact(DisplayName = "InsertRichAsync handles empty document with no requests")]
    public async Task InsertRichAsync_EmptyDocument_NoRequests()
    {
        var documentId = "test-doc-123";
        var document = new GoogleDocsDocument();

        IList<Request>? capturedRequests = null;

        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(
                documentId,
                It.IsAny<IList<Request>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, requests, _) =>
            {
                capturedRequests = requests;
            })
            .Returns(Task.CompletedTask);

        await _adapter.InsertRichAsync(documentId, document);

        capturedRequests.Should().NotBeNull();
        capturedRequests.Should().BeEmpty();
    }

    #endregion

    #region AppendRichAsync Tests

    [Fact(DisplayName = "AppendRichAsync with null document ID throws ArgumentException")]
    public async Task AppendRichAsync_NullDocumentId_ThrowsArgumentException()
    {
        var document = new GoogleDocsDocument();

        var act = () => _adapter.AppendRichAsync(null!, document);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "AppendRichAsync with empty document ID throws ArgumentException")]
    public async Task AppendRichAsync_EmptyDocumentId_ThrowsArgumentException()
    {
        var document = new GoogleDocsDocument();

        var act = () => _adapter.AppendRichAsync(string.Empty, document);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("documentId");
    }

    [Fact(DisplayName = "AppendRichAsync with null document throws ArgumentNullException")]
    public async Task AppendRichAsync_NullDocument_ThrowsArgumentNullException()
    {
        var act = () => _adapter.AppendRichAsync("doc-123", null!);

        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("document");
    }

    [Fact(DisplayName = "AppendRichAsync retrieves document and appends at end index")]
    public async Task AppendRichAsync_RetrievesDocument_AppendsAtEndIndex()
    {
        var documentId = "doc-123";
        var document = new GoogleDocsDocument();
        document.Add(new HeadingSection(1, "Test Heading"));

        var mockGoogleDoc = new Document
        {
            Body = new Body
            {
                Content = new List<StructuralElement>
                {
                    new() { StartIndex = 1, EndIndex = 100 },
                    new() { StartIndex = 100, EndIndex = 250 }
                }
            }
        };

        _clientWrapperMock
            .Setup(x => x.GetDocumentAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockGoogleDoc);

        IList<Request>? capturedRequests = null;
        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(documentId, It.IsAny<IList<Request>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, r, _) => capturedRequests = r)
            .Returns(Task.CompletedTask);

        await _adapter.AppendRichAsync(documentId, document);

        _clientWrapperMock.Verify(
            x => x.GetDocumentAsync(documentId, It.IsAny<CancellationToken>()),
            Times.Once);

        capturedRequests.Should().NotBeNull();
        var insertRequest = capturedRequests![0].InsertText;
        insertRequest.Should().NotBeNull();
        // The mock document's last element has EndIndex = 250.
        // Google Docs API inserts text *before* the given index, so to append at the end,
        // we use endIndex - 1 (i.e., 249) as the insertion point.
        insertRequest!.Location.Index.Should().Be(249);
    }

    [Fact(DisplayName = "AppendRichAsync with null document body throws InvalidOperationException")]
    public async Task AppendRichAsync_NullDocumentBody_ThrowsInvalidOperationException()
    {
        var documentId = "doc-123";
        var document = new GoogleDocsDocument();
        document.Add(new HeadingSection(1, "Test Heading"));

        var mockGoogleDoc = new Document { Body = null };

        _clientWrapperMock
            .Setup(x => x.GetDocumentAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockGoogleDoc);

        var act = () => _adapter.AppendRichAsync(documentId, document);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Unable to retrieve document content for documentId '{documentId}'.");
    }

    [Fact(DisplayName = "AppendRichAsync with empty document uses default index")]
    public async Task AppendRichAsync_EmptyDocument_UsesDefaultIndex()
    {
        var documentId = "doc-123";
        var document = new GoogleDocsDocument();
        document.Add(new ParagraphSection("Test content"));

        var mockGoogleDoc = new Document
        {
            Body = new Body
            {
                Content = new List<StructuralElement>()
            }
        };

        _clientWrapperMock
            .Setup(x => x.GetDocumentAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockGoogleDoc);

        IList<Request>? capturedRequests = null;
        _clientWrapperMock
            .Setup(x => x.BatchUpdateAsync(documentId, It.IsAny<IList<Request>>(), It.IsAny<CancellationToken>()))
            .Callback<string, IList<Request>, CancellationToken>((_, r, _) => capturedRequests = r)
            .Returns(Task.CompletedTask);

        await _adapter.AppendRichAsync(documentId, document);

        capturedRequests.Should().NotBeNull();
        var insertRequest = capturedRequests![0].InsertText;
        insertRequest.Should().NotBeNull();
        // The implementation uses DefaultIfEmpty(1) on the content indices, so for an empty sequence, Max() returns 1.
        // Subtracting 1 gives 0, so the expected index for an empty document is 0.
        insertRequest!.Location.Index.Should().Be(0);
    }

    #endregion
}
