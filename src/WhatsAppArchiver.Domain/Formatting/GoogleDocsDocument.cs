namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Represents a structured document model for Google Docs with rich text styling.
/// </summary>
/// <remarks>
/// This class provides a structured representation of a document containing various
/// section types (headings, paragraphs, bold text, etc.) that can be formatted
/// for Google Docs. It maintains an ordered collection of document sections.
/// </remarks>
public sealed class GoogleDocsDocument
{
    private readonly List<DocumentSection> _sections = [];

    /// <summary>
    /// Gets the ordered list of content sections in this document.
    /// </summary>
    public IReadOnlyList<DocumentSection> Sections => _sections.AsReadOnly();

    /// <summary>
    /// Appends a section to the document.
    /// </summary>
    /// <param name="section">The section to add to the document.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="section"/> is null.</exception>
    public void Add(DocumentSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        _sections.Add(section);
    }

    /// <summary>
    /// Generates a plain text representation of the document for debugging and logging.
    /// </summary>
    /// <returns>A plain text string representing the entire document.</returns>
    /// <remarks>
    /// This method is intended for debugging and logging purposes. It provides a
    /// simple text representation without any formatting or styling information.
    /// </remarks>
    public string ToPlainText()
    {
        var plainTextLines = _sections.Select(section => section switch
        {
            HeadingSection heading => heading.Text,
            BoldTextSection bold => bold.Text,
            ParagraphSection paragraph => paragraph.Text,
            HorizontalRuleSection => "---",
            PageBreakSection => "[PAGE BREAK]",
            EmptyLineSection => "",
            MetadataSection metadata => $"{metadata.Label}: {metadata.Value}",
            _ => section.Content
        });

        return string.Join(Environment.NewLine, plainTextLines);
    }
}

/// <summary>
/// Abstract base class for all document sections.
/// </summary>
/// <remarks>
/// This abstract class serves as the base for all types of document sections,
/// providing a common interface for accessing section content.
/// </remarks>
public abstract class DocumentSection
{
    /// <summary>
    /// Gets the content of this section.
    /// </summary>
    public abstract string Content { get; }
}

/// <summary>
/// Represents a heading section with a specific heading level.
/// </summary>
/// <remarks>
/// Heading levels follow the standard H1-H6 pattern, where H1=1, H2=2, etc.
/// </remarks>
public sealed class HeadingSection : DocumentSection
{
    /// <summary>
    /// Gets the heading level (H1=1, H2=2, etc.).
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Gets the text content of the heading.
    /// </summary>
    public string Text { get; }

    /// <inheritdoc />
    public override string Content => Text;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeadingSection"/> class.
    /// </summary>
    /// <param name="level">The heading level (1-6).</param>
    /// <param name="text">The heading text.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="level"/> is not between 1 and 6.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is null or whitespace.</exception>
    public HeadingSection(int level, string text)
    {
        if (level < 1 || level > 6)
        {
            throw new ArgumentException("Heading level must be between 1 and 6.", nameof(level));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Heading text cannot be null or whitespace.", nameof(text));
        }

        Level = level;
        Text = text;
    }
}

/// <summary>
/// Represents a section of text with bold styling.
/// </summary>
public sealed class BoldTextSection : DocumentSection
{
    /// <summary>
    /// Gets the bold text content.
    /// </summary>
    public string Text { get; }

    /// <inheritdoc />
    public override string Content => Text;

    /// <summary>
    /// Initializes a new instance of the <see cref="BoldTextSection"/> class.
    /// </summary>
    /// <param name="text">The bold text content.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is null or whitespace.</exception>
    public BoldTextSection(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Bold text cannot be null or whitespace.", nameof(text));
        }

        Text = text;
    }
}

/// <summary>
/// Represents a paragraph section with optional multi-line support.
/// </summary>
public sealed class ParagraphSection : DocumentSection
{
    /// <summary>
    /// Gets the paragraph text content.
    /// </summary>
    public string Text { get; }

    /// <inheritdoc />
    public override string Content => Text;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParagraphSection"/> class.
    /// </summary>
    /// <param name="text">The paragraph text content.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="text"/> is null or whitespace.</exception>
    public ParagraphSection(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Paragraph text cannot be null or whitespace.", nameof(text));
        }

        Text = text;
    }
}

/// <summary>
/// Represents a horizontal rule section (visual separator).
/// </summary>
/// <remarks>
/// This section type has no content and represents a visual separator in the document.
/// </remarks>
public sealed class HorizontalRuleSection : DocumentSection
{
    /// <inheritdoc />
    public override string Content => string.Empty;
}

/// <summary>
/// Represents a page break section.
/// </summary>
/// <remarks>
/// This section type has no content and represents a page break in the document.
/// </remarks>
public sealed class PageBreakSection : DocumentSection
{
    /// <inheritdoc />
    public override string Content => string.Empty;
}

/// <summary>
/// Represents an empty line section for spacing.
/// </summary>
/// <remarks>
/// This section type represents an empty line in the document for visual spacing.
/// </remarks>
public sealed class EmptyLineSection : DocumentSection
{
    /// <inheritdoc />
    public override string Content => string.Empty;
}

/// <summary>
/// Represents a metadata section with a bold label and normal value.
/// </summary>
/// <remarks>
/// Metadata sections are typically used to display key-value pairs where
/// the label is displayed in bold and the value in normal text.
/// </remarks>
public sealed class MetadataSection : DocumentSection
{
    /// <summary>
    /// Gets the metadata label (displayed in bold).
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the metadata value (displayed in normal text).
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string Content => $"{Label}: {Value}";

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataSection"/> class.
    /// </summary>
    /// <param name="label">The metadata label.</param>
    /// <param name="value">The metadata value.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="label"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null or whitespace.</exception>
    public MetadataSection(string label, string value)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Metadata label cannot be null or whitespace.", nameof(label));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Metadata value cannot be null or whitespace.", nameof(value));
        }

        Label = label;
        Value = value;
    }
}
