using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Tests.Entities;

public class ChatMessageTests
{
    [Fact(DisplayName = "Constructor with valid parameters creates ChatMessage")]
    public void Constructor_ValidParameters_CreatesChatMessage()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var sender = "John Doe";
        var content = "Hello, World!";

        var message = new ChatMessage(timestamp, sender, content);

        Assert.Equal(timestamp, message.Timestamp);
        Assert.Equal(sender, message.Sender);
        Assert.Equal(content, message.Content);
        Assert.NotNull(message.Id);
    }

    [Fact(DisplayName = "Constructor generates MessageId from timestamp and content")]
    public void Constructor_ValidParameters_GeneratesMessageId()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var content = "Hello, World!";

        var message = new ChatMessage(timestamp, "John", content);

        Assert.Equal(timestamp, message.Id.Timestamp);
        Assert.NotEmpty(message.Id.ContentHash);
    }

    [Theory(DisplayName = "Constructor with null or whitespace sender throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceSender_ThrowsArgumentException(string? sender)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var content = "Hello";

        var exception = Assert.Throws<ArgumentException>(() =>
            new ChatMessage(timestamp, sender!, content));

        Assert.Equal("sender", exception.ParamName);
    }

    [Theory(DisplayName = "Constructor with null or whitespace content throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceContent_ThrowsArgumentException(string? content)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var sender = "John";

        var exception = Assert.Throws<ArgumentException>(() =>
            new ChatMessage(timestamp, sender, content!));

        Assert.Equal("content", exception.ParamName);
    }

    [Fact(DisplayName = "Create with valid parameters creates ChatMessage")]
    public void Create_ValidParameters_CreatesChatMessage()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var sender = "John Doe";
        var content = "Hello!";

        var message = ChatMessage.Create(timestamp, sender, content);

        Assert.NotNull(message);
        Assert.Equal(sender, message.Sender);
        Assert.Equal(content, message.Content);
    }

    [Fact(DisplayName = "Equality with same values returns true")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var message1 = new ChatMessage(timestamp, "John", "Hello");
        var message2 = new ChatMessage(timestamp, "John", "Hello");

        Assert.Equal(message1, message2);
    }

    [Fact(DisplayName = "Equality with different content returns false")]
    public void Equals_DifferentContent_ReturnsFalse()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var message1 = new ChatMessage(timestamp, "John", "Hello");
        var message2 = new ChatMessage(timestamp, "John", "World");

        Assert.NotEqual(message1, message2);
    }

    [Fact(DisplayName = "Messages with same content but different timestamps have different Ids")]
    public void Constructor_SameContentDifferentTimestamps_DifferentIds()
    {
        var content = "Hello";
        var message1 = new ChatMessage(DateTimeOffset.UtcNow, "John", content);
        var message2 = new ChatMessage(DateTimeOffset.UtcNow.AddSeconds(1), "John", content);

        Assert.NotEqual(message1.Id.Timestamp, message2.Id.Timestamp);
    }

    [Fact(DisplayName = "ChatMessage is immutable")]
    public void ChatMessage_RecordType_IsImmutable()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var message = new ChatMessage(timestamp, "John", "Hello");

        Assert.True(typeof(ChatMessage).IsSealed);
    }
}
