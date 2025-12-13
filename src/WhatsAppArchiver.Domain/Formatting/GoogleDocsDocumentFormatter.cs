using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Formats chat exports as structured Google Docs with rich text styling.
/// </summary>
/// <remarks>
/// <para>
/// This formatter implements <see cref="IGoogleDocsFormatter"/> to process entire
/// <see cref="ChatExport"/> aggregates and produce a well-structured Google Docs document
/// with rich formatting including headings, bold text, and visual separators.
/// </para>
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
    public GoogleDocsDocument FormatDocument(ChatExport chatExport)
    {
        ArgumentNullException.ThrowIfNull(chatExport);

        var document = new GoogleDocsDocument();

        // Extract sender name from first message
        var senderName = chatExport.MessageCount > 0
            ? chatExport.Messages[0].Sender
            : "Unknown";

        // Add title heading
        document.Add(new HeadingSection(1, $"WhatsApp Conversation Export - {senderName}"));

        // Add metadata
        document.Add(new MetadataSection("Export Date", $"{chatExport.Metadata.ParsedAt:MMMM d, yyyy}"));
        document.Add(new MetadataSection("Total Messages", chatExport.MessageCount.ToString()));

        // Add separator
        document.Add(new HorizontalRuleSection());

        // Handle empty exports
        if (chatExport.MessageCount == 0)
        {
            return document;
        }

        // Group messages by date
        var messagesByDate = chatExport.Messages
            .GroupBy(m => m.Timestamp.Date)
            .OrderBy(g => g.Key);

        // Process each date group
        foreach (var dateGroup in messagesByDate)
        {
            // Add date heading
            document.Add(new HeadingSection(2, $"{dateGroup.Key:MMMM d, yyyy}"));

            // Process each message in the date group
            foreach (var message in dateGroup.OrderBy(m => m.Timestamp))
            {
                // Add bold timestamp
                document.Add(new BoldTextSection($"{message.Timestamp:HH:mm}"));

                // Add message content
                document.Add(new ParagraphSection(message.Content));

                // Add separator
                document.Add(new HorizontalRuleSection());
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
    /// instead for proper batch processing with rich text styling.
    /// </remarks>
    public string FormatMessage(ChatMessage message)
    {
        throw new NotSupportedException(
            "GoogleDocsDocumentFormatter requires FormatDocument method for batch processing. Use IGoogleDocsFormatter.FormatDocument instead.");
    }
}
