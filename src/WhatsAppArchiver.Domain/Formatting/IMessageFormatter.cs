using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Defines a strategy for formatting chat messages.
/// </summary>
/// <remarks>
/// Implementations of this interface provide different formatting strategies
/// for displaying <see cref="ChatMessage"/> instances. This follows the Strategy pattern,
/// allowing the formatting behavior to be selected at runtime.
/// </remarks>
/// <example>
/// <code>
/// IMessageFormatter formatter = new DefaultMessageFormatter();
/// string formatted = formatter.FormatMessage(chatMessage);
/// </code>
/// </example>
public interface IMessageFormatter
{
    /// <summary>
    /// Formats a chat message according to the formatter's strategy.
    /// </summary>
    /// <param name="message">The chat message to format.</param>
    /// <returns>A formatted string representation of the message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    string FormatMessage(ChatMessage message);
}
