namespace WhatsAppArchiver.Application.Services;

/// <summary>
/// Defines operations for interacting with Google Docs.
/// </summary>
/// <remarks>
/// This interface abstracts the Google Docs API interactions,
/// enabling document creation, content upload, and appending.
/// Implementations handle authentication, API calls, and error handling.
/// </remarks>
/// <example>
/// <code>
/// // Upload new content to an existing document
/// await googleDocsService.UploadAsync("doc-123", "New content to add");
/// 
/// // Append content to the end of a document
/// await googleDocsService.AppendAsync("doc-123", "Additional content");
/// </code>
/// </example>
public interface IGoogleDocsService
{
    /// <summary>
    /// Uploads content to a Google Docs document, replacing existing content.
    /// </summary>
    /// <param name="documentId">The unique identifier of the Google Docs document.</param>
    /// <param name="content">The content to upload.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous upload operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="documentId"/> is null or empty.</exception>
    Task UploadAsync(
        string documentId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends content to the end of a Google Docs document.
    /// </summary>
    /// <param name="documentId">The unique identifier of the Google Docs document.</param>
    /// <param name="content">The content to append.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous append operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="documentId"/> is null or empty.</exception>
    Task AppendAsync(
        string documentId,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts a structured document with rich formatting at the beginning of a Google Docs document.
    /// </summary>
    /// <param name="documentId">The unique identifier of the Google Docs document.</param>
    /// <param name="document">The structured document to insert with rich formatting.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous insert operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="documentId"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="document"/> is null.</exception>
    Task InsertRichAsync(
        string documentId,
        WhatsAppArchiver.Domain.Formatting.GoogleDocsDocument document,
        CancellationToken cancellationToken = default);
}
