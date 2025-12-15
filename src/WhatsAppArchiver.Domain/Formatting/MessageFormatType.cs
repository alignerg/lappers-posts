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
    Default = 0,

    /// <summary>
    /// Compact format: {sender}: {content}
    /// </summary>
    Compact = 1,

    /// <summary>
    /// Verbose format with detailed date, time, and sender metadata.
    /// </summary>
    Verbose = 2,

    /// <summary>
    /// Structured markdown document with friendly date headers (MMMM d, yyyy)
    /// and individual timestamped posts. Requires IGoogleDocsFormatter for batch processing.
    /// </summary>
    [Obsolete("Replaced by GoogleDocs formatter. This value is mapped to GoogleDocs for backward compatibility.")]
    MarkdownDocument = 3,

    /// <summary>
    /// Rich formatted Google Docs with heading styles, bold timestamps, and visual separators.
    /// Requires IGoogleDocsFormatter for batch processing.
    /// </summary>
    GoogleDocs = 4
}
