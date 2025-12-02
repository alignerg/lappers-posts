using System.Text.RegularExpressions;
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
        @"^\[(\d{1,2}/\d{1,2}/\d{4}),\s*(\d{1,2}:\d{2}:\d{2})\]\s*(.+?):\s*(.+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Regex pattern for M/D/YY, h:mm:ss AM/PM format (12-hour).
    /// Groups: 1=date (M/D/YY), 2=time (h:mm:ss AM/PM), 3=sender, 4=content.
    /// Example match: [1/5/24, 8:30:00 AM] Sarah Wilson: Good morning!
    /// </summary>
    private static readonly Regex DatePattern12Hour = new(
        @"^\[(\d{1,2}/\d{1,2}/\d{2,4}),\s*(\d{1,2}:\d{2}:\d{2}\s*(?:AM|PM))\]\s*(.+?):\s*(.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Pattern to detect if a line starts with a timestamp (for continuation detection)
    private static readonly Regex TimestampStartPattern = new(
        @"^\[\d{1,2}/\d{1,2}/\d{2,4},\s*\d{1,2}:\d{2}:\d{2}(?:\s*(?:AM|PM))?\]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ResiliencePipeline _resiliencePipeline;

    /// <summary>
    /// Initializes a new instance of the <see cref="WhatsAppTextFileParser"/> class.
    /// </summary>
    public WhatsAppTextFileParser()
    {
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
    public async Task<ChatExport> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The specified file does not exist.", filePath);
        }

        var lines = await _resiliencePipeline.ExecuteAsync(
            async ct => await File.ReadAllLinesAsync(filePath, ct),
            cancellationToken);

        var messages = new List<ChatMessage>();
        var failedLineCount = 0;
        var totalLines = lines.Length;

        ChatMessage? currentMessage = null;
        var currentContent = new List<string>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Check if this line starts a new message
            if (TimestampStartPattern.IsMatch(line))
            {
                // Save the previous message if exists
                if (currentMessage is not null)
                {
                    if (!TryAddFinalMessage(messages, currentMessage, currentContent))
                    {
                        failedLineCount++;
                    }
                }

                // Try to parse as a new message
                var (message, isSuccess) = TryParseMessageLine(line);

                if (isSuccess && message is not null)
                {
                    currentMessage = message;
                    currentContent.Clear();
                    currentContent.Add(message.Content);
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
                currentContent.Add(line);
            }
            else
            {
                // Orphan line (no current message to attach to)
                failedLineCount++;
            }
        }

        // Don't forget the last message
        if (currentMessage is not null && currentContent.Count > 0)
        {
            if (!TryAddFinalMessage(messages, currentMessage, currentContent))
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

        return ChatExport.Create(messages, metadata);
    }

    private static bool TryAddFinalMessage(List<ChatMessage> messages, ChatMessage currentMessage, List<string> currentContent)
    {
        var finalContent = string.Join(Environment.NewLine, currentContent);
        try
        {
            messages.Add(ChatMessage.Create(
                currentMessage.Timestamp,
                currentMessage.Sender,
                finalContent));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static (ChatMessage? Message, bool IsSuccess) TryParseMessageLine(string line)
    {
        // Try 24-hour format first (DD/MM/YYYY)
        var match = DatePattern24Hour.Match(line);
        if (match.Success)
        {
            return TryCreateMessage(match, is24HourFormat: true);
        }

        // Try 12-hour format (M/D/YY)
        match = DatePattern12Hour.Match(line);
        if (match.Success)
        {
            return TryCreateMessage(match, is24HourFormat: false);
        }

        return (null, false);
    }

    private static (ChatMessage? Message, bool IsSuccess) TryCreateMessage(Match match, bool is24HourFormat)
    {
        var dateStr = match.Groups[1].Value;
        var timeStr = match.Groups[2].Value;
        var sender = match.Groups[3].Value.Trim();
        var content = match.Groups[4].Value;

        if (!TryParseDateTime(dateStr, timeStr, is24HourFormat, out var timestamp))
        {
            return (null, false);
        }

        try
        {
            var message = ChatMessage.Create(timestamp, sender, content);
            return (message, true);
        }
        catch (ArgumentException)
        {
            return (null, false);
        }
    }

    private static bool TryParseDateTime(string dateStr, string timeStr, bool is24HourFormat, out DateTimeOffset result)
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

        // Handle 2-digit years
        if (year < 100)
        {
            year += 2000;
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
            result = new DateTimeOffset(dateTime, TimeSpan.Zero);
            return true;
        }
        catch
        {
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
