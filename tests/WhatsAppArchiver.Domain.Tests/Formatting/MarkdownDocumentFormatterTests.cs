using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class MarkdownDocumentFormatterTests
{
    private readonly MarkdownDocumentFormatter _formatter = new();

    [Fact(DisplayName = "FormatDocument with valid export returns expected markdown structure")]
    public void FormatDocument_ValidExport_ReturnsExpectedMarkdownStructure()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "John Doe", "Hello!")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("# WhatsApp Conversation Export - John Doe", result);
        Assert.Contains("**Export Date:**", result);
        Assert.Contains("**Total Messages:** 1", result);
        Assert.Contains("---", result);
        Assert.Contains("## January 15, 2024", result);
        Assert.Contains("**10:30**", result);
        Assert.Contains("Hello!", result);
    }

    [Fact(DisplayName = "FormatDocument with null export throws ArgumentNullException")]
    public void FormatDocument_NullExport_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _formatter.FormatDocument(null!));

        Assert.Equal("chatExport", exception.ParamName);
    }

    [Fact(DisplayName = "FormatDocument extracts sender name from first message")]
    public void FormatDocument_ValidExport_ExtractsSenderNameFromFirstMessage()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "Alice Smith", "First message"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 1, 0, TimeSpan.Zero), "Bob Jones", "Second message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 2, 2, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("# WhatsApp Conversation Export - Alice Smith", result);
    }

    [Fact(DisplayName = "FormatDocument includes export date in MMMM d, yyyy format")]
    public void FormatDocument_ValidExport_IncludesExportDateInCorrectFormat()
    {
        var parseDate = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 6, 14, 10, 0, 0, TimeSpan.Zero), "John", "Test")
        };
        var metadata = ParsingMetadata.Create("test.txt", parseDate, 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("**Export Date:** June 15, 2024", result);
    }

    [Fact(DisplayName = "FormatDocument includes total message count")]
    public void FormatDocument_ValidExport_IncludesTotalMessageCount()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", "Message 1"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 1, 0, TimeSpan.Zero), "John", "Message 2"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 2, 0, TimeSpan.Zero), "John", "Message 3")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 3, 3, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("**Total Messages:** 3", result);
    }

    [Fact(DisplayName = "FormatDocument groups messages by date")]
    public void FormatDocument_MessagesOnDifferentDates_GroupsByDate()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "John", "Day 1 message"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 16, 09, 15, 0, TimeSpan.Zero), "John", "Day 2 message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 17, 12, 0, 0, TimeSpan.Zero), 2, 2, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("## January 15, 2024", result);
        Assert.Contains("## January 16, 2024", result);
    }

    [Fact(DisplayName = "FormatDocument orders date groups chronologically")]
    public void FormatDocument_UnorderedMessages_OrdersDateGroupsChronologically()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 16, 10, 0, 0, TimeSpan.Zero), "John", "Later"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", "Earlier")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 17, 12, 0, 0, TimeSpan.Zero), 2, 2, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        var indexOfJan15 = result.IndexOf("## January 15, 2024");
        var indexOfJan16 = result.IndexOf("## January 16, 2024");

        Assert.True(indexOfJan15 < indexOfJan16, "January 15 should appear before January 16");
    }

    [Fact(DisplayName = "FormatDocument formats timestamps in 24-hour HH:mm format")]
    public void FormatDocument_ValidMessages_FormatsTimestampsIn24HourFormat()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 14, 45, 30, TimeSpan.Zero), "John", "Afternoon message"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 09, 05, 15, TimeSpan.Zero), "John", "Morning message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 18, 0, 0, TimeSpan.Zero), 2, 2, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("**14:45**", result);
        Assert.Contains("**09:05**", result);
        
        // Verify chronological order within same date
        var indexOfMorning = result.IndexOf("**09:05**");
        var indexOfAfternoon = result.IndexOf("**14:45**");
        Assert.True(indexOfMorning < indexOfAfternoon, "Morning message (09:05) should appear before afternoon message (14:45)");
    }

    [Fact(DisplayName = "FormatDocument preserves multi-line message content")]
    public void FormatDocument_MultiLineContent_PreservesLineBreaks()
    {
        var multiLineContent = "This is line 1\nThis is line 2\nThis is line 3";
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", multiLineContent)
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("This is line 1\nThis is line 2\nThis is line 3", result);
    }

    [Fact(DisplayName = "FormatDocument includes horizontal rule separators between messages")]
    public void FormatDocument_ValidMessages_IncludesHorizontalRuleSeparators()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", "Message 1"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 1, 0, TimeSpan.Zero), "John", "Message 2")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 2, 2, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        var separatorCount = result.Split("---").Length - 1;
        Assert.True(separatorCount >= 3, "Should have at least 3 separators (header + 2 messages)");
    }

    [Fact(DisplayName = "FormatDocument with empty export returns header with zero messages")]
    public void FormatDocument_EmptyExport_ReturnsHeaderWithZeroMessages()
    {
        var messages = Array.Empty<ChatMessage>();
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 0, 0, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("# WhatsApp Conversation Export - Unknown", result);
        Assert.Contains("**Total Messages:** 0", result);
        Assert.Contains("---", result);
        Assert.DoesNotContain("##", result); // No date sections
    }

    [Fact(DisplayName = "FormatMessage throws NotSupportedException")]
    public void FormatMessage_AnyChatMessage_ThrowsNotSupportedException()
    {
        var message = ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", "Test");

        var exception = Assert.Throws<NotSupportedException>(() => _formatter.FormatMessage(message));

        Assert.Contains("MarkdownDocumentFormatter requires FormatDocument method for batch processing", exception.Message);
        Assert.Contains("Use IDocumentFormatter.FormatDocument instead", exception.Message);
    }

    [Fact(DisplayName = "FormatDocument produces valid markdown with multiple messages on same date")]
    public void FormatDocument_MultipleSameDateMessages_ProducesValidMarkdown()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "John", "First"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 35, 0, TimeSpan.Zero), "John", "Second"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 40, 0, TimeSpan.Zero), "John", "Third")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 3, 3, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        // Should have only one date header
        var dateHeaderCount = result.Split("## January 15, 2024").Length - 1;
        Assert.Equal(1, dateHeaderCount);

        // Should have all three messages
        Assert.Contains("**10:30**", result);
        Assert.Contains("First", result);
        Assert.Contains("**10:35**", result);
        Assert.Contains("Second", result);
        Assert.Contains("**10:40**", result);
        Assert.Contains("Third", result);
    }

    [Fact(DisplayName = "FormatDocument handles special characters in message content")]
    public void FormatDocument_SpecialCharacters_PreservesContent()
    {
        var specialContent = "Test @#$%^&*() with special chars 🎉";
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", specialContent)
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains(specialContent, result);
    }

    [Fact(DisplayName = "FormatDocument handles sender names with special characters")]
    public void FormatDocument_SenderWithSpecialChars_PreservesName()
    {
        var specialSender = "José María O'Connor";
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), specialSender, "Hello")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains($"# WhatsApp Conversation Export - {specialSender}", result);
    }
}
