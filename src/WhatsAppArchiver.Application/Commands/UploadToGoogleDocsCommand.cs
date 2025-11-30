using WhatsAppArchiver.Domain.Formatting;

namespace WhatsAppArchiver.Application.Commands;

/// <summary>
/// Command for uploading chat messages to a Google Docs document.
/// </summary>
/// <remarks>
/// This command encapsulates all data needed to parse, format, and upload
/// chat messages to a Google Docs document, including the target document ID,
/// sender filter, and formatting preferences.
/// </remarks>
/// <example>
/// <code>
/// var command = new UploadToGoogleDocsCommand(
///     FilePath: "path/to/chat.txt",
///     Sender: "John Doe",
///     DocumentId: "abc123",
///     FormatterType: MessageFormatType.Default);
/// await handler.HandleAsync(command);
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
public sealed record UploadToGoogleDocsCommand(
    string FilePath,
    string Sender,
    string DocumentId,
    MessageFormatType FormatterType = MessageFormatType.Default
);
