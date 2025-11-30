using WhatsAppArchiver.Domain.Specifications;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Entities;

/// <summary>
/// Represents a checkpoint for tracking message processing state.
/// </summary>
/// <remarks>
/// This entity captures the state of message processing, including which
/// messages have been processed and any active sender filter. This enables
/// resumption of processing from the last known state and prevents
/// duplicate processing of messages.
/// </remarks>
public sealed class ProcessingCheckpoint
{
    private readonly HashSet<MessageId> _processedMessageIds;

    /// <summary>
    /// Gets the unique identifier of this checkpoint.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the document identifier associated with this checkpoint.
    /// </summary>
    public string DocumentId { get; }

    /// <summary>
    /// Gets the timestamp of the last processed message.
    /// </summary>
    public DateTimeOffset? LastProcessedTimestamp { get; private set; }

    /// <summary>
    /// Gets the set of processed message identifiers.
    /// </summary>
    public IReadOnlySet<MessageId> ProcessedMessageIds => _processedMessageIds;

    /// <summary>
    /// Gets the optional sender filter for this checkpoint.
    /// </summary>
    public SenderFilter? SenderFilter { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessingCheckpoint"/> class.
    /// </summary>
    /// <param name="id">The unique identifier for this checkpoint.</param>
    /// <param name="documentId">The document identifier associated with this checkpoint.</param>
    /// <param name="lastProcessedTimestamp">The timestamp of the last processed message.</param>
    /// <param name="processedMessageIds">The set of already processed message identifiers.</param>
    /// <param name="senderFilter">The optional sender filter.</param>
    /// <exception cref="ArgumentException">Thrown when documentId is null or whitespace.</exception>
    /// <exception cref="ArgumentException">Thrown when id is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when processedMessageIds is null.</exception>
    public ProcessingCheckpoint(
        Guid id,
        string documentId,
        DateTimeOffset? lastProcessedTimestamp,
        IEnumerable<MessageId> processedMessageIds,
        SenderFilter? senderFilter = null)
    {
        ArgumentNullException.ThrowIfNull(processedMessageIds);

        if (id == Guid.Empty)
        {
            throw new ArgumentException("Id cannot be empty.", nameof(id));
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document ID cannot be null or whitespace.", nameof(documentId));
        }

        Id = id;
        DocumentId = documentId;
        LastProcessedTimestamp = lastProcessedTimestamp;
        _processedMessageIds = [.. processedMessageIds];
        SenderFilter = senderFilter;
    }

    /// <summary>
    /// Creates a new ProcessingCheckpoint with a generated identifier.
    /// </summary>
    /// <param name="documentId">The document identifier associated with this checkpoint.</param>
    /// <param name="senderFilter">The optional sender filter.</param>
    /// <returns>A new ProcessingCheckpoint instance with a generated GUID.</returns>
    public static ProcessingCheckpoint Create(string documentId, SenderFilter? senderFilter = null)
        => new(Guid.NewGuid(), documentId, null, [], senderFilter);

    /// <summary>
    /// Marks a message as processed and updates the last processed timestamp.
    /// </summary>
    /// <param name="messageId">The identifier of the processed message.</param>
    /// <exception cref="ArgumentNullException">Thrown when messageId is null.</exception>
    public void MarkAsProcessed(MessageId messageId)
    {
        ArgumentNullException.ThrowIfNull(messageId);

        _processedMessageIds.Add(messageId);

        if (LastProcessedTimestamp is null || messageId.Timestamp > LastProcessedTimestamp)
        {
            LastProcessedTimestamp = messageId.Timestamp;
        }
    }

    /// <summary>
    /// Determines whether the specified message has already been processed.
    /// </summary>
    /// <param name="messageId">The message identifier to check.</param>
    /// <returns>True if the message has been processed; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown when messageId is null.</exception>
    public bool HasBeenProcessed(MessageId messageId)
    {
        ArgumentNullException.ThrowIfNull(messageId);

        return _processedMessageIds.Contains(messageId);
    }

    /// <summary>
    /// Gets the count of processed messages.
    /// </summary>
    public int ProcessedCount => _processedMessageIds.Count;
}
