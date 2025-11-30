using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Entities;

/// <summary>
/// Represents a single message in a WhatsApp chat export.
/// </summary>
/// <remarks>
/// This entity is immutable and represents a parsed message from a chat export.
/// The Id is generated from the message's timestamp and content to ensure uniqueness.
/// </remarks>
public sealed record ChatMessage
{
    /// <summary>
    /// Gets the unique identifier for this message.
    /// </summary>
    public MessageId Id { get; }

    /// <summary>
    /// Gets the timestamp when the message was sent.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the sender of the message.
    /// </summary>
    public string Sender { get; }

    /// <summary>
    /// Gets the content of the message.
    /// </summary>
    public string Content { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatMessage"/> record.
    /// </summary>
    /// <param name="timestamp">The timestamp when the message was sent.</param>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="content">The content of the message.</param>
    /// <exception cref="ArgumentException">Thrown when sender is null or whitespace.</exception>
    /// <exception cref="ArgumentException">Thrown when content is null or whitespace.</exception>
    public ChatMessage(DateTimeOffset timestamp, string sender, string content)
    {
        if (string.IsNullOrWhiteSpace(sender))
        {
            throw new ArgumentException("Sender cannot be null or whitespace.", nameof(sender));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
        }

        Timestamp = timestamp;
        Sender = sender;
        Content = content;
        Id = MessageId.Create(timestamp, content);
    }

    /// <summary>
    /// Creates a new ChatMessage with the specified parameters.
    /// </summary>
    /// <param name="timestamp">The timestamp when the message was sent.</param>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="content">The content of the message.</param>
    /// <returns>A new ChatMessage instance.</returns>
    public static ChatMessage Create(DateTimeOffset timestamp, string sender, string content)
        => new(timestamp, sender, content);
}
