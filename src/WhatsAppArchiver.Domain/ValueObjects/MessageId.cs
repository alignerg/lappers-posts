using System.Security.Cryptography;
using System.Text;

namespace WhatsAppArchiver.Domain.ValueObjects;

/// <summary>
/// Represents a unique identifier for a chat message.
/// Composite value object consisting of a timestamp and content hash.
/// </summary>
/// <remarks>
/// This value object ensures message uniqueness by combining:
/// - The exact timestamp of the message
/// - A SHA256 hash of the message content
/// This combination provides a deterministic, immutable identifier.
/// </remarks>
public sealed record MessageId
{
    private const int HashDisplayLength = 8;

    /// <summary>
    /// Gets the timestamp component of the message identifier.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets the SHA256 hash of the message content.
    /// </summary>
    public string ContentHash { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageId"/> record.
    /// </summary>
    /// <param name="timestamp">The timestamp of the message.</param>
    /// <param name="contentHash">The SHA256 hash of the message content.</param>
    /// <exception cref="ArgumentException">Thrown when contentHash is null or whitespace.</exception>
    public MessageId(DateTimeOffset timestamp, string contentHash)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            throw new ArgumentException("Content hash cannot be null or whitespace.", nameof(contentHash));
        }

        Timestamp = timestamp;
        ContentHash = contentHash;
    }

    /// <summary>
    /// Creates a new MessageId from a timestamp and message content.
    /// </summary>
    /// <param name="timestamp">The timestamp of the message.</param>
    /// <param name="content">The content of the message to hash.</param>
    /// <returns>A new MessageId instance.</returns>
    /// <exception cref="ArgumentException">Thrown when content is null or whitespace.</exception>
    public static MessageId Create(DateTimeOffset timestamp, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
        }

        var hash = ComputeHash(content);

        return new MessageId(timestamp, hash);
    }

    /// <summary>
    /// Computes the SHA256 hash of the given content.
    /// </summary>
    /// <param name="content">The content to hash.</param>
    /// <returns>The hexadecimal representation of the hash.</returns>
    private static string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);

        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// Returns a string representation of the MessageId.
    /// </summary>
    /// <returns>A string combining the timestamp and content hash.</returns>
    public override string ToString() => $"{Timestamp:O}_{ContentHash[..HashDisplayLength]}";
}
