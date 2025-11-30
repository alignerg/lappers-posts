using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Provides default formatting for chat messages.
/// </summary>
/// <remarks>
/// Formats messages in the standard pattern: [{timestamp}] {sender}: {content}
/// The timestamp is formatted using the DD/MM/YYYY, HH:mm:ss pattern.
/// </remarks>
/// <example>
/// <code>
/// var formatter = new DefaultMessageFormatter();
/// var message = ChatMessage.Create(DateTimeOffset.UtcNow, "John", "Hello!");
/// string formatted = formatter.FormatMessage(message);
/// // Result: "[15/01/2024, 10:30:00] John: Hello!"
/// </code>
/// </example>
public sealed class DefaultMessageFormatter : IMessageFormatter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public string FormatMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return $"[{message.Timestamp:dd/MM/yyyy, HH:mm:ss}] {message.Sender}: {message.Content}";
    }
}
