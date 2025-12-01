using System.Globalization;
using System.Text.RegularExpressions;
using Polly;
using Polly.Retry;
using WhatsAppArchiver.Application.Services;
using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Infrastructure.Parsers;

/// <summary>
/// Parses WhatsApp text export files into domain aggregates.
/// </summary>
/// <remarks>
/// This parser handles the standard WhatsApp text export format:
/// <c>[DD/MM/YYYY, HH:mm:ss] Sender: Message content</c>
/// Multi-line messages (lines without timestamps) are treated as continuations
/// of the previous message. Implements retry policies for transient I/O failures.
/// </remarks>
/// <example>
/// <code>
/// var parser = new WhatsAppTextFileParser();
/// var chatExport = await parser.ParseAsync("chat.txt");
/// </code>
/// </example>
public sealed partial class WhatsAppTextFileParser : IChatParser
{
    // Pattern: [DD/MM/YYYY, HH:mm:ss] Sender: Message
    private static readonly Regex MessagePattern = CreateMessagePattern();

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

    /// <summary>
    /// Initializes a new instance of the <see cref="WhatsAppTextFileParser"/> class with a custom resilience pipeline.
    /// </summary>
    /// <param name="resiliencePipeline">The resilience pipeline to use for retries.</param>
    /// <exception cref="ArgumentNullException">Thrown when resiliencePipeline is null.</exception>
    public WhatsAppTextFileParser(ResiliencePipeline resiliencePipeline)
    {
        ArgumentNullException.ThrowIfNull(resiliencePipeline);

        _resiliencePipeline = resiliencePipeline;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    public async Task<ChatExport> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("The specified chat export file was not found.", filePath);
        }

        var lines = await _resiliencePipeline.ExecuteAsync(
            async ct => await File.ReadAllLinesAsync(filePath, ct),
            cancellationToken);

        var parseResult = ParseLines(lines);

        var metadata = ParsingMetadata.Create(
            sourceFileName: Path.GetFileName(filePath),
            parsedAt: DateTimeOffset.UtcNow,
            totalLines: lines.Length,
            parsedMessageCount: parseResult.Messages.Count,
            failedLineCount: parseResult.FailedLineCount);

        return ChatExport.Create(parseResult.Messages, metadata);
    }

    /// <summary>
    /// Parses an array of lines into chat messages.
    /// </summary>
    /// <param name="lines">The lines to parse.</param>
    /// <returns>A result containing the parsed messages and failure count.</returns>
    private static ParseResult ParseLines(string[] lines)
    {
        var messages = new List<ChatMessage>();
        var failedLineCount = 0;
        ChatMessageBuilder? currentBuilder = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var match = MessagePattern.Match(line);

            if (match.Success)
            {
                // Finalize the previous message if there is one
                if (currentBuilder is not null)
                {
                    var message = currentBuilder.Build();

                    if (message is not null)
                    {
                        messages.Add(message);
                    }
                    else
                    {
                        failedLineCount++;
                    }
                }

                // Start a new message - handle potential timestamp parsing failures
                try
                {
                    currentBuilder = CreateBuilderFromMatch(match);
                }
                catch (FormatException)
                {
                    failedLineCount++;
                    currentBuilder = null;
                }
            }
            else if (currentBuilder is not null)
            {
                // This is a continuation line
                currentBuilder.AppendContent(line);
            }
            else
            {
                // Line without a timestamp and no previous message to continue
                failedLineCount++;
            }
        }

        // Finalize the last message
        if (currentBuilder is not null)
        {
            var message = currentBuilder.Build();

            if (message is not null)
            {
                messages.Add(message);
            }
            else
            {
                failedLineCount++;
            }
        }

        return new ParseResult(messages, failedLineCount);
    }

    /// <summary>
    /// Creates a message builder from a regex match.
    /// </summary>
    /// <param name="match">The regex match containing message parts.</param>
    /// <returns>A new ChatMessageBuilder.</returns>
    private static ChatMessageBuilder CreateBuilderFromMatch(Match match)
    {
        var dateStr = match.Groups["date"].Value;
        var timeStr = match.Groups["time"].Value;
        var sender = match.Groups["sender"].Value;
        var content = match.Groups["content"].Value;

        // Parse the timestamp: DD/MM/YYYY, HH:mm:ss
        var dateTimeStr = $"{dateStr} {timeStr}";
        var timestamp = ParseTimestamp(dateTimeStr);

        return new ChatMessageBuilder(timestamp, sender, content);
    }

    /// <summary>
    /// Parses a timestamp string in the format DD/MM/YYYY HH:mm:ss.
    /// </summary>
    /// <param name="dateTimeStr">The datetime string to parse.</param>
    /// <returns>The parsed DateTimeOffset.</returns>
    /// <exception cref="FormatException">Thrown when the timestamp cannot be parsed.</exception>
    private static DateTimeOffset ParseTimestamp(string dateTimeStr)
    {
        // Only allow formats with seconds to match the regex pattern requirement
        string[] formats = ["dd/MM/yyyy HH:mm:ss", "d/M/yyyy HH:mm:ss", "d/M/yyyy H:mm:ss"];

        if (DateTimeOffset.TryParseExact(
            dateTimeStr,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var result))
        {
            return result;
        }

        // If none of the formats work, try generic parsing
        if (DateTimeOffset.TryParse(dateTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result))
        {
            return result;
        }

        throw new FormatException($"Unable to parse timestamp '{dateTimeStr}'. Expected format: DD/MM/YYYY HH:mm:ss");
    }

    /// <summary>
    /// Generates the message pattern regex at compile time.
    /// </summary>
    [GeneratedRegex(@"^\[(?<date>\d{1,2}/\d{1,2}/\d{4}),\s+(?<time>\d{1,2}:\d{2}:\d{2})\]\s+(?<sender>[^:]+):\s+(?<content>.*)$")]
    private static partial Regex CreateMessagePattern();

    /// <summary>
    /// Represents the result of parsing lines.
    /// </summary>
    private sealed record ParseResult(List<ChatMessage> Messages, int FailedLineCount);

    /// <summary>
    /// Builder for constructing ChatMessage instances with multi-line content.
    /// </summary>
    private sealed class ChatMessageBuilder
    {
        private readonly DateTimeOffset _timestamp;
        private readonly string _sender;
        private readonly System.Text.StringBuilder _contentBuilder;

        public ChatMessageBuilder(DateTimeOffset timestamp, string sender, string initialContent)
        {
            _timestamp = timestamp;
            _sender = sender;
            _contentBuilder = new System.Text.StringBuilder(initialContent);
        }

        public void AppendContent(string line)
        {
            _contentBuilder.AppendLine();
            _contentBuilder.Append(line);
        }

        public ChatMessage? Build()
        {
            var content = _contentBuilder.ToString();

            if (string.IsNullOrWhiteSpace(_sender) || string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            return ChatMessage.Create(_timestamp, _sender.Trim(), content.Trim());
        }
    }
}
