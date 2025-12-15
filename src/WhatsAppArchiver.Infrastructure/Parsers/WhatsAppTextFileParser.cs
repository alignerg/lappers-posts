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
    /// </summary>
    private static readonly Regex DatePattern24Hour = new(
        @"^\[(\d{1,2}/\d{1,2}/\d{4}),\s*(\d{1,2}:\d{2}:\d{2})\]\s*([^:]+):\s*(.+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Regex pattern for M/D/YY, h:mm:ss AM/PM format (12-hour).
    /// Groups: 1=date (M/D/YY), 2=time (h:mm:ss AM/PM), 3=sender, 4=content.
    /// Example match: [1/5/24, 8:30:00 AM] Sarah Wilson: Good morning!
    /// </summary>
    private static readonly Regex DatePattern12Hour = new(
        @"^\[(\d{1,2}/\d{1,2}/\d{2,4}),\s*(\d{1,2}:\d{2}:\d{2}\s*(?:AM|PM))\]\s*([^:]+):\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Pattern to detect if a line starts with a timestamp (for continuation detection)
    private static readonly Regex TimestampStartPattern = new(
        @"^\[\d{1,2}/\d{1,2}/\d{2,4},\s*\d{1,2}:\d{2}:\d{2}(?:\s*(?:AM|PM))?\]",
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
        "<attached: audio>"
    };

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
                    currentMessage = null;
                    currentContent.Clear();
                    failedLineCount++;
                }
            }
            else if (currentMessage is not null)
            {
                // This is a continuation line
                currentContent.AppendLine();
                currentContent.Append(line);
            }
            else
            {
                // Orphan line (no current message to attach to)
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
        
        // Filter out link-only messages
        if (IsLinkOnlyMessage(finalContent))
        {
            _logger.LogDebug("Filtered out link-only message at line {LineNumber}", lineNumber);
            return MessageAddResult.Filtered;
        }
        
        // Filter out media messages
        if (IsMediaMessage(finalContent))
        {
            _logger.LogDebug("Filtered out media message at line {LineNumber}", lineNumber);
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
    /// Determines if a message content represents a media placeholder that should be filtered.
    /// </summary>
    /// <param name="content">The message content to check.</param>
    /// <returns>True if the content matches a known media placeholder pattern; otherwise, false.</returns>
    /// <remarks>
    /// This method filters messages that indicate media attachments without meaningful text content.
    /// Supported patterns include:
    /// - <c>&lt;Media omitted&gt;</c>
    /// - <c>[image omitted]</c>, <c>[video omitted]</c>, <c>[audio omitted]</c>
    /// - <c>&lt;attached: image&gt;</c>, <c>&lt;attached: video&gt;</c>, <c>&lt;attached: audio&gt;</c>
    /// All comparisons are case-insensitive.
    /// </remarks>
    private static bool IsMediaMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }
        
        var trimmed = content.Trim();
        
        // Check for common media placeholder patterns using HashSet for O(1) lookup
        return MediaPlaceholderPatterns.Contains(trimmed);
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
        var sender = match.Groups[3].Value.Trim();
        var content = match.Groups[4].Value;

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
