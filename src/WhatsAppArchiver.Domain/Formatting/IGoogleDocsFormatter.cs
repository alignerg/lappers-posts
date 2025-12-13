using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Formats chat exports as structured Google Docs with rich text styling.
/// </summary>
/// <remarks>
/// This interface extends <see cref="IMessageFormatter"/> but provides document-level
/// formatting capabilities through the <see cref="FormatDocument"/> method.
/// The inherited <see cref="IMessageFormatter.FormatMessage"/> method is not supported
/// and will throw a <see cref="NotSupportedException"/> when called.
/// </remarks>
public interface IGoogleDocsFormatter : IMessageFormatter
{
    /// <summary>
    /// Formats an entire chat export as a structured Google Docs document.
    /// </summary>
    /// <param name="chatExport">The chat export to format.</param>
    /// <returns>A structured Google Docs document with rich text sections.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chatExport"/> is null.</exception>
    GoogleDocsDocument FormatDocument(ChatExport chatExport);

    /// <summary>
    /// Formats a single chat message.
    /// </summary>
    /// <param name="message">The chat message to format.</param>
    /// <returns>This method is not supported for Google Docs formatting.</returns>
    /// <exception cref="NotSupportedException">Always thrown as message-level formatting is not supported.</exception>
    /// <remarks>
    /// Google Docs formatting requires document-level context. Use <see cref="FormatDocument"/>
    /// instead to format entire chat exports.
    /// </remarks>
    string IMessageFormatter.FormatMessage(ChatMessage message)
    {
        throw new NotSupportedException(
            "Message-level formatting is not supported for Google Docs. Use FormatDocument instead.");
    }
}
