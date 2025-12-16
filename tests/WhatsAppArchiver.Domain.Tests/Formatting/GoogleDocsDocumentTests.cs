using WhatsAppArchiver.Domain.Formatting;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class GoogleDocsDocumentTests
{
    [Fact(DisplayName = "Sections property returns empty list initially")]
    public void Sections_InitialState_ReturnsEmptyList()
    {
        var document = new GoogleDocsDocument();

        Assert.Empty(document.Sections);
    }

    [Fact(DisplayName = "Add with valid section adds section to document")]
    public void Add_ValidSection_AddsSectionToDocument()
    {
        var document = new GoogleDocsDocument();
        var section = new ParagraphSection("Test paragraph");

        document.Add(section);

        Assert.Single(document.Sections);
        Assert.Equal(section, document.Sections[0]);
    }

    [Fact(DisplayName = "Add with null section throws ArgumentNullException")]
    public void Add_NullSection_ThrowsArgumentNullException()
    {
        var document = new GoogleDocsDocument();

        Assert.Throws<ArgumentNullException>(() => document.Add(null!));
    }

    [Fact(DisplayName = "Add multiple sections maintains order")]
    public void Add_MultipleSections_MaintainsOrder()
    {
        var document = new GoogleDocsDocument();
        var section1 = new HeadingSection(1, "Heading");
        var section2 = new ParagraphSection("Paragraph");
        var section3 = new BoldTextSection("Bold");

        document.Add(section1);
        document.Add(section2);
        document.Add(section3);

        Assert.Equal(3, document.Sections.Count);
        Assert.Equal(section1, document.Sections[0]);
        Assert.Equal(section2, document.Sections[1]);
        Assert.Equal(section3, document.Sections[2]);
    }

    [Fact(DisplayName = "Sections returns readonly collection")]
    public void Sections_ReturnsReadOnlyCollection()
    {
        var document = new GoogleDocsDocument();

        Assert.IsAssignableFrom<IReadOnlyList<DocumentSection>>(document.Sections);
    }

    [Fact(DisplayName = "ToPlainText with empty document returns empty string")]
    public void ToPlainText_EmptyDocument_ReturnsEmptyString()
    {
        var document = new GoogleDocsDocument();

        var result = document.ToPlainText();

        Assert.Equal(string.Empty, result);
    }

    [Fact(DisplayName = "ToPlainText with heading section returns heading text")]
    public void ToPlainText_WithHeadingSection_ReturnsHeadingText()
    {
        var document = new GoogleDocsDocument();
        document.Add(new HeadingSection(1, "Test Heading"));

        var result = document.ToPlainText();

        Assert.Equal("Test Heading", result);
    }

    [Fact(DisplayName = "ToPlainText with bold text section returns bold text")]
    public void ToPlainText_WithBoldTextSection_ReturnsBoldText()
    {
        var document = new GoogleDocsDocument();
        document.Add(new BoldTextSection("Bold Text"));

        var result = document.ToPlainText();

        Assert.Equal("Bold Text", result);
    }

    [Fact(DisplayName = "ToPlainText with paragraph section returns paragraph text")]
    public void ToPlainText_WithParagraphSection_ReturnsParagraphText()
    {
        var document = new GoogleDocsDocument();
        document.Add(new ParagraphSection("Paragraph text"));

        var result = document.ToPlainText();

        Assert.Equal("Paragraph text", result);
    }

    [Fact(DisplayName = "ToPlainText with plain text section returns plain text")]
    public void ToPlainText_WithPlainTextSection_ReturnsPlainText()
    {
        var document = new GoogleDocsDocument();
        document.Add(new PlainTextSection("Plain text"));

        var result = document.ToPlainText();

        Assert.Equal("Plain text", result);
    }

    [Fact(DisplayName = "ToPlainText with horizontal rule returns separator")]
    public void ToPlainText_WithHorizontalRule_ReturnsSeparator()
    {
        var document = new GoogleDocsDocument();
        document.Add(new HorizontalRuleSection());

        var result = document.ToPlainText();

        Assert.Equal("---", result);
    }

    [Fact(DisplayName = "ToPlainText with metadata section returns label and value")]
    public void ToPlainText_WithMetadataSection_ReturnsLabelAndValue()
    {
        var document = new GoogleDocsDocument();
        document.Add(new MetadataSection("Label", "Value"));

        var result = document.ToPlainText();

        Assert.Equal("Label: Value", result);
    }

    [Fact(DisplayName = "ToPlainText with multiple sections returns all sections separated by newlines")]
    public void ToPlainText_WithMultipleSections_ReturnsAllSections()
    {
        var document = new GoogleDocsDocument();
        document.Add(new HeadingSection(1, "Title"));
        document.Add(new ParagraphSection("First paragraph"));
        document.Add(new HorizontalRuleSection());
        document.Add(new BoldTextSection("Bold text"));
        document.Add(new MetadataSection("Author", "John Doe"));

        var result = document.ToPlainText();

        var expected = "Title\nFirst paragraph\n---\nBold text\nAuthor: John Doe";
        Assert.Equal(expected, result);
    }
}

