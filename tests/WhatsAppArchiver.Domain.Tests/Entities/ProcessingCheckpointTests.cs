using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Specifications;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.Entities;

public class ProcessingCheckpointTests
{
    [Fact(DisplayName = "Constructor with valid parameters creates ProcessingCheckpoint")]
    public void Constructor_ValidParameters_CreatesProcessingCheckpoint()
    {
        var id = Guid.NewGuid();
        var documentId = "doc-123";
        var timestamp = DateTimeOffset.UtcNow;
        var processedIds = new List<MessageId>();

        var checkpoint = new ProcessingCheckpoint(id, documentId, timestamp, processedIds);

        Assert.Equal(id, checkpoint.Id);
        Assert.Equal(documentId, checkpoint.DocumentId);
        Assert.Equal(timestamp, checkpoint.LastProcessedTimestamp);
        Assert.Empty(checkpoint.ProcessedMessageIds);
    }

    [Fact(DisplayName = "Constructor with empty Guid throws ArgumentException")]
    public void Constructor_EmptyGuid_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ProcessingCheckpoint(Guid.Empty, "doc-123", null, []));

        Assert.Equal("id", exception.ParamName);
    }

    [Theory(DisplayName = "Constructor with null or whitespace document ID throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceDocumentId_ThrowsArgumentException(string? documentId)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new ProcessingCheckpoint(Guid.NewGuid(), documentId!, null, []));

        Assert.Equal("documentId", exception.ParamName);
    }

    [Fact(DisplayName = "Constructor with null processed message IDs throws ArgumentNullException")]
    public void Constructor_NullProcessedMessageIds_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProcessingCheckpoint(Guid.NewGuid(), "doc-123", null, null!));
    }

    [Fact(DisplayName = "Constructor with sender filter stores filter")]
    public void Constructor_WithSenderFilter_StoresFilter()
    {
        var filter = new SenderFilter("John");

        var checkpoint = new ProcessingCheckpoint(Guid.NewGuid(), "doc-123", null, [], filter);

        Assert.NotNull(checkpoint.SenderFilter);
        Assert.Equal("John", checkpoint.SenderFilter.SenderName);
    }

    [Fact(DisplayName = "Create generates new Guid and creates ProcessingCheckpoint")]
    public void Create_ValidDocumentId_CreatesProcessingCheckpointWithGeneratedId()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");

        Assert.NotEqual(Guid.Empty, checkpoint.Id);
        Assert.Equal("doc-123", checkpoint.DocumentId);
        Assert.Null(checkpoint.LastProcessedTimestamp);
        Assert.Empty(checkpoint.ProcessedMessageIds);
    }

    [Fact(DisplayName = "Create with sender filter creates ProcessingCheckpoint with filter")]
    public void Create_WithSenderFilter_CreatesProcessingCheckpointWithFilter()
    {
        var filter = new SenderFilter("John");

        var checkpoint = ProcessingCheckpoint.Create("doc-123", filter);

        Assert.NotNull(checkpoint.SenderFilter);
        Assert.Equal("John", checkpoint.SenderFilter.SenderName);
    }

    [Fact(DisplayName = "MarkAsProcessed adds message ID to processed set")]
    public void MarkAsProcessed_ValidMessageId_AddsToProcessedSet()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");
        var messageId = MessageId.Create(DateTimeOffset.UtcNow, "Hello");

        checkpoint.MarkAsProcessed(messageId);

        Assert.Single(checkpoint.ProcessedMessageIds);
        Assert.Contains(messageId, checkpoint.ProcessedMessageIds);
    }

    [Fact(DisplayName = "MarkAsProcessed updates LastProcessedTimestamp")]
    public void MarkAsProcessed_ValidMessageId_UpdatesLastProcessedTimestamp()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");
        var timestamp = DateTimeOffset.UtcNow;
        var messageId = MessageId.Create(timestamp, "Hello");

        checkpoint.MarkAsProcessed(messageId);

        Assert.Equal(timestamp, checkpoint.LastProcessedTimestamp);
    }

    [Fact(DisplayName = "MarkAsProcessed with null message ID throws ArgumentNullException")]
    public void MarkAsProcessed_NullMessageId_ThrowsArgumentNullException()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");

        Assert.Throws<ArgumentNullException>(() => checkpoint.MarkAsProcessed(null!));
    }

    [Fact(DisplayName = "MarkAsProcessed with earlier timestamp does not update LastProcessedTimestamp")]
    public void MarkAsProcessed_EarlierTimestamp_DoesNotUpdateLastProcessedTimestamp()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");
        var laterTime = DateTimeOffset.UtcNow;
        var earlierTime = laterTime.AddHours(-1);

        checkpoint.MarkAsProcessed(MessageId.Create(laterTime, "Later"));
        checkpoint.MarkAsProcessed(MessageId.Create(earlierTime, "Earlier"));

        Assert.Equal(laterTime, checkpoint.LastProcessedTimestamp);
    }

    [Fact(DisplayName = "HasBeenProcessed returns true for processed message")]
    public void HasBeenProcessed_ProcessedMessage_ReturnsTrue()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");
        var messageId = MessageId.Create(DateTimeOffset.UtcNow, "Hello");
        checkpoint.MarkAsProcessed(messageId);

        var result = checkpoint.HasBeenProcessed(messageId);

        Assert.True(result);
    }

    [Fact(DisplayName = "HasBeenProcessed returns false for unprocessed message")]
    public void HasBeenProcessed_UnprocessedMessage_ReturnsFalse()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");
        var messageId = MessageId.Create(DateTimeOffset.UtcNow, "Hello");

        var result = checkpoint.HasBeenProcessed(messageId);

        Assert.False(result);
    }

    [Fact(DisplayName = "HasBeenProcessed with null message ID throws ArgumentNullException")]
    public void HasBeenProcessed_NullMessageId_ThrowsArgumentNullException()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");

        Assert.Throws<ArgumentNullException>(() => checkpoint.HasBeenProcessed(null!));
    }

    [Fact(DisplayName = "ProcessedCount returns correct count")]
    public void ProcessedCount_WithProcessedMessages_ReturnsCorrectCount()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");
        checkpoint.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "1"));
        checkpoint.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "2"));
        checkpoint.MarkAsProcessed(MessageId.Create(DateTimeOffset.UtcNow, "3"));

        Assert.Equal(3, checkpoint.ProcessedCount);
    }

    [Fact(DisplayName = "ProcessedMessageIds is readonly")]
    public void ProcessedMessageIds_ReturnsReadOnlySet()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");

        Assert.IsAssignableFrom<IReadOnlySet<MessageId>>(checkpoint.ProcessedMessageIds);
    }

    [Fact(DisplayName = "MarkAsProcessed with duplicate message ID does not add duplicate")]
    public void MarkAsProcessed_DuplicateMessageId_DoesNotAddDuplicate()
    {
        var checkpoint = ProcessingCheckpoint.Create("doc-123");
        var messageId = MessageId.Create(DateTimeOffset.UtcNow, "Hello");

        checkpoint.MarkAsProcessed(messageId);
        checkpoint.MarkAsProcessed(messageId);

        Assert.Equal(1, checkpoint.ProcessedCount);
    }
}
