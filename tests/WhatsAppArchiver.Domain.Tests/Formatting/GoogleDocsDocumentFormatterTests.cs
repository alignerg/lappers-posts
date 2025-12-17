using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class GoogleDocsDocumentFormatterTests
{
    private readonly GoogleDocsDocumentFormatter _formatter = new();

    [Fact(DisplayName = "FormatDocument with single day messages produces correct sections")]
    public void FormatDocument_SingleDayMessages_ProducesCorrectSections()
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

        // Date header (H2)
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 2 && h.Text.Contains("January 15, 2024"));
        
        // Message sections - timestamps as H3 headings and content
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 3 && h.Text == "10:30");
        Assert.Contains(result.Sections, s => s is ParagraphSection p && p.Text == "First message");
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 3 && h.Text == "10:35");
        Assert.Contains(result.Sections, s => s is ParagraphSection p && p.Text == "Second message");
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 3 && h.Text == "10:40");
        Assert.Contains(result.Sections, s => s is ParagraphSection p && p.Text == "Third message");
        
        // Empty lines (double empty lines after each message)
        var emptyLines = result.Sections.OfType<EmptyLineSection>().Count();
        Assert.True(emptyLines >= 6, "Should have at least 6 empty lines (2 per message for 3 messages)");
        
        // No page breaks for single day
        var pageBreaks = result.Sections.OfType<PageBreakSection>().Count();
        Assert.Equal(0, pageBreaks);
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

        // Get all H2 sections (date headers)
        var dateHeaders = result.Sections
            .OfType<HeadingSection>()
            .Where(h => h.Level == 2)
            .ToList();

        Assert.Equal(3, dateHeaders.Count);
        Assert.Contains("January 15, 2024", dateHeaders[0].Text);
        Assert.Contains("January 16, 2024", dateHeaders[1].Text);
        Assert.Contains("January 17, 2024", dateHeaders[2].Text);
        
        // Verify page breaks between dates (should have 2 page breaks for 3 dates)
        var pageBreaks = result.Sections.OfType<PageBreakSection>().Count();
        Assert.Equal(2, pageBreaks);
    }

    [Fact(DisplayName = "FormatDocument with multi-line message preserves paragraph content")]
    public void FormatDocument_MultiLineMessage_PreservesParagraphContent()
    {
        var multiLineContent = "Line 1 of message\nLine 2 of message\nLine 3 of message";
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", multiLineContent)
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        var paragraphSection = result.Sections
            .OfType<ParagraphSection>()
            .FirstOrDefault(p => p.Text.Contains("Line 1"));

        Assert.NotNull(paragraphSection);
        Assert.Equal(multiLineContent, paragraphSection.Text);
    }

    [Fact(DisplayName = "FormatDocument with empty export returns empty document")]
    public void FormatDocument_EmptyExport_ReturnsEmptyDocument()
    {
        var messages = Array.Empty<ChatMessage>();
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 0, 0, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        // Should be empty - no sections at all
        Assert.Empty(result.Sections);
    }

    [Fact(DisplayName = "FormatDocument with special characters preserves content")]
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

        var paragraphSection = result.Sections
            .OfType<ParagraphSection>()
            .FirstOrDefault(p => p.Text.Contains(specialContent));

        Assert.NotNull(paragraphSection);
        Assert.Equal(specialContent, paragraphSection.Text);
    }

    [Fact(DisplayName = "FormatDocument section types match expected structure")]
    public void FormatDocument_SectionTypes_MatchesExpectedStructure()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", "Test message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        // Verify presence of all expected section types (no H1 or metadata sections)
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 2);
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 3);
        Assert.Contains(result.Sections, s => s is ParagraphSection p && p.Text == "Test message");
        Assert.Contains(result.Sections, s => s is EmptyLineSection);
    }

    [Fact(DisplayName = "FormatDocument timestamp format uses 24-hour")]
    public void FormatDocument_TimestampFormat_Uses24Hour()
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

        var timestampHeadings = result.Sections.OfType<HeadingSection>().Where(h => h.Level == 3).ToList();
        
        // Verify 24-hour HH:mm format (no AM/PM, leading zeros)
        Assert.Contains(timestampHeadings, h => h.Text == "14:45");
        Assert.Contains(timestampHeadings, h => h.Text == "09:05");
        Assert.Contains(timestampHeadings, h => h.Text == "00:30");
    }

    [Fact(DisplayName = "FormatMessage when called throws NotSupportedException")]
    public void FormatMessage_Called_ThrowsNotSupportedException()
    {
        var message = ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", "Test");

        var exception = Assert.Throws<NotSupportedException>(() => _formatter.FormatMessage(message));

        Assert.Contains("GoogleDocsDocumentFormatter requires FormatDocument for batch processing", exception.Message);
    }

    [Fact(DisplayName = "FormatDocument timestamp as H3 heading without newline")]
    public void FormatDocument_Timestamp_AsH3Heading()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "John", "Test message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        // Verify timestamp is in HeadingSection level 3
        var timestampHeading = result.Sections
            .OfType<HeadingSection>()
            .FirstOrDefault(h => h.Level == 3 && h.Text == "10:30");
        Assert.NotNull(timestampHeading);
        Assert.Equal(3, timestampHeading.Level);
        Assert.Equal("10:30", timestampHeading.Text);

        // Verify there is NO PlainTextSection after the timestamp (it's a heading now)
        var sections = result.Sections.ToList();
        var timestampIndex = sections.IndexOf(timestampHeading);
        Assert.True(timestampIndex >= 0);
        Assert.True(timestampIndex + 1 < sections.Count);
        
        var nextSection = sections[timestampIndex + 1];
        Assert.IsType<ParagraphSection>(nextSection);
    }

    [Fact(DisplayName = "FormatDocument with suppressTimestamps true omits H3 timestamp headings")]
    public void FormatDocument_SuppressTimestampsTrue_OmitsTimestampHeadings()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "John", "First message"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 14, 45, 0, TimeSpan.Zero), "John", "Second message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 18, 0, 0, TimeSpan.Zero), 2, 2, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport, suppressTimestamps: true);

        // Should have no H3 headings (timestamps)
        var timestampHeadings = result.Sections.OfType<HeadingSection>().Where(h => h.Level == 3).ToList();
        Assert.Empty(timestampHeadings);

        // Should still have H2 date heading
        var dateHeading = result.Sections.OfType<HeadingSection>().FirstOrDefault(h => h.Level == 2);
        Assert.NotNull(dateHeading);
        Assert.Contains("January 15, 2024", dateHeading.Text);

        // Should have paragraph sections for messages
        var paragraphs = result.Sections.OfType<ParagraphSection>().ToList();
        Assert.Equal(2, paragraphs.Count);
        Assert.Equal("First message", paragraphs[0].Text);
        Assert.Equal("Second message", paragraphs[1].Text);
    }

    [Fact(DisplayName = "FormatDocument with suppressTimestamps false includes H3 timestamp headings")]
    public void FormatDocument_SuppressTimestampsFalse_IncludesTimestampHeadings()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "John", "First message"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 14, 45, 0, TimeSpan.Zero), "John", "Second message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 18, 0, 0, TimeSpan.Zero), 2, 2, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport, suppressTimestamps: false);

        // Should have H3 headings (timestamps)
        var timestampHeadings = result.Sections.OfType<HeadingSection>().Where(h => h.Level == 3).ToList();
        Assert.Equal(2, timestampHeadings.Count);
        Assert.Equal("10:30", timestampHeadings[0].Text);
        Assert.Equal("14:45", timestampHeadings[1].Text);
    }

    [Fact(DisplayName = "FormatDocument with suppressTimestamps maintains spacing between messages")]
    public void FormatDocument_SuppressTimestamps_MaintainsSpacing()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "John", "First message"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 14, 45, 0, TimeSpan.Zero), "John", "Second message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 18, 0, 0, TimeSpan.Zero), 2, 2, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport, suppressTimestamps: true);

        // Should still have double empty lines (2 per message)
        var emptyLines = result.Sections.OfType<EmptyLineSection>().Count();
        Assert.Equal(4, emptyLines); // 2 messages × 2 empty lines each
    }

    [Fact(DisplayName = "FormatDocument with suppressTimestamps and multiple days groups by date")]
    public void FormatDocument_SuppressTimestamps_MultipleDays_GroupsByDate()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", "Day 1 morning"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 14, 0, 0, TimeSpan.Zero), "John", "Day 1 afternoon"),
            ChatMessage.Create(new DateTimeOffset(2024, 1, 16, 10, 0, 0, TimeSpan.Zero), "John", "Day 2 message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 17, 12, 0, 0, TimeSpan.Zero), 3, 3, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport, suppressTimestamps: true);

        // Should have 2 H2 date headings
        var dateHeadings = result.Sections.OfType<HeadingSection>().Where(h => h.Level == 2).ToList();
        Assert.Equal(2, dateHeadings.Count);
        Assert.Contains("January 15, 2024", dateHeadings[0].Text);
        Assert.Contains("January 16, 2024", dateHeadings[1].Text);

        // Should have no H3 timestamp headings
        var timestampHeadings = result.Sections.OfType<HeadingSection>().Where(h => h.Level == 3).ToList();
        Assert.Empty(timestampHeadings);

        // Should have 3 paragraph sections
        var paragraphs = result.Sections.OfType<ParagraphSection>().ToList();
        Assert.Equal(3, paragraphs.Count);

        // Should have page break between dates
        var pageBreaks = result.Sections.OfType<PageBreakSection>().Count();
        Assert.Equal(1, pageBreaks);
    }

    [Fact(DisplayName = "FormatDocument default parameter includes timestamps for backward compatibility")]
    public void FormatDocument_DefaultParameter_IncludesTimestamps()
    {
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "John", "Test message")
        };
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        // Call without parameter (should default to false, meaning timestamps are included)
        var result = _formatter.FormatDocument(chatExport);

        // Should have H3 timestamp heading
        var timestampHeadings = result.Sections.OfType<HeadingSection>().Where(h => h.Level == 3).ToList();
        Assert.Single(timestampHeadings);
        Assert.Equal("10:30", timestampHeadings[0].Text);
    }
}

