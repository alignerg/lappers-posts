using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.ValueObjects;

public class MessageIdTests
{
    [Fact(DisplayName = "Constructor with valid parameters creates MessageId")]
    public void Constructor_ValidParameters_CreatesMessageId()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var contentHash = "ABC123DEF456";

        var messageId = new MessageId(timestamp, contentHash);

        Assert.Equal(timestamp, messageId.Timestamp);
        Assert.Equal(contentHash, messageId.ContentHash);
    }

    [Theory(DisplayName = "Constructor with null or whitespace content hash throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceContentHash_ThrowsArgumentException(string? contentHash)
    {
        var timestamp = DateTimeOffset.UtcNow;

        var exception = Assert.Throws<ArgumentException>(() => new MessageId(timestamp, contentHash!));

        Assert.Equal("contentHash", exception.ParamName);
    }

    [Fact(DisplayName = "Create with valid content generates deterministic hash")]
    public void Create_ValidContent_GeneratesDeterministicHash()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var content = "Hello, World!";

        var messageId1 = MessageId.Create(timestamp, content);
        var messageId2 = MessageId.Create(timestamp, content);

        Assert.Equal(messageId1.ContentHash, messageId2.ContentHash);
    }

    [Fact(DisplayName = "Create with different content generates different hash")]
    public void Create_DifferentContent_GeneratesDifferentHash()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var messageId1 = MessageId.Create(timestamp, "Hello");
        var messageId2 = MessageId.Create(timestamp, "World");

        Assert.NotEqual(messageId1.ContentHash, messageId2.ContentHash);
    }

    [Theory(DisplayName = "Create with null or whitespace content throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NullOrWhitespaceContent_ThrowsArgumentException(string? content)
    {
        var timestamp = DateTimeOffset.UtcNow;

        var exception = Assert.Throws<ArgumentException>(() => MessageId.Create(timestamp, content!));

        Assert.Equal("content", exception.ParamName);
    }

    [Fact(DisplayName = "Equality with same values returns true")]
    public void Equals_SameValues_ReturnsTrue()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var contentHash = "ABC123DEF456";

        var messageId1 = new MessageId(timestamp, contentHash);
        var messageId2 = new MessageId(timestamp, contentHash);

        Assert.Equal(messageId1, messageId2);
    }

    [Fact(DisplayName = "Equality with different values returns false")]
    public void Equals_DifferentValues_ReturnsFalse()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var messageId1 = new MessageId(timestamp, "HASH1");
        var messageId2 = new MessageId(timestamp, "HASH2");

        Assert.NotEqual(messageId1, messageId2);
    }

    [Fact(DisplayName = "GetHashCode with equal objects returns same hash")]
    public void GetHashCode_EqualObjects_ReturnsSameHash()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var contentHash = "ABC123DEF456";

        var messageId1 = new MessageId(timestamp, contentHash);
        var messageId2 = new MessageId(timestamp, contentHash);

        Assert.Equal(messageId1.GetHashCode(), messageId2.GetHashCode());
    }

    [Fact(DisplayName = "ToString returns formatted string")]
    public void ToString_ValidMessageId_ReturnsFormattedString()
    {
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var messageId = MessageId.Create(timestamp, "Test content");

        var result = messageId.ToString();

        Assert.Contains("2024-01-15", result);
        Assert.Contains("_", result);
    }
}
