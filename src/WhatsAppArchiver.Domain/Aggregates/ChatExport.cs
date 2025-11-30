using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Specifications;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Aggregates;

/// <summary>
/// Aggregate root representing a parsed WhatsApp chat export.
/// </summary>
/// <remarks>
/// The ChatExport is the primary aggregate root in the domain, containing
/// a collection of chat messages and metadata about the parsing process.
/// This aggregate ensures consistency of the messages collection and
/// provides domain operations for querying and filtering messages.
/// </remarks>
public sealed class ChatExport
{
    private readonly List<ChatMessage> _messages;

    /// <summary>
    /// Gets the unique identifier of this chat export.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the collection of messages in this chat export.
    /// </summary>
    public IReadOnlyList<ChatMessage> Messages => _messages.AsReadOnly();

    /// <summary>
    /// Gets the metadata about the parsing process.
    /// </summary>
    public ParsingMetadata Metadata { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatExport"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for this export.</param>
    /// <param name="messages">The collection of chat messages.</param>
    /// <param name="metadata">The parsing metadata.</param>
    /// <exception cref="ArgumentNullException">Thrown when messages or metadata is null.</exception>
    public ChatExport(Guid id, IEnumerable<ChatMessage> messages, ParsingMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(metadata);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        }

        Id = id;
        _messages = [.. messages];
        Metadata = metadata;
    }

    /// <summary>
    /// Creates a new ChatExport with a generated identifier.
    /// </summary>
    /// <param name="messages">The collection of chat messages.</param>
    /// <param name="metadata">The parsing metadata.</param>
    /// <returns>A new ChatExport instance with a generated GUID.</returns>
    public static ChatExport Create(IEnumerable<ChatMessage> messages, ParsingMetadata metadata)
        => new(Guid.NewGuid(), messages, metadata);

    /// <summary>
    /// Filters messages using the specified specification.
    /// </summary>
    /// <param name="specification">The specification to filter messages by.</param>
    /// <returns>An enumerable of messages that satisfy the specification.</returns>
    /// <exception cref="ArgumentNullException">Thrown when specification is null.</exception>
    public IEnumerable<ChatMessage> FilterMessages(ISpecification<ChatMessage> specification)
    {
        ArgumentNullException.ThrowIfNull(specification);

        return _messages.Where(specification.IsSatisfiedBy);
    }

    /// <summary>
    /// Gets messages filtered by sender name (case-insensitive).
    /// </summary>
    /// <param name="senderName">The sender name to filter by.</param>
    /// <returns>An enumerable of messages from the specified sender.</returns>
    public IEnumerable<ChatMessage> GetMessagesBySender(string senderName)
    {
        var filter = SenderFilter.Create(senderName);

        return FilterMessages(filter);
    }

    /// <summary>
    /// Gets messages within the specified time range.
    /// </summary>
    /// <param name="start">The start of the time range (inclusive).</param>
    /// <param name="end">The end of the time range (inclusive).</param>
    /// <returns>An enumerable of messages within the time range.</returns>
    public IEnumerable<ChatMessage> GetMessagesInRange(DateTimeOffset start, DateTimeOffset end)
        => _messages.Where(m => m.Timestamp >= start && m.Timestamp <= end);

    /// <summary>
    /// Gets the count of messages in this export.
    /// </summary>
    public int MessageCount => _messages.Count;

    /// <summary>
    /// Gets the distinct senders in this chat export.
    /// </summary>
    /// <returns>An enumerable of distinct sender names.</returns>
    public IEnumerable<string> GetDistinctSenders()
        => _messages.Select(m => m.Sender).Distinct();
}
