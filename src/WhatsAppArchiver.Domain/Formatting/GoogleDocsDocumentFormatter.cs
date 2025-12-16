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
/// <description>Individual messages with bold timestamps in 24-hour format (HH:mm) followed by a plain text newline</description>
/// </item>
/// <item>
/// <description>Double empty lines separating messages for readability</description>
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
    /// <description>For each message, adds a bold timestamp (HH:mm) with newline, content, and double empty line separator</description>
    /// </item>
    /// </list>
    /// <para>
    /// Multi-line message content is preserved as-is, maintaining original line breaks.
    /// Empty exports (no messages) return an empty document.
    /// </para>
    /// </remarks>
    public GoogleDocsDocument FormatDocument(ChatExport chatExport)
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
            }
            isFirstDate = false;

            // Add H2 header for the date
            document.Add(new HeadingSection(2, dateGroup.Key.ToString("MMMM d, yyyy")));

            // Process each message in the date group
            foreach (var message in dateGroup.OrderBy(m => m.Timestamp))
            {
                // Add bold timestamp (24-hour format)
                document.Add(new BoldTextSection(message.Timestamp.ToString("HH:mm")));
                
                // Add newline as plain text to prevent bold bleeding
                document.Add(new PlainTextSection("\n"));

                // Add message content (preserve line breaks)
                document.Add(new ParagraphSection(message.Content));

                // Add separator (double empty lines)
                document.Add(new EmptyLineSection());
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
