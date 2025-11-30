using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Specifications;

namespace WhatsAppArchiver.Application.Services;

/// <summary>
/// Defines operations for managing processing state and checkpoints.
/// </summary>
/// <remarks>
/// This interface abstracts the persistence of processing state,
/// enabling resumable processing and preventing duplicate message handling.
/// Implementations may use various storage backends such as JSON files,
/// databases, or cloud storage.
/// </remarks>
/// <example>
/// <code>
/// var checkpoint = await processingStateService.GetCheckpointAsync("doc-123", senderFilter);
/// // Process messages...
/// await processingStateService.SaveCheckpointAsync(checkpoint);
/// </code>
/// </example>
public interface IProcessingStateService
{
    /// <summary>
    /// Retrieves the processing checkpoint for the specified document and sender.
    /// </summary>
    /// <param name="documentId">The unique identifier of the document being processed.</param>
    /// <param name="senderFilter">Optional sender filter to scope the checkpoint.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// The existing checkpoint if found, or a new checkpoint if none exists for the given parameters.
    /// </returns>
    Task<ProcessingCheckpoint> GetCheckpointAsync(
        string documentId,
        SenderFilter? senderFilter = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists the processing checkpoint state.
    /// </summary>
    /// <param name="checkpoint">The checkpoint to persist.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous save operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="checkpoint"/> is null.</exception>
    Task SaveCheckpointAsync(
        ProcessingCheckpoint checkpoint,
        CancellationToken cancellationToken = default);
}
