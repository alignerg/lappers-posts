using System.Text;
using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Provides structured markdown document formatting for entire chat exports.
/// </summary>
/// <remarks>
/// <para>
/// This formatter implements <see cref="IDocumentFormatter"/> to generate a complete
/// markdown document from a <see cref="ChatExport"/> aggregate. The output includes:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>H1 title with sender name from the first message</description>
/// </item>
/// <item>
/// <description>Metadata section with export date and total message count</description>
/// </item>
/// <item>
/// <description>Date-grouped sections with friendly date headers (MMMM d, yyyy)</description>
/// </item>
/// <item>
/// <description>Individual timestamped posts with 24-hour format (HH:mm)</description>
/// </item>
/// <item>
/// <description>Horizontal rule separators between posts</description>
/// </item>
/// </list>
/// <para>
/// This formatter processes the entire chat export at once, enabling document-level
/// decisions such as grouping by date and generating aggregate metadata. It does not
/// support message-level formatting via <see cref="FormatMessage"/> - that method
/// throws <see cref="NotSupportedException"/> to enforce document-level processing.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var formatter = new MarkdownDocumentFormatter();
/// var chatExport = ChatExport.Create(messages, metadata);
/// string markdownDocument = formatter.FormatDocument(chatExport);
/// // Produces:
/// // # WhatsApp Conversation Export - John Doe
/// //
/// // **Export Date:** December 12, 2025
/// // **Total Messages:** 42
/// //
/// // ---
/// //
/// // ## December 12, 2025
/// //
/// // **10:30**
/// // Hello, world!
/// //
/// // ---
/// // ...
/// </code>
/// </example>
public sealed class MarkdownDocumentFormatter : IDocumentFormatter, IMessageFormatter
{
    /// <summary>
    /// Formats an entire chat export as a structured markdown document.
    /// </summary>
    /// <param name="chatExport">The chat export aggregate to format.</param>
    /// <returns>A structured markdown document with date headers and timestamped posts.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chatExport"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// The output structure includes:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <description>H1 header with "WhatsApp Conversation Export - {senderName}" where senderName
    /// comes from the first message. If the export is empty, uses "Unknown User".</description>
    /// </item>
    /// <item>
    /// <description>Metadata section with current export date (MMMM d, yyyy) and total message count.</description>
    /// </item>
    /// <item>
    /// <description>Messages grouped by date, with H2 headers in "MMMM d, yyyy" format.</description>
    /// </item>
    /// <item>
    /// <description>Each message formatted with bold timestamp (HH:mm) followed by content on next line.</description>
    /// </item>
    /// <item>
    /// <description>Horizontal rule (---) separator with blank line before it after each message.</description>
    /// </item>
    /// </list>
    /// <para>
    /// Multi-line message content is preserved as-is, maintaining original line breaks.
    /// Messages are sorted chronologically within each date group.
    /// </para>
    /// </remarks>
    public string FormatDocument(ChatExport chatExport)
    {
        ArgumentNullException.ThrowIfNull(chatExport);

        var markdown = new StringBuilder();

        // Extract sender name from first message, or use placeholder for empty exports
        var senderName = chatExport.Messages.Count > 0
            ? chatExport.Messages[0].Sender
            : "Unknown User";

        // H1 title
        markdown.AppendLine($"# WhatsApp Conversation Export - {senderName}");
        markdown.AppendLine();

        // Metadata section
        markdown.AppendLine($"**Export Date:** {DateTime.Now:MMMM d, yyyy}");
        markdown.AppendLine($"**Total Messages:** {chatExport.MessageCount}");
        markdown.AppendLine();

        // Horizontal rule separator after metadata
        markdown.AppendLine("---");

        // Group messages by date and format each group
        if (chatExport.Messages.Count > 0)
        {
            var messagesByDate = chatExport.Messages
                .GroupBy(m => m.Timestamp.Date)
                .OrderBy(g => g.Key);

            foreach (var dateGroup in messagesByDate)
            {
                markdown.AppendLine();

                // H2 date header with friendly format
                markdown.AppendLine($"## {dateGroup.Key:MMMM d, yyyy}");
                markdown.AppendLine();

                // Format each message in the date group
                foreach (var message in dateGroup.OrderBy(m => m.Timestamp))
                {
                    // Bold timestamp in 24-hour format
                    markdown.AppendLine($"**{message.Timestamp:HH:mm}**");

                    // Message content (preserves multi-line content)
                    markdown.AppendLine(message.Content);

                    // Horizontal rule separator with blank line before
                    markdown.AppendLine();
                    markdown.AppendLine("---");
                }
            }
        }

        return markdown.ToString();
    }

    /// <summary>
    /// Not supported for document-level formatters.
    /// </summary>
    /// <param name="message">The chat message.</param>
    /// <returns>This method always throws an exception.</returns>
    /// <exception cref="NotSupportedException">Always thrown to enforce document-level processing.</exception>
    /// <remarks>
    /// <see cref="MarkdownDocumentFormatter"/> is a document-level formatter that requires
    /// access to the entire <see cref="ChatExport"/> aggregate to generate properly structured
    /// output with date grouping and document metadata. Use <see cref="FormatDocument"/> instead.
    /// </remarks>
    public string FormatMessage(ChatMessage message)
    {
        throw new NotSupportedException(
            "MarkdownDocumentFormatter requires FormatDocument method for batch processing. " +
            "Use IDocumentFormatter.FormatDocument instead.");
    }
}
