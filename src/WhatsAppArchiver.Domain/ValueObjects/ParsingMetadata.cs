namespace WhatsAppArchiver.Domain.ValueObjects;

/// <summary>
/// Represents metadata about the parsing of a chat export.
/// </summary>
/// <remarks>
/// This value object captures information about the parsing process,
/// including source file information and parsing statistics.
/// </remarks>
public sealed record ParsingMetadata
{
    /// <summary>
    /// Gets the name of the source file.
    /// </summary>
    public string SourceFileName { get; }

    /// <summary>
    /// Gets the timestamp when parsing occurred.
    /// </summary>
    public DateTimeOffset ParsedAt { get; }

    /// <summary>
    /// Gets the total number of lines in the source file.
    /// </summary>
    public int TotalLines { get; }

    /// <summary>
    /// Gets the number of successfully parsed messages.
    /// </summary>
    public int ParsedMessageCount { get; }

    /// <summary>
    /// Gets the number of lines that failed to parse.
    /// </summary>
    public int FailedLineCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsingMetadata"/> record.
    /// </summary>
    /// <param name="sourceFileName">The name of the source file.</param>
    /// <param name="parsedAt">The timestamp when parsing occurred.</param>
    /// <param name="totalLines">The total number of lines in the source file.</param>
    /// <param name="parsedMessageCount">The number of successfully parsed messages.</param>
    /// <param name="failedLineCount">The number of lines that failed to parse.</param>
    /// <exception cref="ArgumentException">Thrown when sourceFileName is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when any count is negative.</exception>
    public ParsingMetadata(
        string sourceFileName,
        DateTimeOffset parsedAt,
        int totalLines,
        int parsedMessageCount,
        int failedLineCount)
    {
        if (string.IsNullOrWhiteSpace(sourceFileName))
        {
            throw new ArgumentException("Source file name cannot be null or whitespace.", nameof(sourceFileName));
        }

        if (totalLines < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalLines), "Total lines cannot be negative.");
        }

        if (parsedMessageCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(parsedMessageCount), "Parsed message count cannot be negative.");
        }

        if (failedLineCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(failedLineCount), "Failed line count cannot be negative.");
        }

        SourceFileName = sourceFileName;
        ParsedAt = parsedAt;
        TotalLines = totalLines;
        ParsedMessageCount = parsedMessageCount;
        FailedLineCount = failedLineCount;
    }

    /// <summary>
    /// Creates a new ParsingMetadata with the specified parameters.
    /// </summary>
    /// <param name="sourceFileName">The name of the source file.</param>
    /// <param name="parsedAt">The timestamp when parsing occurred.</param>
    /// <param name="totalLines">The total number of lines in the source file.</param>
    /// <param name="parsedMessageCount">The number of successfully parsed messages.</param>
    /// <param name="failedLineCount">The number of lines that failed to parse.</param>
    /// <returns>A new ParsingMetadata instance.</returns>
    public static ParsingMetadata Create(
        string sourceFileName,
        DateTimeOffset parsedAt,
        int totalLines,
        int parsedMessageCount,
        int failedLineCount)
        => new(sourceFileName, parsedAt, totalLines, parsedMessageCount, failedLineCount);
}
