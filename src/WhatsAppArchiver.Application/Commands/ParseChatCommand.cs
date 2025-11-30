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
public sealed record ParseChatCommand
{
    /// <summary>
    /// Gets the path to the chat export file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets the optional sender name to filter messages by.
    /// </summary>
    /// <remarks>
    /// When specified, only messages from this sender will be included
    /// in the resulting chat export. The matching is case-insensitive.
    /// </remarks>
    public string? SenderFilter { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseChatCommand"/> record.
    /// </summary>
    /// <param name="filePath">The path to the chat export file.</param>
    /// <param name="senderFilter">Optional sender name to filter messages by.</param>
    public ParseChatCommand(string filePath, string? senderFilter = null)
    {
        FilePath = filePath;
        SenderFilter = senderFilter;
    }
}
