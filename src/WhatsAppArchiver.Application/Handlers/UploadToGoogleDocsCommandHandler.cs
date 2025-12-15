using System.Text;
using WhatsAppArchiver.Application.Commands;
using WhatsAppArchiver.Application.Services;
using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Domain.Specifications;

namespace WhatsAppArchiver.Application.Handlers;

/// <summary>
/// Handles the <see cref="UploadToGoogleDocsCommand"/> by coordinating chat parsing,
/// formatting, and uploading to Google Docs.
/// </summary>
/// <remarks>
/// This handler orchestrates the complete workflow of:
/// <list type="number">
/// <item>Parsing the chat export file</item>
/// <item>Filtering messages by sender</item>
/// <item>Retrieving/creating a processing checkpoint</item>
/// <item>Formatting and uploading only unprocessed messages</item>
/// <item>Updating the checkpoint transactionally</item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// var handler = new UploadToGoogleDocsCommandHandler(
///     chatParser, googleDocsService, processingStateService);
/// var command = new UploadToGoogleDocsCommand(
///     "path/to/chat.txt", "John Doe", "doc-123", MessageFormatType.Default);
/// var count = await handler.HandleAsync(command);
/// Console.WriteLine($"Uploaded {count} messages");
/// </code>
/// </example>
public sealed class UploadToGoogleDocsCommandHandler
{
    private readonly IChatParser _chatParser;
    private readonly IGoogleDocsService _googleDocsService;
    private readonly IProcessingStateService _processingStateService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadToGoogleDocsCommandHandler"/> class.
    /// </summary>
    /// <param name="chatParser">The chat parser service.</param>
    /// <param name="googleDocsService">The Google Docs service.</param>
    /// <param name="processingStateService">The processing state service.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when any of the parameters is null.
    /// </exception>
    public UploadToGoogleDocsCommandHandler(
        IChatParser chatParser,
        IGoogleDocsService googleDocsService,
        IProcessingStateService processingStateService)
    {
        ArgumentNullException.ThrowIfNull(chatParser);
        ArgumentNullException.ThrowIfNull(googleDocsService);
        ArgumentNullException.ThrowIfNull(processingStateService);

        _chatParser = chatParser;
        _googleDocsService = googleDocsService;
        _processingStateService = processingStateService;
    }

    /// <summary>
    /// Handles the upload to Google Docs command asynchronously.
    /// </summary>
    /// <param name="command">The command containing upload parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The number of messages uploaded.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// If the command contains a cached ChatExport, it will be used directly to avoid re-parsing.
    /// Otherwise, the file will be parsed using the chat parser service.
    /// </para>
    /// <para>
    /// The handler supports two formatting strategies:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <strong>Rich Google Docs formatting</strong>: For formatters implementing <see cref="IGoogleDocsFormatter"/>,
    /// processes the entire export at once to create structured documents with headings, metadata, and rich text.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <strong>Message-level formatting</strong>: For standard <see cref="IMessageFormatter"/> implementations,
    /// processes messages individually for simple formatting scenarios.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public async Task<int> HandleAsync(
        UploadToGoogleDocsCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var chatExport = command.CachedChatExport
            ?? await _chatParser.ParseAsync(command.FilePath, timeZoneOffset: null, cancellationToken);

        var senderFilter = SenderFilter.Create(command.Sender);
        var filteredMessages = chatExport.FilterMessages(senderFilter).ToList();

        var checkpoint = await _processingStateService.GetCheckpointAsync(
            command.DocumentId,
            senderFilter,
            cancellationToken);

        var unprocessedMessages = filteredMessages
            .Where(m => !checkpoint.HasBeenProcessed(m.Id))
            .OrderBy(m => m.Timestamp)
            .ToList();

        if (unprocessedMessages.Count == 0)
        {
            return 0;
        }

        var formatter = FormatterFactory.Create(command.FormatterType);

        if (formatter is IGoogleDocsFormatter googleDocsFormatter)
        {
            // Rich Google Docs formatting
            var exportToFormat = ChatExport.Create(unprocessedMessages, chatExport.Metadata);
            var richDocument = googleDocsFormatter.FormatDocument(exportToFormat);
            await _googleDocsService.AppendRichAsync(command.DocumentId, richDocument, cancellationToken);
        }
        else
        {
            // Message-level formatting
            var content = FormatMessages(unprocessedMessages, formatter);
            await _googleDocsService.AppendAsync(command.DocumentId, content, cancellationToken);
        }

        foreach (var message in unprocessedMessages)
        {
            checkpoint.MarkAsProcessed(message.Id);
        }

        await _processingStateService.SaveCheckpointAsync(checkpoint, cancellationToken);

        return unprocessedMessages.Count;
    }

    /// <summary>
    /// Formats a collection of messages using the specified formatter.
    /// </summary>
    /// <param name="messages">The messages to format.</param>
    /// <param name="formatter">The formatter to use.</param>
    /// <returns>The formatted content as a single string.</returns>
    private static string FormatMessages(
        IEnumerable<ChatMessage> messages,
        IMessageFormatter formatter)
    {
        var builder = new StringBuilder();

        foreach (var message in messages)
        {
            builder.AppendLine(formatter.FormatMessage(message));
        }

        return builder.ToString();
    }
}