public class HeadingSectionTests
{
    [Fact(DisplayName = "Constructor with valid parameters creates HeadingSection")]
    public void Constructor_ValidParameters_CreatesHeadingSection()
    {
        var section = new HeadingSection(1, "Test Heading");

        Assert.Equal(1, section.Level);
        Assert.Equal("Test Heading", section.Text);
        Assert.Equal("Test Heading", section.Content);
    }

    [Theory(DisplayName = "Constructor with valid heading levels creates section")]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void Constructor_ValidHeadingLevels_CreatesSection(int level)
    {
        var section = new HeadingSection(level, "Heading");

        Assert.Equal(level, section.Level);
    }

    [Theory(DisplayName = "Constructor with invalid heading level throws ArgumentException")]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    [InlineData(10)]
    public void Constructor_InvalidHeadingLevel_ThrowsArgumentException(int level)
    {
        var exception = Assert.Throws<ArgumentException>(() => new HeadingSection(level, "Text"));

        Assert.Equal("level", exception.ParamName);
    }

    [Theory(DisplayName = "Constructor with null or whitespace text throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceText_ThrowsArgumentException(string? text)
    {
        var exception = Assert.Throws<ArgumentException>(() => new HeadingSection(1, text!));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact(DisplayName = "Content property returns same value as Text")]
    public void Content_ReturnsSameValueAsText()
    {
        var section = new HeadingSection(2, "Heading Text");

        Assert.Equal(section.Text, section.Content);
    }
}

public class BoldTextSectionTests
{
    [Fact(DisplayName = "Constructor with valid text creates BoldTextSection")]
    public void Constructor_ValidText_CreatesBoldTextSection()
    {
        var section = new BoldTextSection("Bold Text");

        Assert.Equal("Bold Text", section.Text);
        Assert.Equal("Bold Text", section.Content);
    }

    [Theory(DisplayName = "Constructor with null or whitespace text throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceText_ThrowsArgumentException(string? text)
    {
        var exception = Assert.Throws<ArgumentException>(() => new BoldTextSection(text!));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact(DisplayName = "Content property returns same value as Text")]
    public void Content_ReturnsSameValueAsText()
    {
        var section = new BoldTextSection("Bold Content");

        Assert.Equal(section.Text, section.Content);
    }

    [Fact(DisplayName = "Constructor preserves text with special characters")]
    public void Constructor_TextWithSpecialCharacters_PreservesText()
    {
        var text = "Text with special chars: @#$%^&*()";
        var section = new BoldTextSection(text);

        Assert.Equal(text, section.Text);
    }
}

public class PlainTextSectionTests
{
    [Fact(DisplayName = "Constructor with valid text creates PlainTextSection")]
    public void Constructor_ValidText_CreatesPlainTextSection()
    {
        var section = new PlainTextSection("Plain text");

        Assert.Equal("Plain text", section.Text);
        Assert.Equal("Plain text", section.Content);
    }

    [Fact(DisplayName = "Constructor with null text throws ArgumentNullException")]
    public void Constructor_NullText_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new PlainTextSection(null!));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact(DisplayName = "Constructor with whitespace-only text creates PlainTextSection")]
    public void Constructor_WhitespaceOnlyText_CreatesPlainTextSection()
    {
        var section = new PlainTextSection("   ");

        Assert.Equal("   ", section.Text);
        Assert.Equal("   ", section.Content);
    }

    [Fact(DisplayName = "Constructor with newline creates PlainTextSection")]
    public void Constructor_NewlineText_CreatesPlainTextSection()
    {
        var section = new PlainTextSection("\n");

        Assert.Equal("\n", section.Text);
        Assert.Equal("\n", section.Content);
    }

    [Fact(DisplayName = "Constructor with empty string creates PlainTextSection")]
    public void Constructor_EmptyString_CreatesPlainTextSection()
    {
        var section = new PlainTextSection("");

        Assert.Equal("", section.Text);
        Assert.Equal("", section.Content);
    }

    [Fact(DisplayName = "Content property returns same value as Text")]
    public void Content_ReturnsSameValueAsText()
    {
        var section = new PlainTextSection("Plain content");

        Assert.Equal(section.Text, section.Content);
    }

    [Fact(DisplayName = "Constructor preserves text with special characters")]
    public void Constructor_TextWithSpecialCharacters_PreservesText()
    {
        var text = "Text with special chars: @#$%^&*()";
        var section = new PlainTextSection(text);

        Assert.Equal(text, section.Text);
    }

    [Fact(DisplayName = "Constructor supports multi-line text")]
    public void Constructor_MultiLineText_PreservesMultiLineText()
    {
        var text = "Line 1\nLine 2\nLine 3";
        var section = new PlainTextSection(text);

        Assert.Equal(text, section.Text);
        Assert.Contains("\n", section.Text);
    }
}

public class ParagraphSectionTests
{
    [Fact(DisplayName = "Constructor with valid text creates ParagraphSection")]
    public void Constructor_ValidText_CreatesParagraphSection()
    {
        var section = new ParagraphSection("Paragraph text");

        Assert.Equal("Paragraph text", section.Text);
        Assert.Equal("Paragraph text", section.Content);
    }

    [Theory(DisplayName = "Constructor with null or whitespace text throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceText_ThrowsArgumentException(string? text)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ParagraphSection(text!));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact(DisplayName = "Content property returns same value as Text")]
    public void Content_ReturnsSameValueAsText()
    {
        var section = new ParagraphSection("Paragraph content");

        Assert.Equal(section.Text, section.Content);
    }

    [Fact(DisplayName = "Constructor supports multi-line text")]
    public void Constructor_MultiLineText_PreservesMultiLineText()
    {
        var text = "Line 1\nLine 2\nLine 3";
        var section = new ParagraphSection(text);

        Assert.Equal(text, section.Text);
        Assert.Contains("\n", section.Text);
    }

    [Fact(DisplayName = "Constructor preserves text with special characters")]
    public void Constructor_TextWithSpecialCharacters_PreservesText()
    {
        var text = "Text with special chars: @#$%^&*()";
        var section = new ParagraphSection(text);

        Assert.Equal(text, section.Text);
    }
}

public class HorizontalRuleSectionTests
{
    [Fact(DisplayName = "Constructor creates HorizontalRuleSection")]
    public void Constructor_CreatesHorizontalRuleSection()
    {
        var section = new HorizontalRuleSection();

        Assert.NotNull(section);
    }

    [Fact(DisplayName = "Content property returns empty string")]
    public void Content_ReturnsEmptyString()
    {
        var section = new HorizontalRuleSection();

        Assert.Equal(string.Empty, section.Content);
    }

    [Fact(DisplayName = "Multiple instances are independent")]
    public void MultipleInstances_AreIndependent()
    {
        var section1 = new HorizontalRuleSection();
        var section2 = new HorizontalRuleSection();

        Assert.NotSame(section1, section2);
    }
}

public class MetadataSectionTests
{
    [Fact(DisplayName = "Constructor with valid parameters creates MetadataSection")]
    public void Constructor_ValidParameters_CreatesMetadataSection()
    {
        var section = new MetadataSection("Label", "Value");

        Assert.Equal("Label", section.Label);
        Assert.Equal("Value", section.Value);
        Assert.Equal("Label: Value", section.Content);
    }

    [Theory(DisplayName = "Constructor with null or whitespace label throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceLabel_ThrowsArgumentException(string? label)
    {
        var exception = Assert.Throws<ArgumentException>(() => new MetadataSection(label!, "Value"));

        Assert.Equal("label", exception.ParamName);
    }

    [Theory(DisplayName = "Constructor with null or whitespace value throws ArgumentException")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespaceValue_ThrowsArgumentException(string? value)
    {
        var exception = Assert.Throws<ArgumentException>(() => new MetadataSection("Label", value!));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact(DisplayName = "Content property combines label and value")]
    public void Content_CombinesLabelAndValue()
    {
        var section = new MetadataSection("Author", "John Doe");

        Assert.Equal("Author: John Doe", section.Content);
    }

    [Fact(DisplayName = "Constructor preserves special characters in label and value")]
    public void Constructor_SpecialCharacters_PreservesContent()
    {
        var section = new MetadataSection("Label@#$", "Value%^&");

        Assert.Equal("Label@#$", section.Label);
        Assert.Equal("Value%^&", section.Value);
    }
}

public class DocumentSectionTests
{
    [Fact(DisplayName = "All section types inherit from DocumentSection")]
    public void AllSectionTypes_InheritFromDocumentSection()
    {
        Assert.IsAssignableFrom<DocumentSection>(new HeadingSection(1, "Text"));
        Assert.IsAssignableFrom<DocumentSection>(new BoldTextSection("Text"));
        Assert.IsAssignableFrom<DocumentSection>(new PlainTextSection("Text"));
        Assert.IsAssignableFrom<DocumentSection>(new ParagraphSection("Text"));
        Assert.IsAssignableFrom<DocumentSection>(new HorizontalRuleSection());
        Assert.IsAssignableFrom<DocumentSection>(new MetadataSection("Label", "Value"));
    }
}
