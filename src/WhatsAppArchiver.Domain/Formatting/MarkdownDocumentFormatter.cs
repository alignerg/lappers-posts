using System.Text;
using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Formats chat exports as structured markdown documents with friendly date headers.
/// </summary>
/// <remarks>
/// <para>
/// This formatter implements <see cref="IDocumentFormatter"/> to process entire
/// <see cref="ChatExport"/> aggregates and produce a well-structured markdown document.
/// The output includes:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>H1 title with sender name extracted from the first message</description>
/// </item>
/// <item>
/// <description>Metadata section showing export date and total message count</description>
/// </item>
/// <item>
/// <description>Messages grouped by date with H2 headers in MMMM d, yyyy format</description>
/// </item>
/// <item>
/// <description>Individual messages with bold timestamps in 24-hour format (HH:mm)</description>
/// </item>
/// <item>
/// <description>Horizontal rules separating messages for readability</description>
/// </item>
/// </list>
/// <para>
/// This formatter is designed for batch processing and cannot format individual messages.
/// Attempting to call <see cref="FormatMessage"/> will result in a <see cref="NotSupportedException"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var formatter = new MarkdownDocumentFormatter();
/// var messages = new[]
/// {
///     ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "John Doe", "Hello!"),
///     ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 35, 0, TimeSpan.Zero), "John Doe", "How are you?"),
///     ChatMessage.Create(new DateTimeOffset(2024, 1, 16, 09, 15, 0, TimeSpan.Zero), "John Doe", "Good morning!")
/// };
/// var metadata = ParsingMetadata.Create("chat.txt", new DateTimeOffset(2024, 1, 17, 12, 0, 0, TimeSpan.Zero), 3, 3, 0);
/// var chatExport = ChatExport.Create(messages, metadata);
/// 
/// string markdown = formatter.FormatDocument(chatExport);
/// 
/// // Output:
/// // # WhatsApp Conversation Export - John Doe
/// // 
/// // **Export Date:** January 17, 2024
/// // **Total Messages:** 3
/// // 
/// // ---
/// // 
/// // ## January 15, 2024
/// // 
/// // **10:30**
/// // Hello!
/// // 
/// // ---
/// // 
/// // **10:35**
/// // How are you?
/// // 
/// // ---
/// // 
/// // ## January 16, 2024
/// // 
/// // **09:15**
/// // Good morning!
/// // 
/// // ---
/// </code>
/// </example>
public sealed class MarkdownDocumentFormatter : IDocumentFormatter, IMessageFormatter
{
    /// <summary>
    /// Formats an entire chat export as a structured markdown document.
    /// </summary>
    /// <param name="chatExport">The chat export aggregate to format.</param>
    /// <returns>A formatted markdown document string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chatExport"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The formatter processes messages in the following way:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>Extracts the sender name from the first message (if available)</description>
    /// </item>
    /// <item>
    /// <description>Creates a title section with H1 header and metadata</description>
    /// </item>
    /// <item>
    /// <description>Groups messages by date (using message.Timestamp.Date)</description>
    /// </item>
    /// <item>
    /// <description>For each date group, adds an H2 header with the date in MMMM d, yyyy format</description>
    /// </item>
    /// <item>
    /// <description>For each message, adds a bold timestamp (HH:mm), content, and separator</description>
    /// </item>
    /// </list>
    /// <para>
    /// Multi-line message content is preserved as-is, maintaining original line breaks.
    /// Empty exports (no messages) return a document with only the header and "Total Messages: 0".
    /// </para>
    /// </remarks>
    public string FormatDocument(ChatExport chatExport)
    {
        ArgumentNullException.ThrowIfNull(chatExport);

        var markdown = new StringBuilder();

        // Extract sender name from first message
        var senderName = chatExport.MessageCount > 0
            ? chatExport.Messages[0].Sender
            : "Unknown";

        // Build H1 title
        markdown.AppendLine($"# WhatsApp Conversation Export - {senderName}");
        markdown.AppendLine();

        // Add metadata lines
        markdown.AppendLine($"**Export Date:** {chatExport.Metadata.ParsedAt:MMMM d, yyyy}");
        markdown.AppendLine($"**Total Messages:** {chatExport.MessageCount}");
        markdown.AppendLine();

        // Add horizontal rule separator
        markdown.AppendLine("---");

        // Handle empty exports
        if (chatExport.MessageCount == 0)
        {
            return markdown.ToString();
        }

        // Group messages by date and order by date
        var messagesByDate = chatExport.Messages
            .GroupBy(m => m.Timestamp.Date)
            .OrderBy(g => g.Key);

        // Process each date group
        foreach (var dateGroup in messagesByDate)
        {
            markdown.AppendLine();

            // Add H2 header for the date
            markdown.AppendLine($"## {dateGroup.Key:MMMM d, yyyy}");

            // Process each message in the date group
            foreach (var message in dateGroup.OrderBy(m => m.Timestamp))
            {
                markdown.AppendLine();

                // Add bold timestamp (24-hour format)
                markdown.AppendLine($"**{message.Timestamp:HH:mm}**");

                // Add message content (preserve line breaks)
                markdown.AppendLine(message.Content);

                // Add separator
                markdown.AppendLine();
                markdown.AppendLine("---");
            }
        }

        return markdown.ToString();
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
    /// <see cref="FormatDocument"/> method. Use <see cref="IDocumentFormatter.FormatDocument"/>
    /// instead for proper batch processing with date grouping and document structure.
    /// </remarks>
    public string FormatMessage(ChatMessage message)
    {
        throw new NotSupportedException(
            "MarkdownDocumentFormatter requires FormatDocument method for batch processing. Use IDocumentFormatter.FormatDocument instead.");
    }
}
