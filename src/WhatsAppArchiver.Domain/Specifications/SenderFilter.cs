using WhatsAppArchiver.Domain.Entities;

namespace WhatsAppArchiver.Domain.Specifications;

/// <summary>
/// Specification for filtering chat messages by sender name using case-insensitive matching.
/// </summary>
/// <remarks>
/// This specification implements the Specification pattern to encapsulate the
/// business rule of filtering messages by sender. The matching is case-insensitive
/// to provide a user-friendly filtering experience.
/// </remarks>
public sealed class SenderFilter : ISpecification<ChatMessage>
{
    /// <summary>
    /// Gets the sender name to filter by.
    /// </summary>
    public string SenderName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SenderFilter"/> class.
    /// </summary>
    /// <param name="senderName">The sender name to filter by.</param>
    /// <exception cref="ArgumentException">Thrown when senderName is null or whitespace.</exception>
    public SenderFilter(string senderName)
    {
        if (string.IsNullOrWhiteSpace(senderName))
        {
            throw new ArgumentException("Sender name cannot be null or whitespace.", nameof(senderName));
        }

        SenderName = senderName;
    }

    /// <summary>
    /// Determines whether the specified message matches the sender filter.
    /// </summary>
    /// <param name="candidate">The chat message to evaluate.</param>
    /// <returns>True if the message sender matches the filter (case-insensitive); otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when candidate is null.</exception>
    public bool IsSatisfiedBy(ChatMessage candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return string.Equals(candidate.Sender, SenderName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Creates a new SenderFilter with the specified sender name.
    /// </summary>
    /// <param name="senderName">The sender name to filter by.</param>
    /// <returns>A new SenderFilter instance.</returns>
    public static SenderFilter Create(string senderName) => new(senderName);
}
