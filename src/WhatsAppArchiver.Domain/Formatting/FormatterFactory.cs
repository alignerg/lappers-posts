namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Factory for creating message formatters based on format type.
/// </summary>
/// <remarks>
/// This factory implements the Strategy pattern by providing runtime selection
/// of the appropriate <see cref="IMessageFormatter"/> implementation based on
/// the specified <see cref="MessageFormatType"/>.
/// </remarks>
/// <example>
/// <code>
/// IMessageFormatter formatter = FormatterFactory.Create(MessageFormatType.Compact);
/// string formatted = formatter.FormatMessage(chatMessage);
/// </code>
/// </example>
public static class FormatterFactory
{
    /// <summary>
    /// Creates a message formatter for the specified format type.
    /// </summary>
    /// <param name="formatType">The type of formatting to apply.</param>
    /// <returns>An <see cref="IMessageFormatter"/> instance for the specified format type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="formatType"/> is not a valid <see cref="MessageFormatType"/> value.
    /// </exception>
    public static IMessageFormatter Create(MessageFormatType formatType)
    {
        return formatType switch
        {
            MessageFormatType.Default => new DefaultMessageFormatter(),
            MessageFormatType.Compact => new CompactMessageFormatter(),
            MessageFormatType.Verbose => new VerboseMessageFormatter(),
            MessageFormatType.MarkdownDocument => throw new ArgumentException(
                "MarkdownDocument format type requires IDocumentFormatter for batch processing. Use a document formatter implementation instead of FormatterFactory.Create().",
                nameof(formatType)),
            _ => throw new ArgumentOutOfRangeException(nameof(formatType), formatType, "Unknown message format type.")
        };
    }
}
