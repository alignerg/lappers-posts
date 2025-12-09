using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Formatting;

namespace WhatsAppArchiver.Application.Commands;

/// <summary>
/// Command for uploading chat messages to a Google Docs document.
/// </summary>
/// <remarks>
/// This command encapsulates all data needed to parse, format, and upload
/// chat messages to a Google Docs document, including the target document ID,
/// sender filter, and formatting preferences.
/// To optimize performance and avoid double-parsing, a pre-parsed ChatExport
/// can be provided. If not provided, the handler will parse the file.
/// </remarks>
/// <example>
/// <code>
/// // Without pre-parsed export (handler will parse)
/// var command = new UploadToGoogleDocsCommand(
///     FilePath: "path/to/chat.txt",
///     Sender: "John Doe",
///     DocumentId: "abc123",
///     FormatterType: MessageFormatType.Default);
/// await handler.HandleAsync(command);
/// 
/// // With pre-parsed export (avoids double-parsing)
/// var chatExport = await parser.ParseAsync("path/to/chat.txt");
/// var commandWithCache = new UploadToGoogleDocsCommand(
///     FilePath: "path/to/chat.txt",
///     Sender: "John Doe",
///     DocumentId: "abc123",
///     FormatterType: MessageFormatType.Default,
///     CachedChatExport: chatExport);
/// await handler.HandleAsync(commandWithCache);
/// </code>
/// </example>
/// <param name="FilePath">The path to the chat export file.</param>
/// <param name="Sender">
/// The sender name to filter messages by.
/// Only messages from this sender will be uploaded to the document.
/// The matching is case-insensitive.
/// </param>
/// <param name="DocumentId">The Google Docs document identifier.</param>
/// <param name="FormatterType">The message formatting type to use.</param>
/// <param name="CachedChatExport">
/// Optional pre-parsed chat export to avoid re-parsing the file.
/// If provided, the handler will use this instead of parsing the file again.
/// </param>
public sealed record UploadToGoogleDocsCommand(
    string FilePath,
    string Sender,
    string DocumentId,
    MessageFormatType FormatterType = MessageFormatType.Default,
    ChatExport? CachedChatExport = null
);
