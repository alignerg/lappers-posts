using FluentAssertions;
using Google.Apis.Docs.v1.Data;
using Moq;
using Polly;
using WhatsAppArchiver.Infrastructure;

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

    [Fact(DisplayName = "UploadAsync with content ending with newline does not create empty insert requests")]
    public async Task UploadAsync_ContentEndingWithNewline_DoesNotCreateEmptyInsertRequests()
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

        // Verify no empty text is inserted
        insertTextRequests.Should().NotContain(string.Empty);
        insertTextRequests.Should().NotContain(text => string.IsNullOrEmpty(text));
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
}
