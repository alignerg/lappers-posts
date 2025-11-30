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
///     filePath: "path/to/chat.txt",
///     sender: "John Doe",
///     documentId: "abc123",
///     formatterType: MessageFormatType.Default);
/// await handler.HandleAsync(command);
/// </code>
/// </example>
public sealed record UploadToGoogleDocsCommand
{
    /// <summary>
    /// Gets the path to the chat export file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the sender name to filter messages by.
    /// </summary>
    /// <remarks>
    /// Only messages from this sender will be uploaded to the document.
    /// The matching is case-insensitive.
    /// </remarks>
    public string Sender { get; }

    /// <summary>
    /// Gets the Google Docs document identifier.
    /// </summary>
    public string DocumentId { get; }

    /// <summary>
    /// Gets the message formatting type to use.
    /// </summary>
    public MessageFormatType FormatterType { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadToGoogleDocsCommand"/> record.
    /// </summary>
    /// <param name="filePath">The path to the chat export file.</param>
    /// <param name="sender">The sender name to filter messages by.</param>
    /// <param name="documentId">The Google Docs document identifier.</param>
    /// <param name="formatterType">The message formatting type to use.</param>
    public UploadToGoogleDocsCommand(
        string filePath,
        string sender,
        string documentId,
        MessageFormatType formatterType = MessageFormatType.Default)
    {
        FilePath = filePath;
        Sender = sender;
        DocumentId = documentId;
        FormatterType = formatterType;
    }
}
