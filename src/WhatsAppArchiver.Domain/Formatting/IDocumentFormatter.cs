using WhatsAppArchiver.Domain.Aggregates;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Defines a strategy for formatting entire chat export documents.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides document-level formatting capabilities for processing
/// entire <see cref="ChatExport"/> aggregates at once. There are two types of formatters in the system:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Document-level formatters</strong>: Implement <see cref="IDocumentFormatter"/> to process
/// the entire <see cref="ChatExport"/> aggregate at once, allowing for global formatting decisions,
/// document structure, and aggregate-level operations.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Message-level formatters</strong>: Implement <see cref="IMessageFormatter"/> to process
/// one message at a time, focusing on individual message formatting without knowledge of the full document.
/// </description>
/// </item>
/// </list>
/// <para>
/// Document-level formatters are useful when the output format requires knowledge of the entire
/// chat history, such as generating structured documents, creating indexes, or applying
/// aggregate-level transformations. If individual message formatting is also needed,
/// document formatters can compose a message formatter internally.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using System.Text;
/// 
/// // Document-level formatter implementation
/// public class HtmlDocumentFormatter : IDocumentFormatter
/// {
///     private readonly IMessageFormatter _messageFormatter;
///     
///     public HtmlDocumentFormatter(IMessageFormatter messageFormatter)
///     {
///         _messageFormatter = messageFormatter;
///     }
///     
///     public string FormatDocument(ChatExport chatExport)
///     {
///         var html = new StringBuilder();
///         html.Append("&lt;html&gt;&lt;body&gt;");
///         
///         foreach (var message in chatExport.Messages)
///         {
///             var formattedMessage = _messageFormatter.FormatMessage(message);
///             html.AppendFormat("&lt;p&gt;{0}&lt;/p&gt;", formattedMessage);
///         }
///         
///         html.Append("&lt;/body&gt;&lt;/html&gt;");
///         return html.ToString();
///     }
/// }
/// 
/// // Usage
/// IMessageFormatter messageFormatter = new DefaultMessageFormatter();
/// IDocumentFormatter formatter = new HtmlDocumentFormatter(messageFormatter);
/// string document = formatter.FormatDocument(chatExport);
/// </code>
/// </example>
public interface IDocumentFormatter
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
