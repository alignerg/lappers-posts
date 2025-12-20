using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.Retry;
using WhatsAppArchiver.Application.Services;
using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Infrastructure.Parsers;

/// <summary>
/// Parser for WhatsApp text export files.
/// </summary>
/// <remarks>
/// This parser handles two common WhatsApp export date formats:
/// <list type="bullet">
/// <item><description>DD/MM/YYYY, HH:mm:ss (24-hour format)</description></item>
/// <item><description>M/D/YY, h:mm:ss AM/PM (12-hour format)</description></item>
/// </list>
/// Multi-line messages are supported by detecting continuation lines that don't start with a timestamp pattern.
/// </remarks>
public sealed class WhatsAppTextFileParser : IChatParser
{
    /// <summary>
    /// Regex pattern for DD/MM/YYYY, HH:mm:ss format (24-hour).
    /// Groups: 1=date (DD/MM/YYYY), 2=time (HH:mm:ss), 3=sender, 4=content.
    /// Example match: [25/12/2024, 09:15:00] John Smith: Hello everyone!
    /// Allows optional Unicode format control characters before the opening bracket.
    /// Content group matches empty messages (e.g., messages with no text after colon) so they can be detected and rejected as parsing failures.
    /// </summary>
    private static readonly Regex DatePattern24Hour = new(
        @"^[\u200B\u200E\u200F\u202A-\u202E]*\[(\d{1,2}/\d{1,2}/\d{4}),\s*(\d{1,2}:\d{2}:\d{2})\]\s*([^:]+):\s*(.*)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Regex pattern for M/D/YY, h:mm:ss AM/PM format (12-hour).
    /// Groups: 1=date (M/D/YY), 2=time (h:mm:ss AM/PM), 3=sender, 4=content.
    /// Example match: [1/5/24, 8:30:00 AM] Sarah Wilson: Good morning!
    /// Allows optional Unicode format control characters before the opening bracket.
    /// Content group matches empty messages (e.g., messages with no text after colon) so they can be detected and rejected as parsing failures.
    /// </summary>
    private static readonly Regex DatePattern12Hour = new(
        @"^[\u200B\u200E\u200F\u202A-\u202E]*\[(\d{1,2}/\d{1,2}/\d{2,4}),\s*(\d{1,2}:\d{2}:\d{2}\s*(?:AM|PM))\]\s*([^:]+):\s*(.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Pattern to detect if a line starts with a timestamp (for continuation detection)
    // Allows optional Unicode format control characters before the opening bracket
    private static readonly Regex TimestampStartPattern = new(
        @"^[\u200B\u200E\u200F\u202A-\u202E]*\[\d{1,2}/\d{1,2}/\d{2,4},\s*\d{1,2}:\d{2}:\d{2}(?:\s*(?:AM|PM))?\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Media placeholder patterns to filter
    private static readonly HashSet<string> MediaPlaceholderPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "<Media omitted>",
        "[image omitted]",
        "[video omitted]",
        "[audio omitted]",
        "<attached: image>",
        "<attached: video>",
        "<attached: audio>",
        "This message was deleted."
    };

    // Attachment pattern prefix for dynamic filename matching
    private const string AttachedPrefix = "<attached: ";

    // Regex pattern to match Unicode format control characters (zero-width space and bidirectional control characters)
    private static readonly Regex FormatControlCharsPattern = new(
        @"[\u200B\u200E\u200F\u202A-\u202E]",
        RegexOptions.Compiled);

    // Regex pattern to detect WhatsApp "added" system messages with proper name patterns
    // Allows exactly one or two capitalized words before and after "added" to avoid over-greedy matches
    // Matches names with apostrophes and hyphens (e.g., "O'Brien", "Mary-Jane")
    private static readonly Regex AddedSystemMessagePattern = new(
        @"^\s*[A-Z][\w'-]*(?:\s+[A-Z][\w'-]*)?\s+added\s+[A-Z][\w'-]*(?:\s+[A-Z][\w'-]*)?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Regex pattern to detect WhatsApp word boundary for "who" to catch variations
    private static readonly Regex WhoWordBoundaryPattern = new(
        @"\bwho\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Regex pattern to detect WhatsApp "created group" system messages
    // Matches format: "[Name] created group "Group name"" where [Name] is one or more capitalized words
    // Excludes common pronouns (e.g., "We created group ...") to avoid matching conversational messages
    private static readonly Regex CreatedGroupPattern = new(
        @"^\s*(?!We\b|I\b|They\b|You\b)([A-Z][\w'-]*)(?:\s+[A-Z][\w'-]*)*\s+created group ""[^""]+""\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<WhatsAppTextFileParser> _logger;
    private readonly Func<string, CancellationToken, Task<string[]>>? _fileReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhatsAppTextFileParser"/> class.
    /// </summary>
    public WhatsAppTextFileParser()
        : this(NullLogger<WhatsAppTextFileParser>.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WhatsAppTextFileParser"/> class with a logger.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    public WhatsAppTextFileParser(ILogger<WhatsAppTextFileParser> logger)
        : this(logger, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WhatsAppTextFileParser"/> class with a logger and custom file reader.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="fileReader">Optional custom file reader for testing purposes.</param>
    internal WhatsAppTextFileParser(ILogger<WhatsAppTextFileParser> logger, Func<string, CancellationToken, Task<string[]>>? fileReader)
    {
        _logger = logger ?? NullLogger<WhatsAppTextFileParser>.Instance;
        _fileReader = fileReader;
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                ShouldHandle = new PredicateBuilder().Handle<IOException>()
            })
            .Build();
    }

    /// <inheritdoc/>
    public async Task<ChatExport> ParseAsync(string filePath, TimeSpan? timeZoneOffset = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        }

        // Skip file existence check if using custom file reader (for testing)
        if (_fileReader is null && !File.Exists(filePath))
        {
            throw new FileNotFoundException("The specified file does not exist.", filePath);
        }

        var offset = timeZoneOffset ?? TimeSpan.Zero;
        _logger.LogDebug("Parsing file {FilePath} with timezone offset {Offset}", filePath, offset);

        var readFileAsync = _fileReader ?? ((path, ct) => File.ReadAllLinesAsync(path, ct));
        var lines = await _resiliencePipeline.ExecuteAsync(
            async ct => await readFileAsync(filePath, ct),
            cancellationToken);

        var messages = new List<ChatMessage>();
        var failedLineCount = 0;
        var totalLines = lines.Length;

        ChatMessage? currentMessage = null;
        var currentContent = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Check if this line starts a new message
            if (TimestampStartPattern.IsMatch(line))
            {
                // Save the previous message if exists
                if (currentMessage is not null)
                {
                    var result = TryAddFinalMessage(messages, currentMessage, currentContent, i);
                    if (result == MessageAddResult.Failed)
                    {
                        failedLineCount++;
                    }
                }

                // Try to parse as a new message
                var (message, isSuccess) = TryParseMessageLine(line, offset, i + 1);

                if (isSuccess && message is not null)
                {
                    currentMessage = message;
                    currentContent.Clear();
                    currentContent.Append(message.Content);
                }
                else
                {
                    // Line starts with timestamp but couldn't be parsed as a message
                    _logger.LogWarning("Line {LineNumber} starts with timestamp but could not be parsed: {Line}",
                        i + 1, line.Length > 100 ? line[..100] + "..." : line);
                    currentMessage = null;
                    currentContent.Clear();
                    failedLineCount++;
                }
            }
            else if (currentMessage is not null)
            {
                // This is a continuation line
                currentContent.AppendLine();
                currentContent.Append(StripFormatControlCharacters(line));
            }
            else
            {
                // Orphan line (no current message to attach to)
                _logger.LogWarning("Orphan line {LineNumber} (no message context): {Line}",
                    i + 1, line.Length > 100 ? line[..100] + "..." : line);
                failedLineCount++;
            }
        }

        // Don't forget the last message
        if (currentMessage is not null)
        {
            var result = TryAddFinalMessage(messages, currentMessage, currentContent, lines.Length);
            if (result == MessageAddResult.Failed)
            {
                failedLineCount++;
            }
        }

        var metadata = ParsingMetadata.Create(
            Path.GetFileName(filePath),
            DateTimeOffset.UtcNow,
            totalLines,
            messages.Count,
            failedLineCount);

        _logger.LogInformation(
            "Parsed {ParsedCount} messages from {TotalLines} lines with {FailedCount} failures",
            messages.Count, totalLines, failedLineCount);

        return ChatExport.Create(messages, metadata);
    }

    private MessageAddResult TryAddFinalMessage(List<ChatMessage> messages, ChatMessage currentMessage, StringBuilder currentContent, int lineNumber)
    {
        var finalContent = currentContent.ToString();

        // Remove edited message tag if present
        finalContent = RemoveEditedMessageTag(finalContent);

        // Filter out link-only messages
        if (IsLinkOnlyMessage(finalContent))
        {
            _logger.LogDebug("Filtered out link-only message at line {LineNumber}", lineNumber);
            return MessageAddResult.Filtered;
        }

        // Filter out media and deleted messages
        if (ShouldFilterMessage(finalContent))
        {
            _logger.LogDebug("Filtered out placeholder message at line {LineNumber}", lineNumber);
            return MessageAddResult.Filtered;
        }

        try
        {
            messages.Add(ChatMessage.Create(
                currentMessage.Timestamp,
                currentMessage.Sender,
                finalContent));
            return MessageAddResult.Success;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create message at line {LineNumber}: {Message} (Parameter: {ParamName})",
                lineNumber,
                ex.Message,
                ex.ParamName);
            return MessageAddResult.Failed;
        }
    }

    /// <summary>
    /// Strips invisible format control characters from text.
    /// </summary>
    /// <param name="text">The text to clean.</param>
    /// <returns>The text with all format control characters removed.</returns>
    /// <remarks>
    /// WhatsApp chat exports contain invisible format control characters throughout the text:
    /// - ZWSP (U+200B): Zero-Width Space
    /// - LRM (U+200E): Left-to-Right Mark
    /// - RLM (U+200F): Right-to-Left Mark
    /// - LRE (U+202A): Left-to-Right Embedding
    /// - RLE (U+202B): Right-to-Left Embedding
    /// - PDF (U+202C): Pop Directional Formatting
    /// - LRO (U+202D): Left-to-Right Override
    /// - RLO (U+202E): Right-to-Left Override
    /// These characters appear in sender names, message content, system messages, and media placeholders.
    /// This method removes these characters to ensure clean text processing and accurate filtering.
    /// </remarks>
    private static string StripFormatControlCharacters(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return FormatControlCharsPattern.Replace(text, string.Empty);
    }

    /// <summary>
    /// Removes the WhatsApp edited message tag from message content.
    /// </summary>
    /// <param name="content">The message content to clean.</param>
    /// <returns>The message content with the edited tag removed, or the original content if no tag is present.</returns>
    /// <remarks>
    /// WhatsApp appends "&lt;This message was edited&gt;" to messages that have been edited.
    /// This method removes that tag to keep only the actual message content.
    /// </remarks>
    private static string RemoveEditedMessageTag(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        const string editedTag = "<This message was edited>";

        // Check if the content ends with the edited tag
        if (content.EndsWith(editedTag, StringComparison.Ordinal))
        {
            // Remove the tag and trim any trailing whitespace
            return content[..^editedTag.Length].TrimEnd();
        }

        return content;
    }

    /// <summary>
    /// Determines if a message content represents a link-only message that should be filtered.
    /// </summary>
    /// <param name="content">The message content to check.</param>
    /// <returns>True if the content is a link-only message (starts with http:// or https:// and contains no whitespace); otherwise, false.</returns>
    /// <remarks>
    /// This method filters messages that contain only a URL without any accompanying text.
    /// Multi-line messages or messages with links embedded in text are not filtered.
    /// </remarks>
    private static bool IsLinkOnlyMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var trimmed = content.Trim();

        // Check if the entire message is a URL (http:// or https://)
        return (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
               trimmed.IndexOf(' ') == -1;
    }

    /// <summary>
    /// Determines if a message content represents a placeholder that should be filtered out.
    /// </summary>
    /// <param name="content">The message content to check.</param>
    /// <returns>True if the content matches a known placeholder pattern that should be filtered; otherwise, false.</returns>
    /// <remarks>
    /// This method filters placeholder messages that don't contain meaningful text content.
    /// Supported patterns include:
    /// - <c>&lt;Media omitted&gt;</c>
    /// - <c>[image omitted]</c>, <c>[video omitted]</c>, <c>[audio omitted]</c>
    /// - <c>&lt;attached: image&gt;</c>, <c>&lt;attached: video&gt;</c>, <c>&lt;attached: audio&gt;</c>
    /// - <c>&lt;attached: FILENAME&gt;</c> (e.g., <c>&lt;attached: 00000387-PHOTO-2025-07-19-08-43-56.jpg&gt;</c>)
    /// - <c>&lt;attached: FILENAME&gt;</c> with negative file numbers (e.g., <c>&lt;attached: -0000013-AUDIO-2025-12-05-03-47-56.opus&gt;</c>)
    /// - <c>This message was deleted.</c>
    /// - WhatsApp system messages (group notifications like "added you", "left", "changed this group's icon", etc.)
    /// 
    /// For attachment patterns with filenames, a space after the colon is required and the filename must be non-empty.
    /// This includes both valid positive file numbers and invalid negative file numbers from WhatsApp export bugs.
    /// All comparisons are case-insensitive.
    /// </remarks>
    private static bool ShouldFilterMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var trimmed = content.Trim();

        // Check for common media placeholder patterns using HashSet for O(1) lookup
        if (MediaPlaceholderPatterns.Contains(trimmed))
        {
            return true;
        }

        // Check for attachment patterns with filenames: <attached: FILENAME>
        // Requires a space after the colon and non-empty content before the closing bracket
        if (trimmed.StartsWith(AttachedPrefix, StringComparison.OrdinalIgnoreCase) &&
            trimmed.EndsWith(">", StringComparison.Ordinal) &&
            trimmed.Length > AttachedPrefix.Length + 1)
        {
            // Verify there's actual content between the space and closing bracket
            var filename = trimmed[AttachedPrefix.Length..^1];
            if (!string.IsNullOrWhiteSpace(filename))
            {
                return true;
            }
        }

        // Check for WhatsApp system messages (group notifications)
        if (IsSystemMessage(trimmed))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a message content represents a WhatsApp system message (group notification).
    /// </summary>
    /// <param name="content">The message content to check.</param>
    /// <returns>True if the content matches a system message pattern; otherwise, false.</returns>
    /// <remarks>
    /// System messages are generated by WhatsApp to notify group members about events like:
    /// - Member additions ("Rudi Anderson added you", "John added Mary")
    /// - Member removals ("John removed Mary")
    /// - Members leaving ("John left")
    /// - Members joining via link ("John joined using this group's invite link")
    /// - Group setting changes ("John changed this group's icon", "Mary changed the subject from X to Y")
    /// - Admin changes ("You're now an admin", "John is now an admin")
    /// 
    /// These messages don't contain user-generated content and are filtered out during parsing.
    /// 
    /// The pattern matching in this method is intentionally heuristic and relatively broad: it looks for
    /// messages that END with or closely match known system-message phrases and keywords in order to catch
    /// variations across different WhatsApp exports and locales.
    /// 
    /// As a consequence, very short user messages that structurally resemble system notifications (for example,
    /// "John added sugar" or "Mary added a comment") may be classified as system messages and
    /// filtered out as edge cases. If this behavior is undesirable for a particular dataset, the patterns in
    /// this method should be refined to better fit that usage.
    /// 
    /// User messages with punctuation like "?", "!", or conversational indicators are not filtered.
    /// 
    /// <para><strong>Pattern Matching Limitations:</strong></para>
    /// <list type="bullet">
    /// <item><description>The "added" pattern requires capitalized names (e.g., "John added Mary") and supports
    /// names containing apostrophes and hyphens (e.g., "O'Brien", "Anne-Marie") but may not match names with
    /// other special characters, lowercase usernames, or non-English scripts. This is based on typical WhatsApp
    /// system message formatting.</description></item>
    /// <item><description>The "removed" pattern may filter some conversational messages like "Alice removed Bob"
    /// if they don't contain common pronouns or articles. Messages like "Alice removed Bob from the list" would
    /// still be filtered unless they contain excluded patterns like "removed the".</description></item>
    /// </list>
    /// </remarks>
    private static bool IsSystemMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var lowerContent = content.ToLowerInvariant();
        var trimmedContent = content.Trim();

        // User messages with questions, exclamations, or other conversational markers should not be filtered
        if (trimmedContent.Contains('?') || trimmedContent.Contains('!'))
        {
            return false;
        }

        // Check for conversational indicators that suggest user-generated content
        if (lowerContent.Contains("someone ", StringComparison.Ordinal) ||
            lowerContent.Contains("yesterday", StringComparison.Ordinal) ||
            lowerContent.Contains("the new member", StringComparison.Ordinal) ||
            WhoWordBoundaryPattern.IsMatch(lowerContent))
        {
            return false;
        }

        // Normalize content for system-message suffix checks by trimming trailing whitespace
        // and common punctuation. This makes patterns like "John left." still match "left".
        var normalizedSystemMessageContent = lowerContent.TrimEnd().TrimEnd('.', '!', '?');

        // Check for patterns that typically appear at the end of system messages
        // or are very specific to system messages
        if (normalizedSystemMessageContent.EndsWith("added you", StringComparison.Ordinal) ||
            normalizedSystemMessageContent.EndsWith("left", StringComparison.Ordinal) ||
            normalizedSystemMessageContent.EndsWith("joined", StringComparison.Ordinal) ||
            lowerContent.Contains("joined using this group's invite link", StringComparison.Ordinal) ||
            lowerContent.Contains("changed this group's icon", StringComparison.Ordinal) ||
            lowerContent.Contains("changed the group description", StringComparison.Ordinal) ||
            lowerContent.Contains("changed this group's settings", StringComparison.Ordinal) ||
            normalizedSystemMessageContent == "you're now an admin" ||
            normalizedSystemMessageContent.EndsWith("is now an admin", StringComparison.Ordinal))
        {
            return true;
        }

        // Check for "added" pattern: typically "Person1 added Person2"
        // Must be in a specific format to avoid filtering conversational messages like
        // "I added you to my contacts" or "The team added a new feature".
        if (lowerContent.Contains(" added ", StringComparison.Ordinal) &&
            !lowerContent.Contains("i added", StringComparison.Ordinal) &&
            !lowerContent.Contains("to my", StringComparison.Ordinal) &&
            !lowerContent.Contains("contacts", StringComparison.Ordinal) &&
            !lowerContent.Contains(" added a ", StringComparison.Ordinal) &&
            !lowerContent.Contains(" added the ", StringComparison.Ordinal) &&
            !lowerContent.Contains(" added some ", StringComparison.Ordinal) &&
            AddedSystemMessagePattern.IsMatch(trimmedContent))
        {
            return true;
        }

        // Check for "removed" pattern: typically "Person1 removed Person2"
        // Avoid filtering conversational messages like "I removed the old files"
        // Note: May still filter some conversational messages like "Alice removed Bob"
        // unless they contain excluded patterns like "removed the" or start with pronouns.
        if (lowerContent.Contains(" removed ", StringComparison.Ordinal) &&
            !lowerContent.StartsWith("i ", StringComparison.Ordinal) &&
            !lowerContent.StartsWith("he ", StringComparison.Ordinal) &&
            !lowerContent.StartsWith("she ", StringComparison.Ordinal) &&
            !lowerContent.StartsWith("they ", StringComparison.Ordinal) &&
            !lowerContent.StartsWith("we ", StringComparison.Ordinal) &&
            !lowerContent.StartsWith("you ", StringComparison.Ordinal) &&
            !lowerContent.Contains(" removed the ", StringComparison.Ordinal) &&
            !lowerContent.Contains(" removed my ", StringComparison.Ordinal) &&
            !lowerContent.Contains(" removed your ", StringComparison.Ordinal) &&
            !lowerContent.Contains(" removed our ", StringComparison.Ordinal) &&
            !lowerContent.Contains(" removed their ", StringComparison.Ordinal) &&
            !lowerContent.Contains(" removed his ", StringComparison.Ordinal) &&
            !lowerContent.Contains(" removed her ", StringComparison.Ordinal))
        {
            return true;
        }

        // Check for "created group" system message pattern: typically "[name] created group "Group name""
        // Using a regex here ensures we only match the exact WhatsApp-style system message format
        if (CreatedGroupPattern.IsMatch(trimmedContent))
        {
            return true;
        }

        // Check for "changed the subject" pattern in the specific WhatsApp system message format
        // This avoids classifying conversational sentences like "He changed the subject to politics"
        // as system messages by requiring quoted group names.
        if (lowerContent.Contains("changed the subject from \"", StringComparison.Ordinal) ||
            lowerContent.Contains("changed the subject to \"", StringComparison.Ordinal) ||
            lowerContent.Contains("changed the subject from '", StringComparison.Ordinal) ||
            lowerContent.Contains("changed the subject to '", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Represents the result of attempting to add a message.
    /// </summary>
    private enum MessageAddResult
    {
        /// <summary>
        /// The message was successfully added.
        /// </summary>
        Success,

        /// <summary>
        /// The message was intentionally filtered out (link-only or media placeholder).
        /// </summary>
        Filtered,

        /// <summary>
        /// The message failed to be added due to a parsing or validation error.
        /// </summary>
        Failed
    }

    private (ChatMessage? Message, bool IsSuccess) TryParseMessageLine(string line, TimeSpan offset, int lineNumber)
    {
        // Try 24-hour format first (DD/MM/YYYY)
        var match = DatePattern24Hour.Match(line);
        if (match.Success)
        {
            return TryCreateMessage(match, is24HourFormat: true, offset, lineNumber);
        }

        // Try 12-hour format (M/D/YY)
        match = DatePattern12Hour.Match(line);
        if (match.Success)
        {
            return TryCreateMessage(match, is24HourFormat: false, offset, lineNumber);
        }

        return (null, false);
    }

    private (ChatMessage? Message, bool IsSuccess) TryCreateMessage(Match match, bool is24HourFormat, TimeSpan offset, int lineNumber)
    {
        var dateStr = match.Groups[1].Value;
        var timeStr = match.Groups[2].Value;
        var sender = StripFormatControlCharacters(match.Groups[3].Value.Trim());
        var content = StripFormatControlCharacters(match.Groups[4].Value);

        // Reject messages with empty content (e.g., "[timestamp] Sender:" with nothing after)
        // This ensures consistency with messages that become empty after post-processing
        if (string.IsNullOrWhiteSpace(content))
        {
            _logger.LogDebug("Rejected empty message at line {LineNumber}", lineNumber);
            return (null, false); // Return false to count as parsing failure
        }

        if (!TryParseDateTime(dateStr, timeStr, is24HourFormat, offset, out var timestamp))
        {
            _logger.LogDebug("Failed to parse date/time at line {LineNumber}: {Date} {Time}", lineNumber, dateStr, timeStr);
            return (null, false);
        }

        try
        {
            var message = ChatMessage.Create(timestamp, sender, content);
            return (message, true);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to create message at line {LineNumber}: {Message} (Parameter: {ParamName})",
                lineNumber,
                ex.Message,
                ex.ParamName);
            return (null, false);
        }
    }

    private static bool TryParseDateTime(string dateStr, string timeStr, bool is24HourFormat, TimeSpan offset, out DateTimeOffset result)
    {
        result = default;

        var dateParts = dateStr.Split('/');
        if (dateParts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(dateParts[0], out var part1) ||
            !int.TryParse(dateParts[1], out var part2) ||
            !int.TryParse(dateParts[2], out var year))
        {
            return false;
        }

        // Handle 2-digit years using a pivot year of 50:
        // 00-49 => 2000-2049, 50-99 => 1950-1999
        if (year < 100)
        {
            year += (year < 50) ? 2000 : 1900;
        }

        int day, month;

        if (is24HourFormat)
        {
            // DD/MM/YYYY format
            day = part1;
            month = part2;
        }
        else
        {
            // M/D/YY format
            month = part1;
            day = part2;
        }

        // Validate date parts
        if (month < 1 || month > 12 || day < 1 || day > 31)
        {
            return false;
        }

        // Parse time
        if (!TimeSpan.TryParse(is24HourFormat ? timeStr : ConvertTo24HourTime(timeStr), out var time))
        {
            return false;
        }

        try
        {
            var dateTime = new DateTime(year, month, day, time.Hours, time.Minutes, time.Seconds);
            result = new DateTimeOffset(dateTime, offset);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            // Invalid date/time values; treat as parse failure.
            return false;
        }
    }

    private static string ConvertTo24HourTime(string timeStr)
    {
        // Parse time like "8:30:00 AM" or "11:58:45 PM"
        var normalizedTime = timeStr.Trim().ToUpperInvariant();
        var isPm = normalizedTime.EndsWith("PM");
        var isAm = normalizedTime.EndsWith("AM");

        if (!isPm && !isAm)
        {
            return timeStr;
        }

        var timePart = normalizedTime.Replace("AM", "").Replace("PM", "").Trim();
        var parts = timePart.Split(':');

        if (parts.Length != 3 ||
            !int.TryParse(parts[0], out var hours) ||
            !int.TryParse(parts[1], out var minutes) ||
            !int.TryParse(parts[2], out var seconds))
        {
            return timeStr;
        }

        if (isPm && hours != 12)
        {
            hours += 12;
        }
        else if (isAm && hours == 12)
        {
            hours = 0;
        }

        return $"{hours}:{minutes:D2}:{seconds:D2}";
    }
}
