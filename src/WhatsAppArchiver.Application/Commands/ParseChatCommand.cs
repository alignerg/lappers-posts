namespace WhatsAppArchiver.Application.Commands;

/// <summary>
/// Command for parsing a WhatsApp chat export file.
/// </summary>
/// <remarks>
/// This command encapsulates the data needed to parse a chat export,
/// including the file path and an optional sender filter.
/// </remarks>
/// <example>
/// <code>
/// var command = new ParseChatCommand("path/to/chat.txt", "John Doe");
/// var result = await handler.HandleAsync(command);
/// </code>
/// </example>
/// <param name="FilePath">The path to the chat export file.</param>
/// <param name="SenderFilter">
/// Optional sender name to filter messages by.
/// When specified, only messages from this sender will be included
/// in the resulting chat export. The matching is case-insensitive.
/// </param>
public sealed record ParseChatCommand(
    string FilePath,
    string? SenderFilter = null
);
