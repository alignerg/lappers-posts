using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Provides verbose formatting for chat messages with detailed metadata.
/// </summary>
/// <remarks>
/// Formats messages with comprehensive date, time, and sender information.
/// The format includes the date in DD/MM/YYYY format, time in HH:mm:ss format,
/// and sender details on separate lines for maximum readability.
/// </remarks>
/// <example>
/// <code>
/// var formatter = new VerboseMessageFormatter();
/// var message = ChatMessage.Create(DateTimeOffset.UtcNow, "John Doe", "Hello!");
/// string formatted = formatter.FormatMessage(message);
/// // Result:
/// // Date: 15/01/2024
/// // Time: 10:30:00
/// // From: John Doe
/// // Message: Hello!
/// </code>
/// </example>
public sealed class VerboseMessageFormatter : IMessageFormatter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public string FormatMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return $"""
            Date: {message.Timestamp:dd/MM/yyyy}
            Time: {message.Timestamp:HH:mm:ss}
            From: {message.Sender}
            Message: {message.Content}
            """;
    }
}
