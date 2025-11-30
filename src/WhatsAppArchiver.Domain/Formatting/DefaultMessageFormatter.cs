using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Provides default formatting for chat messages.
/// </summary>
/// <remarks>
/// Formats messages in the standard pattern: [{timestamp}] {sender}: {content}
/// The timestamp is formatted using the round-trip date/time pattern (ISO 8601).
/// </remarks>
/// <example>
/// <code>
/// var formatter = new DefaultMessageFormatter();
/// var message = ChatMessage.Create(DateTimeOffset.UtcNow, "John", "Hello!");
/// string formatted = formatter.FormatMessage(message);
/// // Result: "[2024-01-15T10:30:00.0000000+00:00] John: Hello!"
/// </code>
/// </example>
public sealed class DefaultMessageFormatter : IMessageFormatter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public string FormatMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return $"[{message.Timestamp:O}] {message.Sender}: {message.Content}";
    }
}
