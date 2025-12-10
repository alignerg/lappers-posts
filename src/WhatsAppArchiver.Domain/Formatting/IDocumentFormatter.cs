using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Defines a strategy for formatting entire chat export documents.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="IMessageFormatter"/> to provide document-level formatting
/// capabilities. There are two types of formatters in the system:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Document-level formatters</strong>: Process the entire <see cref="ChatExport"/> aggregate
/// at once, allowing for global formatting decisions, document structure, and aggregate-level operations.
/// These formatters should implement <see cref="FormatDocument"/> and throw <see cref="NotSupportedException"/>
/// from <see cref="IMessageFormatter.FormatMessage"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Message-level formatters</strong>: Process one <see cref="ChatMessage"/> at a time,
/// focusing on individual message formatting. These formatters only implement
/// <see cref="IMessageFormatter.FormatMessage"/> and do not need to implement this interface.
/// </description>
/// </item>
/// </list>
/// <para>
/// Document-level formatters are useful when the output format requires knowledge of the entire
/// chat history, such as generating structured documents, creating indexes, or applying
/// aggregate-level transformations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Document-level formatter implementation
/// public class HtmlDocumentFormatter : IDocumentFormatter
/// {
///     public string FormatDocument(ChatExport chatExport)
///     {
///         var html = new StringBuilder();
///         html.Append("&lt;html&gt;&lt;body&gt;");
///         
///         foreach (var message in chatExport.Messages)
///         {
///             html.AppendFormat("&lt;p&gt;{0}: {1}&lt;/p&gt;", message.Sender, message.Content);
///         }
///         
///         html.Append("&lt;/body&gt;&lt;/html&gt;");
///         return html.ToString();
///     }
///     
///     public string FormatMessage(ChatMessage message)
///     {
///         throw new NotSupportedException(
///             "Document-level formatters do not support individual message formatting.");
///     }
/// }
/// 
/// // Usage
/// IDocumentFormatter formatter = new HtmlDocumentFormatter();
/// string document = formatter.FormatDocument(chatExport);
/// </code>
/// </example>
public interface IDocumentFormatter : IMessageFormatter
{
    /// <summary>
    /// Formats an entire chat export document according to the formatter's strategy.
    /// </summary>
    /// <param name="chatExport">The chat export aggregate to format.</param>
    /// <returns>A formatted string representation of the entire document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chatExport"/> is null.</exception>
    /// <remarks>
    /// Implementations should process the entire <see cref="ChatExport"/> aggregate,
    /// including all messages and metadata, to produce a complete formatted document.
    /// This method has access to the full aggregate boundary and can make decisions
    /// based on the complete chat history.
    /// </remarks>
    string FormatDocument(ChatExport chatExport);
}
