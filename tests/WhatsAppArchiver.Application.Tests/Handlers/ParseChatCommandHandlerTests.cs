using Moq;
using WhatsAppArchiver.Application.Commands;
using WhatsAppArchiver.Application.Handlers;
using WhatsAppArchiver.Application.Services;
using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Application.Tests.Handlers;

public class ParseChatCommandHandlerTests
{
    private readonly Mock<IChatParser> _chatParserMock;
    private readonly ParseChatCommandHandler _handler;

    public ParseChatCommandHandlerTests()
    {
        _chatParserMock = new Mock<IChatParser>();
        _handler = new ParseChatCommandHandler(_chatParserMock.Object);
    }

    [Fact(DisplayName = "Constructor with null parser throws ArgumentNullException")]
    public void Constructor_NullParser_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new ParseChatCommandHandler(null!));

        Assert.Equal("chatParser", exception.ParamName);
    }

    [Fact(DisplayName = "HandleAsync with null command throws ArgumentNullException")]
    public async Task HandleAsync_NullCommand_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.HandleAsync(null!));

        Assert.Equal("command", exception.ParamName);
    }

    [Fact(DisplayName = "HandleAsync with valid command returns ChatExport")]
    public async Task HandleAsync_ValidCommand_ReturnsChatExport()
    {
        var filePath = "/path/to/chat.txt";
        var command = new ParseChatCommand(filePath);
        var expectedExport = CreateChatExport();

        _chatParserMock
            .Setup(x => x.ParseAsync(filePath, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedExport);

        var result = await _handler.HandleAsync(command);

        Assert.NotNull(result);
        Assert.Equal(expectedExport.Id, result.Id);
        _chatParserMock.Verify(x => x.ParseAsync(filePath, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "HandleAsync without sender filter returns all messages")]
    public async Task HandleAsync_WithoutSenderFilter_ReturnsAllMessages()
    {
        var filePath = "/path/to/chat.txt";
        var command = new ParseChatCommand(filePath);
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello"),
            ChatMessage.Create(DateTimeOffset.Now.AddMinutes(1), "Bob", "Hi there")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 2, 2, 0);
        var expectedExport = new ChatExport(Guid.NewGuid(), messages, metadata);

        _chatParserMock
            .Setup(x => x.ParseAsync(filePath, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedExport);

        var result = await _handler.HandleAsync(command);

        Assert.Equal(2, result.MessageCount);
    }

    [Fact(DisplayName = "HandleAsync with sender filter returns filtered messages")]
    public async Task HandleAsync_WithSenderFilter_ReturnsFilteredMessages()
    {
        var filePath = "/path/to/chat.txt";
        var command = new ParseChatCommand(filePath, "Alice");
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello"),
            ChatMessage.Create(DateTimeOffset.Now.AddMinutes(1), "Bob", "Hi there"),
            ChatMessage.Create(DateTimeOffset.Now.AddMinutes(2), "Alice", "How are you?")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 3, 3, 0);
        var originalExport = new ChatExport(Guid.NewGuid(), messages, metadata);

        _chatParserMock
            .Setup(x => x.ParseAsync(filePath, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalExport);

        var result = await _handler.HandleAsync(command);

        Assert.Equal(2, result.MessageCount);
        Assert.All(result.Messages, m => Assert.Equal("Alice", m.Sender));
    }

    [Fact(DisplayName = "HandleAsync with case-insensitive sender filter returns filtered messages")]
    public async Task HandleAsync_WithCaseInsensitiveSenderFilter_ReturnsFilteredMessages()
    {
        var filePath = "/path/to/chat.txt";
        var command = new ParseChatCommand(filePath, "ALICE");
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello"),
            ChatMessage.Create(DateTimeOffset.Now.AddMinutes(1), "Bob", "Hi there")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 2, 2, 0);
        var originalExport = new ChatExport(Guid.NewGuid(), messages, metadata);

        _chatParserMock
            .Setup(x => x.ParseAsync(filePath, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalExport);

        var result = await _handler.HandleAsync(command);

        Assert.Single(result.Messages);
        Assert.Equal("Alice", result.Messages[0].Sender);
    }

    [Fact(DisplayName = "HandleAsync with non-matching sender filter returns empty collection")]
    public async Task HandleAsync_WithNonMatchingSenderFilter_ReturnsEmptyCollection()
    {
        var filePath = "/path/to/chat.txt";
        var command = new ParseChatCommand(filePath, "Charlie");
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Alice", "Hello"),
            ChatMessage.Create(DateTimeOffset.Now.AddMinutes(1), "Bob", "Hi there")
        };
        var metadata = new ParsingMetadata("chat.txt", DateTimeOffset.Now, 2, 2, 0);
        var originalExport = new ChatExport(Guid.NewGuid(), messages, metadata);

        _chatParserMock
            .Setup(x => x.ParseAsync(filePath, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(originalExport);

        var result = await _handler.HandleAsync(command);

        Assert.Empty(result.Messages);
    }

    [Fact(DisplayName = "HandleAsync passes cancellation token to parser")]
    public async Task HandleAsync_WithCancellationToken_PassesToParser()
    {
        var filePath = "/path/to/chat.txt";
        var command = new ParseChatCommand(filePath);
        using var cts = new CancellationTokenSource();
        var expectedExport = CreateChatExport();

        _chatParserMock
            .Setup(x => x.ParseAsync(filePath, It.IsAny<TimeSpan?>(), cts.Token))
            .ReturnsAsync(expectedExport);

        await _handler.HandleAsync(command, cts.Token);

        _chatParserMock.Verify(x => x.ParseAsync(filePath, It.IsAny<TimeSpan?>(), cts.Token), Times.Once);
    }

    private static ChatExport CreateChatExport()
    {
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.Now, "Test User", "Test message")
        };
        var metadata = new ParsingMetadata("test.txt", DateTimeOffset.Now, 1, 1, 0);

        return new ChatExport(Guid.NewGuid(), messages, metadata);
    }
}
