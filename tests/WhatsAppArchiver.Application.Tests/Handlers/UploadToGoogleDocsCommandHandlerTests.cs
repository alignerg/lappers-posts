using Moq;
using WhatsAppArchiver.Application.Commands;
using WhatsAppArchiver.Application.Handlers;
using WhatsAppArchiver.Application.Services;
using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Domain.Specifications;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Application.Tests.Handlers;

public class UploadToGoogleDocsCommandHandlerTests
{
    private readonly Mock<IChatParser> _chatParserMock;
    private readonly Mock<IGoogleDocsService> _googleDocsServiceMock;
    private readonly Mock<IProcessingStateService> _processingStateServiceMock;
    private readonly UploadToGoogleDocsCommandHandler _handler;

    public UploadToGoogleDocsCommandHandlerTests()
    {
        _chatParserMock = new Mock<IChatParser>();
        _googleDocsServiceMock = new Mock<IGoogleDocsService>();
        _processingStateServiceMock = new Mock<IProcessingStateService>();
        _handler = new UploadToGoogleDocsCommandHandler(
            _chatParserMock.Object,
            _googleDocsServiceMock.Object,
            _processingStateServiceMock.Object);
    }

    [Fact(DisplayName = "Constructor with null chat parser throws ArgumentNullException")]
    public void Constructor_NullChatParser_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new UploadToGoogleDocsCommandHandler(
                null!,
                _googleDocsServiceMock.Object,
                _processingStateServiceMock.Object));

        Assert.Equal("chatParser", exception.ParamName);
    }

    [Fact(DisplayName = "Constructor with null Google Docs service throws ArgumentNullException")]
    public void Constructor_NullGoogleDocsService_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new UploadToGoogleDocsCommandHandler(
                _chatParserMock.Object,
                null!,
                _processingStateServiceMock.Object));

        Assert.Equal("googleDocsService", exception.ParamName);
    }

    [Fact(DisplayName = "Constructor with null processing state service throws ArgumentNullException")]
    public void Constructor_NullProcessingStateService_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new UploadToGoogleDocsCommandHandler(
                _chatParserMock.Object,
                _googleDocsServiceMock.Object,
                null!));

        Assert.Equal("processingStateService", exception.ParamName);
    }

    [Fact(DisplayName = "HandleAsync with null command throws ArgumentNullException")]
    public async Task HandleAsync_NullCommand_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.HandleAsync(null!));

        Assert.Equal("command", exception.ParamName);
    }

    [Fact(DisplayName = "HandleAsync with unprocessed messages returns message count")]
    public async Task HandleAsync_WithUnprocessedMessages_ReturnsMessageCount()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt", "Alice", "doc-123", MessageFormatType.Default);
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello"),
            ChatMessage.Create(DateTimeOffset.Now.AddMinutes(1), "Alice", "How are you?")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 2, 2, 0);
        var chatExport = new ChatExport(Guid.NewGuid(), messages, metadata);
        var checkpoint = ProcessingCheckpoint.Create(command.DocumentId, SenderFilter.Create(command.Sender));

        SetupMocks(command, chatExport, checkpoint);

        var result = await _handler.HandleAsync(command);

        Assert.Equal(2, result);
    }

    [Fact(DisplayName = "HandleAsync with no unprocessed messages returns zero")]
    public async Task HandleAsync_WithNoUnprocessedMessages_ReturnsZero()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt", "Alice", "doc-123", MessageFormatType.Default);
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 1, 1, 0);
        var chatExport = new ChatExport(Guid.NewGuid(), messages, metadata);
        var checkpoint = ProcessingCheckpoint.Create(command.DocumentId, SenderFilter.Create(command.Sender));
        checkpoint.MarkAsProcessed(messages[0].Id);

        SetupMocks(command, chatExport, checkpoint);

        var result = await _handler.HandleAsync(command);

        Assert.Equal(0, result);
        _googleDocsServiceMock.Verify(
            x => x.AppendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "HandleAsync filters messages by sender")]
    public async Task HandleAsync_FiltersBySender_OnlyProcessesSenderMessages()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt", "Alice", "doc-123", MessageFormatType.Default);
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello"),
            ChatMessage.Create(DateTimeOffset.Now.AddMinutes(1), "Bob", "Hi there"),
            ChatMessage.Create(DateTimeOffset.Now.AddMinutes(2), "Alice", "How are you?")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 3, 3, 0);
        var chatExport = new ChatExport(Guid.NewGuid(), messages, metadata);
        var checkpoint = ProcessingCheckpoint.Create(command.DocumentId, SenderFilter.Create(command.Sender));

        SetupMocks(command, chatExport, checkpoint);

        var result = await _handler.HandleAsync(command);

        Assert.Equal(2, result);
    }

    [Fact(DisplayName = "HandleAsync appends formatted content to Google Docs")]
    public async Task HandleAsync_AppendsFormattedContent_ToGoogleDocs()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt", "Alice", "doc-123", MessageFormatType.Default);
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 1, 1, 0);
        var chatExport = new ChatExport(Guid.NewGuid(), messages, metadata);
        var checkpoint = ProcessingCheckpoint.Create(command.DocumentId, SenderFilter.Create(command.Sender));

        SetupMocks(command, chatExport, checkpoint);

        await _handler.HandleAsync(command);

        _googleDocsServiceMock.Verify(
            x => x.AppendAsync(
                command.DocumentId,
                It.Is<string>(s => s.Contains("Alice") && s.Contains("Hello")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "HandleAsync saves checkpoint after successful upload")]
    public async Task HandleAsync_SavesCheckpoint_AfterSuccessfulUpload()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt", "Alice", "doc-123", MessageFormatType.Default);
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 1, 1, 0);
        var chatExport = new ChatExport(Guid.NewGuid(), messages, metadata);
        var checkpoint = ProcessingCheckpoint.Create(command.DocumentId, SenderFilter.Create(command.Sender));

        SetupMocks(command, chatExport, checkpoint);

        await _handler.HandleAsync(command);

        _processingStateServiceMock.Verify(
            x => x.SaveCheckpointAsync(
                It.Is<ProcessingCheckpoint>(c => c.ProcessedCount == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "HandleAsync does not save checkpoint when no messages uploaded")]
    public async Task HandleAsync_DoesNotSaveCheckpoint_WhenNoMessagesUploaded()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt", "Charlie", "doc-123", MessageFormatType.Default);
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello"),
            ChatMessage.Create(DateTimeOffset.Now.AddMinutes(1), "Bob", "Hi there")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 2, 2, 0);
        var chatExport = new ChatExport(Guid.NewGuid(), messages, metadata);
        var checkpoint = ProcessingCheckpoint.Create(command.DocumentId, SenderFilter.Create(command.Sender));

        SetupMocks(command, chatExport, checkpoint);

        var result = await _handler.HandleAsync(command);

        Assert.Equal(0, result);
        _processingStateServiceMock.Verify(
            x => x.SaveCheckpointAsync(It.IsAny<ProcessingCheckpoint>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName = "HandleAsync uses specified formatter type")]
    public async Task HandleAsync_UsesSpecifiedFormatterType_FormatsMessages()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt", "Alice", "doc-123", MessageFormatType.Compact);
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 1, 1, 0);
        var chatExport = new ChatExport(Guid.NewGuid(), messages, metadata);
        var checkpoint = ProcessingCheckpoint.Create(command.DocumentId, SenderFilter.Create(command.Sender));

        SetupMocks(command, chatExport, checkpoint);

        await _handler.HandleAsync(command);

        _googleDocsServiceMock.Verify(
            x => x.AppendAsync(
                command.DocumentId,
                It.Is<string>(s => s.Contains("Alice: Hello")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "HandleAsync processes messages in chronological order")]
    public async Task HandleAsync_ProcessesMessagesInChronologicalOrder()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt", "Alice", "doc-123", MessageFormatType.Compact);
        var now = DateTimeOffset.Now;
        var messages = new[]
        {
            ChatMessage.Create(now.AddMinutes(2), "Alice", "Third"),
            ChatMessage.Create(now, "Alice", "First"),
            ChatMessage.Create(now.AddMinutes(1), "Alice", "Second")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 3, 3, 0);
        var chatExport = new ChatExport(Guid.NewGuid(), messages, metadata);
        var checkpoint = ProcessingCheckpoint.Create(command.DocumentId, SenderFilter.Create(command.Sender));

        SetupMocks(command, chatExport, checkpoint);

        await _handler.HandleAsync(command);

        _googleDocsServiceMock.Verify(
            x => x.AppendAsync(
                command.DocumentId,
                It.Is<string>(s =>
                    s.IndexOf("First", StringComparison.Ordinal) <
                    s.IndexOf("Second", StringComparison.Ordinal) &&
                    s.IndexOf("Second", StringComparison.Ordinal) <
                    s.IndexOf("Third", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact(DisplayName = "HandleAsync passes cancellation token to all services")]
    public async Task HandleAsync_PassesCancellationToken_ToAllServices()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt", "Alice", "doc-123", MessageFormatType.Default);
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 1, 1, 0);
        var chatExport = new ChatExport(Guid.NewGuid(), messages, metadata);
        var checkpoint = ProcessingCheckpoint.Create(command.DocumentId, SenderFilter.Create(command.Sender));
        var cts = new CancellationTokenSource();

        _chatParserMock
            .Setup(x => x.ParseAsync(command.FilePath, cts.Token))
            .ReturnsAsync(chatExport);

        _processingStateServiceMock
            .Setup(x => x.GetCheckpointAsync(command.DocumentId, It.IsAny<SenderFilter>(), cts.Token))
            .ReturnsAsync(checkpoint);

        await _handler.HandleAsync(command, cts.Token);

        _chatParserMock.Verify(x => x.ParseAsync(command.FilePath, cts.Token), Times.Once);
        _processingStateServiceMock.Verify(
            x => x.GetCheckpointAsync(command.DocumentId, It.IsAny<SenderFilter>(), cts.Token),
            Times.Once);
        _googleDocsServiceMock.Verify(
            x => x.AppendAsync(command.DocumentId, It.IsAny<string>(), cts.Token),
            Times.Once);
        _processingStateServiceMock.Verify(
            x => x.SaveCheckpointAsync(It.IsAny<ProcessingCheckpoint>(), cts.Token),
            Times.Once);
    }

    private void SetupMocks(
        UploadToGoogleDocsCommand command,
        ChatExport chatExport,
        ProcessingCheckpoint checkpoint)
    {
        _chatParserMock
            .Setup(x => x.ParseAsync(command.FilePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chatExport);

        _processingStateServiceMock
            .Setup(x => x.GetCheckpointAsync(
                command.DocumentId,
                It.IsAny<SenderFilter>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(checkpoint);
    }
}
