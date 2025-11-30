using WhatsAppArchiver.Application.Commands;
using WhatsAppArchiver.Application.Services;
using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Specifications;

namespace WhatsAppArchiver.Application.Handlers;

/// <summary>
/// Handles the <see cref="ParseChatCommand"/> by orchestrating chat parsing.
/// </summary>
/// <remarks>
/// This handler coordinates with the <see cref="IChatParser"/> to parse
/// WhatsApp chat export files and optionally filters messages by sender.
/// </remarks>
/// <example>
/// <code>
/// var handler = new ParseChatCommandHandler(chatParser);
/// var command = new ParseChatCommand("path/to/chat.txt", "John Doe");
/// var chatExport = await handler.HandleAsync(command);
/// </code>
/// </example>
public sealed class ParseChatCommandHandler
{
    private readonly IChatParser _chatParser;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseChatCommandHandler"/> class.
    /// </summary>
    /// <param name="chatParser">The chat parser service.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="chatParser"/> is null.</exception>
    public ParseChatCommandHandler(IChatParser chatParser)
    {
        ArgumentNullException.ThrowIfNull(chatParser);
        _chatParser = chatParser;
    }

    /// <summary>
    /// Handles the parse chat command asynchronously.
    /// </summary>
    /// <param name="command">The command containing parsing parameters.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The parsed <see cref="ChatExport"/> aggregate.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is null.</exception>
    public async Task<ChatExport> HandleAsync(
        ParseChatCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var chatExport = await _chatParser.ParseAsync(command.FilePath, cancellationToken);

        if (string.IsNullOrWhiteSpace(command.SenderFilter))
        {
            return chatExport;
        }

        var senderFilter = SenderFilter.Create(command.SenderFilter);
        var filteredMessages = chatExport.FilterMessages(senderFilter);

        return ChatExport.Create(filteredMessages, chatExport.Metadata);
    }
}
