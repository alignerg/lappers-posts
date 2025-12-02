using WhatsAppArchiver.Domain.Aggregates;

namespace WhatsAppArchiver.Application.Services;

/// <summary>
/// Defines operations for parsing WhatsApp chat export files.
/// </summary>
/// <remarks>
/// This interface abstracts the parsing logic for WhatsApp chat exports,
/// enabling different parsing strategies for various export formats.
/// Implementations handle the specific parsing logic including line parsing,
/// timestamp extraction, and message continuation handling.
/// </remarks>
/// <example>
/// <code>
/// var chatExport = await chatParser.ParseAsync("path/to/chat.txt");
/// foreach (var message in chatExport.Messages)
/// {
///     Console.WriteLine($"{message.Sender}: {message.Content}");
/// }
/// </code>
/// </example>
public interface IChatParser
{
    /// <summary>
    /// Parses a WhatsApp chat export file and returns the resulting aggregate.
    /// </summary>
    /// <param name="filePath">The path to the chat export file.</param>
    /// <param name="timeZoneOffset">
    /// The timezone offset for the export file timestamps. WhatsApp exports typically contain
    /// timestamps in the local timezone of the device that created the export. If null, UTC is assumed.
    /// </param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ChatExport"/> aggregate containing all parsed messages
    /// and metadata about the parsing process.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    Task<ChatExport> ParseAsync(string filePath, TimeSpan? timeZoneOffset = null, CancellationToken cancellationToken = default);
}
