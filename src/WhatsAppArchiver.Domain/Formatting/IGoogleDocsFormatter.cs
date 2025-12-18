using WhatsAppArchiver.Domain.Aggregates;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Formats chat exports as structured Google Docs with rich text styling.
/// </summary>
/// <remarks>
/// This interface provides document-level formatting capabilities for processing
/// entire <see cref="ChatExport"/> aggregates into structured Google Docs documents.
/// Unlike message-level formatters, this formatter operates on the complete chat export
/// to produce a rich document with sections, headings, and metadata.
/// </remarks>
public interface IGoogleDocsFormatter
{
    /// <summary>
    /// Formats an entire chat export as a structured Google Docs document.
    /// </summary>
    /// <param name="chatExport">The chat export to format.</param>
    /// <param name="suppressTimestamps">
    /// When true, suppresses timestamp (Heading 3) entries in the generated document,
    /// allowing multiple posts on the same day to appear as consecutive paragraphs.
    /// Defaults to false to maintain backward compatibility.
    /// </param>
    /// <returns>A structured Google Docs document with rich text sections.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chatExport"/> is null.</exception>
    GoogleDocsDocument FormatDocument(ChatExport chatExport, bool suppressTimestamps = false);
}
