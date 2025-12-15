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

        // Header section (H1)
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 1 && h.Text.Contains("John Doe"));
        
        // Metadata sections
        Assert.Contains(result.Sections, s => s is MetadataSection m && m.Label == "Export Date");
        Assert.Contains(result.Sections, s => s is MetadataSection m && m.Label == "Total Messages" && m.Value == "3");
        
        // Date header (H2)
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 2 && h.Text.Contains("January 15, 2024"));
        
        // Message sections - timestamps and content
        Assert.Contains(result.Sections, s => s is BoldTextSection b && b.Text == "10:30\n");
        Assert.Contains(result.Sections, s => s is ParagraphSection p && p.Text == "First message");
        Assert.Contains(result.Sections, s => s is BoldTextSection b && b.Text == "10:35\n");
        Assert.Contains(result.Sections, s => s is ParagraphSection p && p.Text == "Second message");
        Assert.Contains(result.Sections, s => s is BoldTextSection b && b.Text == "10:40\n");
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

    [Fact(DisplayName = "FormatDocument with empty export returns header only")]
    public void FormatDocument_EmptyExport_ReturnsHeaderOnly()
    {
        var messages = Array.Empty<ChatMessage>();
        var metadata = ParsingMetadata.Create("test.txt", new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.Zero), 0, 0, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = _formatter.FormatDocument(chatExport);

        // Should have H1, 2 metadata sections, and 1 horizontal rule
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 1 && h.Text.Contains("Unknown"));
        Assert.Contains(result.Sections, s => s is MetadataSection m && m.Label == "Total Messages" && m.Value == "0");
        Assert.Single(result.Sections.OfType<HorizontalRuleSection>());
        
        // Should NOT have any H2 sections (date headers)
        Assert.DoesNotContain(result.Sections, s => s is HeadingSection h && h.Level == 2);
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
            .FirstOrDefault();

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

        // Verify presence of all expected section types
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 1);
        Assert.Contains(result.Sections, s => s is MetadataSection);
        Assert.Contains(result.Sections, s => s is HeadingSection h && h.Level == 2);
        Assert.Contains(result.Sections, s => s is BoldTextSection);
        Assert.Contains(result.Sections, s => s is ParagraphSection);
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

        var boldSections = result.Sections.OfType<BoldTextSection>().ToList();
        
        // Verify 24-hour HH:mm format (no AM/PM, leading zeros) - now with newline
        Assert.Contains(boldSections, b => b.Text == "14:45\n");
        Assert.Contains(boldSections, b => b.Text == "09:05\n");
        Assert.Contains(boldSections, b => b.Text == "00:30\n");
    }

    [Fact(DisplayName = "FormatMessage when called throws NotSupportedException")]
    public void FormatMessage_Called_ThrowsNotSupportedException()
    {
        var message = ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero), "John", "Test");

        var exception = Assert.Throws<NotSupportedException>(() => _formatter.FormatMessage(message));

        Assert.Contains("GoogleDocsDocumentFormatter requires FormatDocument for batch processing", exception.Message);
    }
}
