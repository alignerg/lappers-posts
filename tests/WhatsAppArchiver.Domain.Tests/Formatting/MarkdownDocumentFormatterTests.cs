using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class MarkdownDocumentFormatterTests
{
    private readonly MarkdownDocumentFormatter _formatter = new();

    private static ParsingMetadata CreateTestMetadata(int messageCount = 0)
        => ParsingMetadata.Create("test.txt", DateTimeOffset.UtcNow, messageCount, messageCount, 0);

    [Fact(DisplayName = "FormatDocument with single day messages produces correct structure")]
    public void FormatDocument_SingleDayMessages_ProducesCorrectStructure()
    {
        var timestamp = new DateTimeOffset(2024, 12, 8, 10, 30, 0, TimeSpan.Zero);
        var messages = new[]
        {
            ChatMessage.Create(timestamp, "John Doe", "First message"),
            ChatMessage.Create(timestamp.AddHours(1), "John Doe", "Second message"),
            ChatMessage.Create(timestamp.AddHours(2), "John Doe", "Third message")
        };
        var metadata = CreateTestMetadata(3);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("# WhatsApp Conversation Export - John Doe", result);
        Assert.Contains("**Total Messages:** 3", result);
        Assert.Contains("## December 8, 2024", result);
        Assert.Contains("**10:30**", result);
        Assert.Contains("**11:30**", result);
        Assert.Contains("**12:30**", result);
        var separatorCount = result.Split("---", StringSplitOptions.None).Length - 1;
        Assert.Equal(4, separatorCount); // 1 after metadata + 3 after messages
    }

    [Fact(DisplayName = "FormatDocument with multiple days groups by date chronologically")]
    public void FormatDocument_MultipleDays_GroupsByDateChronologically()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 12, 10, 14, 0, 0, TimeSpan.Zero), "Alice", "Day 3"),
            ChatMessage.Create(new DateTimeOffset(2024, 12, 8, 10, 0, 0, TimeSpan.Zero), "Alice", "Day 1"),
            ChatMessage.Create(new DateTimeOffset(2024, 12, 9, 12, 0, 0, TimeSpan.Zero), "Alice", "Day 2")
        };
        var metadata = CreateTestMetadata(3);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        var dec8Index = result.IndexOf("## December 8, 2024", StringComparison.Ordinal);
        var dec9Index = result.IndexOf("## December 9, 2024", StringComparison.Ordinal);
        var dec10Index = result.IndexOf("## December 10, 2024", StringComparison.Ordinal);
        
        Assert.True(dec8Index < dec9Index, "December 8 should come before December 9");
        Assert.True(dec9Index < dec10Index, "December 9 should come before December 10");
    }

    [Fact(DisplayName = "FormatDocument with multi-line message preserves line breaks")]
    public void FormatDocument_MultiLineMessage_PreservesLineBreaks()
    {
        var multiLineContent = "First paragraph\n\nSecond paragraph\n\nThird paragraph";
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.UtcNow, "Bob", multiLineContent)
        };
        var metadata = CreateTestMetadata(1);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("First paragraph", result);
        Assert.Contains("Second paragraph", result);
        Assert.Contains("Third paragraph", result);
    }

    [Fact(DisplayName = "FormatDocument with empty export returns header only")]
    public void FormatDocument_EmptyExport_ReturnsHeaderOnly()
    {
        var metadata = CreateTestMetadata(0);
        var chatExport = ChatExport.Create(Array.Empty<ChatMessage>(), metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("# WhatsApp Conversation Export - Unknown User", result);
        Assert.Contains("**Total Messages:** 0", result);
        
        var afterMetadataSeparator = result.Substring(result.IndexOf("---", StringComparison.Ordinal));
        Assert.DoesNotContain("## ", afterMetadataSeparator);
    }

    [Fact(DisplayName = "FormatDocument with special characters does not escape markdown")]
    public void FormatDocument_SpecialCharacters_DoesNotEscapeMarkdown()
    {
        var contentWithSpecialChars = "Hello *bold* _italic_ #hashtag -dash";
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.UtcNow, "Charlie", contentWithSpecialChars)
        };
        var metadata = CreateTestMetadata(1);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("Hello *bold* _italic_ #hashtag -dash", result);
    }

    [Fact(DisplayName = "FormatDocument date format uses friendly format")]
    public void FormatDocument_DateFormat_UsesFriendlyFormat()
    {
        var timestamp = new DateTimeOffset(2024, 12, 8, 10, 0, 0, TimeSpan.Zero);
        var messages = new[]
        {
            ChatMessage.Create(timestamp, "Dave", "Test message")
        };
        var metadata = CreateTestMetadata(1);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("## December 8, 2024", result);
        Assert.DoesNotContain("12/8/2024", result);
        Assert.DoesNotContain("2024-12-08", result);
    }

    [Fact(DisplayName = "FormatDocument time format uses 24-hour format")]
    public void FormatDocument_TimeFormat_Uses24HourFormat()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 12, 8, 1, 0, 0, TimeSpan.Zero), "Eve", "1 AM message"),
            ChatMessage.Create(new DateTimeOffset(2024, 12, 8, 13, 0, 0, TimeSpan.Zero), "Eve", "1 PM message"),
            ChatMessage.Create(new DateTimeOffset(2024, 12, 8, 23, 59, 0, TimeSpan.Zero), "Eve", "Late night")
        };
        var metadata = CreateTestMetadata(3);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("**01:00**", result);
        Assert.Contains("**13:00**", result);
        Assert.Contains("**23:59**", result);
    }

    [Fact(DisplayName = "FormatMessage throws NotSupportedException")]
    public void FormatMessage_Called_ThrowsNotSupportedException()
    {
        var message = ChatMessage.Create(DateTimeOffset.UtcNow, "Test", "Content");

        var exception = Assert.Throws<NotSupportedException>(() => _formatter.FormatMessage(message));

        Assert.Contains("FormatDocument", exception.Message);
        Assert.Contains("batch processing", exception.Message);
    }

    [Fact(DisplayName = "FormatDocument with null chat export throws ArgumentNullException")]
    public void FormatDocument_NullChatExport_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _formatter.FormatDocument(null!));

        Assert.Equal("chatExport", exception.ParamName);
    }

    [Fact(DisplayName = "FormatDocument includes export date in metadata")]
    public void FormatDocument_ValidExport_IncludesExportDate()
    {
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.UtcNow, "Frank", "Message")
        };
        var metadata = CreateTestMetadata(1);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("**Export Date:**", result);
        Assert.Matches(@"\*\*Export Date:\*\* \w+ \d+, \d{4}", result);
    }
}
