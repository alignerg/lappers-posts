using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Provides compact formatting for chat messages without timestamp.
/// </summary>
/// <remarks>
/// Formats messages in a compact pattern: {sender}: {content}
/// This format is useful when timestamps are not needed or are displayed elsewhere.
/// </remarks>
/// <example>
/// <code>
/// var formatter = new CompactMessageFormatter();
/// var message = ChatMessage.Create(DateTimeOffset.UtcNow, "John", "Hello!");
/// string formatted = formatter.FormatMessage(message);
/// // Result: "John: Hello!"
/// </code>
/// </example>
public sealed class CompactMessageFormatter : IMessageFormatter
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public string FormatMessage(ChatMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return $"{message.Sender}: {message.Content}";
    }
}
