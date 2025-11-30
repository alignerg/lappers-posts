using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Specifications;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.Aggregates;

public class ChatExportTests
{
    private static ParsingMetadata CreateTestMetadata() =>
        new("test.txt", DateTimeOffset.UtcNow, 100, 95, 5);

    private static ChatMessage CreateTestMessage(string sender = "John", string content = "Hello") =>
        new(DateTimeOffset.UtcNow, sender, content);

    [Fact(DisplayName = "Constructor with valid parameters creates ChatExport")]
    public void Constructor_ValidParameters_CreatesChatExport()
    {
        var id = Guid.NewGuid();
        var messages = new[] { CreateTestMessage() };
        var metadata = CreateTestMetadata();

        var export = new ChatExport(id, messages, metadata);

        Assert.Equal(id, export.Id);
        Assert.Single(export.Messages);
        Assert.Equal(metadata, export.Metadata);
    }

    [Fact(DisplayName = "Constructor with empty Guid throws ArgumentException")]
    public void Constructor_EmptyGuid_ThrowsArgumentException()
    {
        var messages = new[] { CreateTestMessage() };
        var metadata = CreateTestMetadata();

        var exception = Assert.Throws<ArgumentException>(() =>
            new ChatExport(Guid.Empty, messages, metadata));

        Assert.Equal("id", exception.ParamName);
    }

    [Fact(DisplayName = "Constructor with null messages throws ArgumentNullException")]
    public void Constructor_NullMessages_ThrowsArgumentNullException()
    {
        var id = Guid.NewGuid();
        var metadata = CreateTestMetadata();

        Assert.Throws<ArgumentNullException>(() => new ChatExport(id, null!, metadata));
    }

    [Fact(DisplayName = "Constructor with null metadata throws ArgumentNullException")]
    public void Constructor_NullMetadata_ThrowsArgumentNullException()
    {
        var id = Guid.NewGuid();
        var messages = new[] { CreateTestMessage() };

        Assert.Throws<ArgumentNullException>(() => new ChatExport(id, messages, null!));
    }

    [Fact(DisplayName = "Create generates new Guid and creates ChatExport")]
    public void Create_ValidParameters_CreatesChatExportWithGeneratedId()
    {
        var messages = new[] { CreateTestMessage() };
        var metadata = CreateTestMetadata();

        var export = ChatExport.Create(messages, metadata);

        Assert.NotEqual(Guid.Empty, export.Id);
        Assert.Single(export.Messages);
    }

    [Fact(DisplayName = "Messages collection is readonly")]
    public void Messages_ReturnsReadOnlyCollection()
    {
        var messages = new[] { CreateTestMessage() };
        var export = ChatExport.Create(messages, CreateTestMetadata());

        Assert.IsAssignableFrom<IReadOnlyList<ChatMessage>>(export.Messages);
    }

    [Fact(DisplayName = "FilterMessages with matching specification returns filtered messages")]
    public void FilterMessages_MatchingSpecification_ReturnsFilteredMessages()
    {
        var messages = new[]
        {
            CreateTestMessage("John", "Hello"),
            CreateTestMessage("Jane", "Hi"),
            CreateTestMessage("John", "How are you?")
        };
        var export = ChatExport.Create(messages, CreateTestMetadata());
        var filter = new SenderFilter("John");

        var result = export.FilterMessages(filter).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, m => Assert.Equal("John", m.Sender));
    }

    [Fact(DisplayName = "FilterMessages with null specification throws ArgumentNullException")]
    public void FilterMessages_NullSpecification_ThrowsArgumentNullException()
    {
        var export = ChatExport.Create([CreateTestMessage()], CreateTestMetadata());

        Assert.Throws<ArgumentNullException>(() => export.FilterMessages(null!).ToList());
    }

    [Fact(DisplayName = "GetMessagesBySender returns messages from specified sender")]
    public void GetMessagesBySender_ValidSender_ReturnsMatchingMessages()
    {
        var messages = new[]
        {
            CreateTestMessage("John", "Hello"),
            CreateTestMessage("Jane", "Hi"),
            CreateTestMessage("john", "Bye")
        };
        var export = ChatExport.Create(messages, CreateTestMetadata());

        var result = export.GetMessagesBySender("John").ToList();

        Assert.Equal(2, result.Count);
    }

    [Fact(DisplayName = "GetMessagesInRange returns messages within time range")]
    public void GetMessagesInRange_ValidRange_ReturnsMatchingMessages()
    {
        var baseTime = DateTimeOffset.UtcNow;
        var messages = new[]
        {
            new ChatMessage(baseTime.AddHours(-2), "John", "Old message"),
            new ChatMessage(baseTime, "John", "Current message"),
            new ChatMessage(baseTime.AddHours(2), "John", "Future message")
        };
        var export = ChatExport.Create(messages, CreateTestMetadata());

        var result = export.GetMessagesInRange(baseTime.AddHours(-1), baseTime.AddHours(1)).ToList();

        Assert.Single(result);
        Assert.Equal("Current message", result[0].Content);
    }

    [Fact(DisplayName = "MessageCount returns correct count")]
    public void MessageCount_WithMessages_ReturnsCorrectCount()
    {
        var messages = new[]
        {
            CreateTestMessage("John", "1"),
            CreateTestMessage("Jane", "2"),
            CreateTestMessage("Bob", "3")
        };
        var export = ChatExport.Create(messages, CreateTestMetadata());

        Assert.Equal(3, export.MessageCount);
    }

    [Fact(DisplayName = "GetDistinctSenders returns unique sender names")]
    public void GetDistinctSenders_WithDuplicateSenders_ReturnsUniqueSenders()
    {
        var messages = new[]
        {
            CreateTestMessage("John", "1"),
            CreateTestMessage("Jane", "2"),
            CreateTestMessage("John", "3")
        };
        var export = ChatExport.Create(messages, CreateTestMetadata());

        var senders = export.GetDistinctSenders().ToList();

        Assert.Equal(2, senders.Count);
        Assert.Contains("John", senders);
        Assert.Contains("Jane", senders);
    }

    [Fact(DisplayName = "Constructor with empty messages creates valid ChatExport")]
    public void Constructor_EmptyMessages_CreatesValidChatExport()
    {
        var export = ChatExport.Create([], CreateTestMetadata());

        Assert.Empty(export.Messages);
        Assert.Equal(0, export.MessageCount);
    }
}
