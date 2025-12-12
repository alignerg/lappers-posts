using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class MarkdownDocumentFormatterTests
{
    private readonly MarkdownDocumentFormatter _formatter = new();

    [Fact(DisplayName = "FormatDocument with single day messages produces correct structure")]
    public void FormatDocument_SingleDayMessages_ProducesCorrectStructure()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "John Doe", "First message"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 35, 0, TimeSpan.Zero), "John Doe", "Second message"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 40, 0, TimeSpan.Zero), "John Doe", "Third message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 3, 3, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("# WhatsApp Conversation Export - John Doe", result);
        Assert.Contains("**Export Date:** January 15, 2024", result);
        Assert.Contains("**Total Messages:** 3", result);
        Assert.Contains("---", result);
        Assert.Contains("## January 15, 2024", result);
        Assert.Contains("**10:30**", result);
        Assert.Contains("First message", result);
        Assert.Contains("**10:35**", result);
        Assert.Contains("Second message", result);
        Assert.Contains("**10:40**", result);
        Assert.Contains("Third message", result);
        
        // Should have only one date header for single day
        var dateHeaderCount = result.Split("## January 15, 2024").Length - 1;
        Assert.Equal(1, dateHeaderCount);
    }

    [Fact(DisplayName = "FormatDocument with multiple days groups by date chronologically")]
    public void FormatDocument_MultipleDays_GroupsByDateChronologically()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 16, 10, 0, 0, TimeSpan.Zero), "John", "Day 2 message"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", "Day 1 message"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 17, 10, 0, 0, TimeSpan.Zero), "John", "Day 3 message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 18, 12, 0, 0, TimeSpan.Zero), 3, 3, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("## January 15, 2024", result);
        Assert.Contains("## January 16, 2024", result);
        Assert.Contains("## January 17, 2024", result);
        
        // Verify chronological ordering
        var indexOfJan15 = result.IndexOf("## January 15, 2024");
        var indexOfJan16 = result.IndexOf("## January 16, 2024");
        var indexOfJan17 = result.IndexOf("## January 17, 2024");
        
        Assert.True(indexOfJan15 < indexOfJan16, "January 15 should appear before January 16");
        Assert.True(indexOfJan16 < indexOfJan17, "January 16 should appear before January 17");
    }

    [Fact(DisplayName = "FormatDocument with multi-line message preserves line breaks")]
    public void FormatDocument_MultiLineMessage_PreservesLineBreaks()
    {
        var multiLineContent = "Line 1 of message\nLine 2 of message\nLine 3 of message";
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", multiLineContent)
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("Line 1 of message\nLine 2 of message\nLine 3 of message", result);
    }

    [Fact(DisplayName = "FormatDocument with empty export returns header only")]
    public void FormatDocument_EmptyExport_ReturnsHeaderOnly()
    {
        var messages = Array.Empty<ChatMessage>();
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 0, 0, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        Assert.Contains("# WhatsApp Conversation Export - Unknown", result);
        Assert.Contains("**Export Date:** January 15, 2024", result);
        Assert.Contains("**Total Messages:** 0", result);
        Assert.Contains("---", result);
        Assert.DoesNotContain("##", result); // No date sections for empty export
    }

    [Fact(DisplayName = "FormatDocument with special characters does not escape markdown")]
    public void FormatDocument_SpecialCharacters_DoesNotEscapeMarkdown()
    {
        var specialContent = "Test @#$%^&*() with **bold** and _italic_ and [link](url) 🎉";
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", specialContent)
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        // Verify special characters and markdown syntax are preserved as-is
        Assert.Contains(specialContent, result);
        Assert.Contains("**bold**", result);
        Assert.Contains("_italic_", result);
        Assert.Contains("[link](url)", result);
    }

    [Fact(DisplayName = "FormatDocument date format uses friendly format")]
    public void FormatDocument_DateFormat_UsesFriendlyFormat()
    {
        var parseDate = new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 3, 5, 10, 0, 0, TimeSpan.Zero), "John", "Test message")
        };
        var metadata = ParsingMetadata.Create("test.txt", parseDate, 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        // Verify export date uses MMMM d, yyyy format (e.g., "June 15, 2024")
        Assert.Contains("**Export Date:** June 15, 2024", result);
        
        // Verify message date header uses MMMM d, yyyy format (e.g., "March 5, 2024")
        Assert.Contains("## March 5, 2024", result);
    }

    [Fact(DisplayName = "FormatDocument time format uses 24-hour format")]
    public void FormatDocument_TimeFormat_Uses24HourFormat()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 14, 45, 30, TimeSpan.Zero), "John", "Afternoon"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 09, 05, 15, TimeSpan.Zero), "John", "Morning"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 00, 30, 00, TimeSpan.Zero), "John", "Midnight")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 18, 0, 0, TimeSpan.Zero), 3, 3, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        // Verify 24-hour HH:mm format (no AM/PM, leading zeros)
        Assert.Contains("**14:45**", result);
        Assert.Contains("**09:05**", result);
        Assert.Contains("**00:30**", result);
        
        // Verify no 12-hour format indicators
        Assert.DoesNotContain("AM", result);
        Assert.DoesNotContain("PM", result);
        Assert.DoesNotContain("am", result);
        Assert.DoesNotContain("pm", result);
    }

    [Fact(DisplayName = "FormatMessage when called throws NotSupportedException")]
    public void FormatMessage_Called_ThrowsNotSupportedException()
    {
        var message = ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", "Test");

        var exception = Assert.Throws<NotSupportedException>(() => _formatter.FormatMessage(message));

        Assert.Contains("MarkdownDocumentFormatter requires FormatDocument method for batch processing", exception.Message);
        Assert.Contains("Use IDocumentFormatter.FormatDocument instead", exception.Message);
    }

    [Fact(DisplayName = "FormatDocument with null export throws ArgumentNullException")]
    public void FormatDocument_NullExport_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _formatter.FormatDocument(null!));

        Assert.Equal("chatExport", exception.ParamName);
    }

    [Fact(DisplayName = "FormatDocument extracts sender name from first message")]
    public void FormatDocument_ExtractsSenderNameFromFirstMessage()
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

    [Fact(DisplayName = "FormatDocument includes horizontal rule separators between messages")]
    public void FormatDocument_IncludesHorizontalRuleSeparators()
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
