using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class IGoogleDocsFormatterTests
{
    private sealed class TestGoogleDocsFormatter : IGoogleDocsFormatter
    {
        public GoogleDocsDocument FormatDocument(ChatExport chatExport, bool suppressTimestamps = false)
        {
            ArgumentNullException.ThrowIfNull(chatExport);

            var document = new GoogleDocsDocument();
            document.Add(new HeadingSection(1, "Test Document"));
            return document;
        }
    }

    [Fact(DisplayName = "FormatDocument with valid ChatExport returns GoogleDocsDocument")]
    public void FormatDocument_ValidChatExport_ReturnsGoogleDocsDocument()
    {
        var formatter = new TestGoogleDocsFormatter();
        var messages = new[] { ChatMessage.Create(DateTimeOffset.UtcNow, "John", "Hello") };
        var metadata = new ParsingMetadata("test.txt", DateTimeOffset.UtcNow, 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = formatter.FormatDocument(chatExport);

        Assert.NotNull(result);
        Assert.IsType<GoogleDocsDocument>(result);
    }

    [Fact(DisplayName = "FormatDocument with null ChatExport throws ArgumentNullException")]
    public void FormatDocument_NullChatExport_ThrowsArgumentNullException()
    {
        var formatter = new TestGoogleDocsFormatter();

        Assert.Throws<ArgumentNullException>(() => formatter.FormatDocument(null!));
    }

    [Fact(DisplayName = "TestGoogleDocsFormatter creates document with heading")]
    public void TestFormatter_CreatesDocumentWithHeading()
    {
        var formatter = new TestGoogleDocsFormatter();
        var messages = new[] { ChatMessage.Create(DateTimeOffset.UtcNow, "John", "Hello") };
        var metadata = new ParsingMetadata("test.txt", DateTimeOffset.UtcNow, 1, 1, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = formatter.FormatDocument(chatExport);

        Assert.Single(result.Sections);
        Assert.IsType<HeadingSection>(result.Sections[0]);
        var heading = (HeadingSection)result.Sections[0];
        Assert.Equal(1, heading.Level);
        Assert.Equal("Test Document", heading.Text);
    }
}
