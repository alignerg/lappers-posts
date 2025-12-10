namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Specifies the type of message formatting to apply.
/// </summary>
/// <remarks>
/// This enum is used by the <see cref="FormatterFactory"/> to create the appropriate
/// <see cref="IMessageFormatter"/> implementation at runtime.
/// </remarks>
public enum MessageFormatType
{
    /// <summary>
    /// Default format: [{timestamp}] {sender}: {content}
    /// </summary>
    Default,

    /// <summary>
    /// Compact format: {sender}: {content}
    /// </summary>
    Compact,

    /// <summary>
    /// Verbose format with detailed date, time, and sender metadata.
    /// </summary>
    Verbose,

    /// <summary>
    /// Structured markdown document with friendly date headers (MMMM d, yyyy) and individual timestamped posts. Requires IDocumentFormatter for batch processing.
    /// </summary>
    MarkdownDocument
}
