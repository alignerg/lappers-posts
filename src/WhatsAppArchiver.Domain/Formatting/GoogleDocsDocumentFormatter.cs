using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Formats chat exports as structured Google Docs documents with rich text sections.
/// </summary>
/// <remarks>
/// <para>
/// This formatter implements <see cref="IGoogleDocsFormatter"/> to process entire
/// <see cref="ChatExport"/> aggregates and produce a structured Google Docs document.
/// The output includes:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>Messages grouped by date with H2 headers in MMMM d, yyyy format</description>
/// </item>
/// <item>
/// <description>Page breaks between date sections for improved navigation</description>
/// </item>
/// <item>
/// <description>Individual messages with H3 timestamp headers in 24-hour format (HH:mm)</description>
/// </item>
/// <item>
/// <description>Single empty line separating messages for readability</description>
/// </item>
/// </list>
/// <para>
/// This formatter is designed for batch processing and cannot format individual messages.
/// Attempting to call <see cref="FormatMessage"/> will result in a <see cref="NotSupportedException"/>.
/// </para>
/// </remarks>
public sealed class GoogleDocsDocumentFormatter : IGoogleDocsFormatter, IMessageFormatter
{
    /// <summary>
    /// Formats an entire chat export as a structured Google Docs document.
    /// </summary>
    /// <param name="chatExport">The chat export to format.</param>
    /// <param name="suppressTimestamps">
    /// When true, suppresses timestamp (Heading 3) entries in the generated document,
    /// allowing multiple posts on the same day to appear as consecutive paragraphs.
    /// </param>
    /// <returns>A structured Google Docs document with rich text sections.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chatExport"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The formatter processes messages in the following way:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>Groups messages by date (using message.Timestamp.Date)</description>
    /// </item>
    /// <item>
    /// <description>For each date group, adds a page break (except first), then an H2 header with the date in MMMM d, yyyy format</description>
    /// </item>
    /// <item>
    /// <description>For each message, adds an H3 timestamp header (HH:mm) unless suppressTimestamps is true, content, and single empty line separator</description>
    /// </item>
    /// </list>
    /// <para>
    /// Multi-line message content is preserved as-is, maintaining original line breaks.
    /// Empty exports (no messages) return an empty document.
    /// </para>
    /// </remarks>
    public GoogleDocsDocument FormatDocument(ChatExport chatExport, bool suppressTimestamps = false)
    {
        ArgumentNullException.ThrowIfNull(chatExport);

        var document = new GoogleDocsDocument();

        // Handle empty exports
        if (chatExport.MessageCount == 0)
        {
            return document;
        }

        // Group messages by date and order by date
        var messagesByDate = chatExport.Messages
            .GroupBy(m => m.Timestamp.Date)
            .OrderBy(g => g.Key);

        var isFirstDate = true;

        // Process each date group
        foreach (var dateGroup in messagesByDate)
        {
            // Add page break before each date (except the first one)
            if (!isFirstDate)
            {
                document.Add(new PageBreakSection());
                // Add empty line after page break to separate it from the heading
                // This ensures the heading anchor is positioned at the heading text, not at the page break
                document.Add(new EmptyLineSection());
            }
            isFirstDate = false;

            // Add H2 header for the date
            document.Add(new HeadingSection(2, dateGroup.Key.ToString("MMMM d, yyyy")));

            // Process each message in the date group
            foreach (var message in dateGroup.OrderBy(m => m.Timestamp))
            {
                // Add timestamp as H3 heading (24-hour format) unless suppressed
                if (!suppressTimestamps)
                {
                    document.Add(new HeadingSection(3, message.Timestamp.ToString("HH:mm")));
                }

                // Add message content (preserve line breaks)
                document.Add(new ParagraphSection(message.Content));

                // Add separator (single empty line)
                document.Add(new EmptyLineSection());
            }
        }

        return document;
    }

    /// <summary>
    /// Throws <see cref="NotSupportedException"/> as this formatter requires batch processing.
    /// </summary>
    /// <param name="message">The chat message (not used).</param>
    /// <returns>This method never returns; it always throws an exception.</returns>
    /// <exception cref="NotSupportedException">
    /// Always thrown to indicate that individual message formatting is not supported.
    /// </exception>
    /// <remarks>
    /// This formatter is designed to process entire chat exports at once using the
    /// <see cref="FormatDocument"/> method. Use <see cref="IGoogleDocsFormatter.FormatDocument"/>
    /// instead for proper batch processing with date grouping and document structure.
    /// </remarks>
    public string FormatMessage(ChatMessage message)
    {
        throw new NotSupportedException(
            "GoogleDocsDocumentFormatter requires FormatDocument for batch processing");
    }
}
