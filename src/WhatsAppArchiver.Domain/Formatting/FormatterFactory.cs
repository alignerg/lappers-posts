namespace WhatsAppArchiver.Domain.Formatting;

/// <summary>
/// Factory for creating message and document formatters based on format type.
/// </summary>
/// <remarks>
/// <para>
/// This factory implements the Strategy pattern by providing runtime selection
/// of the appropriate formatter implementation based on the specified <see cref="MessageFormatType"/>.
/// </para>
/// <para>
/// The factory creates two types of formatters:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Message-level formatters</strong> (<see cref="IMessageFormatter"/>): Process one message
/// at a time for format types: Default, Compact, Verbose.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Document-level formatters</strong> (<see cref="IGoogleDocsFormatter"/>): Process entire
/// <see cref="Aggregates.ChatExport"/> aggregates at once for format type: GoogleDocs.
/// </description>
/// </item>
/// </list>
/// <para>
/// Use <see cref="IsDocumentFormatter"/> to determine if a format type requires document-level processing.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Creating a message-level formatter
/// IMessageFormatter messageFormatter = FormatterFactory.Create(MessageFormatType.Compact);
/// string formatted = messageFormatter.FormatMessage(chatMessage);
/// 
/// // Creating a document-level formatter
/// IMessageFormatter formatter = FormatterFactory.Create(MessageFormatType.GoogleDocs);
/// if (formatter is IGoogleDocsFormatter documentFormatter)
/// {
///     string document = documentFormatter.FormatDocument(chatExport);
/// }
/// 
/// // Checking if a format type requires document-level processing
/// bool isDocument = FormatterFactory.IsDocumentFormatter(MessageFormatType.GoogleDocs);
/// </code>
/// </example>
public static class FormatterFactory
{
    /// <summary>
    /// Creates a message formatter for the specified format type.
    /// </summary>
    /// <param name="formatType">The type of formatting to apply.</param>
    /// <returns>
    /// An <see cref="IMessageFormatter"/> instance for the specified format type.
    /// For document-level format types (e.g., GoogleDocs), the returned instance
    /// also implements <see cref="IGoogleDocsFormatter"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="formatType"/> is not a valid <see cref="MessageFormatType"/> value.
    /// </exception>
    /// <remarks>
    /// Document-level formatters (those where <see cref="IsDocumentFormatter"/> returns true)
    /// implement both <see cref="IMessageFormatter"/> and <see cref="IGoogleDocsFormatter"/>.
    /// Cast to the appropriate interface to access document-level formatting capabilities.
    /// </remarks>
    public static IMessageFormatter Create(MessageFormatType formatType)
    {
        return formatType switch
        {
            MessageFormatType.Default => new DefaultMessageFormatter(),
            MessageFormatType.Compact => new CompactMessageFormatter(),
            MessageFormatType.Verbose => new VerboseMessageFormatter(),
            MessageFormatType.GoogleDocs => new GoogleDocsDocumentFormatter(),
            _ => throw new ArgumentOutOfRangeException(nameof(formatType), formatType, "Unknown message format type.")
        };
    }

    /// <summary>
    /// Determines whether the specified format type requires document-level formatting.
    /// </summary>
    /// <param name="formatType">The format type to check.</param>
    /// <returns>
    /// <c>true</c> if the format type requires document-level formatting (<see cref="IGoogleDocsFormatter"/>)
    /// for processing entire <see cref="Aggregates.ChatExport"/> aggregates; otherwise, <c>false</c>
    /// for message-level formatters.
    /// </returns>
    /// <remarks>
    /// Use this method to determine the appropriate formatting approach:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// If <c>true</c>, cast the formatter to <see cref="IGoogleDocsFormatter"/>
    /// and use the FormatDocument method to process the entire export.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If <c>false</c>, use <see cref="IMessageFormatter.FormatMessage"/> to process
    /// messages individually.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var formatter = FormatterFactory.Create(formatType);
    /// 
    /// if (FormatterFactory.IsDocumentFormatter(formatType))
    /// {
    ///     var documentFormatter = (IGoogleDocsFormatter)formatter;
    ///     content = documentFormatter.FormatDocument(chatExport);
    /// }
    /// else
    /// {
    ///     content = FormatMessagesIndividually(messages, formatter);
    /// }
    /// </code>
    /// </example>
    public static bool IsDocumentFormatter(MessageFormatType formatType)
        => formatType == MessageFormatType.GoogleDocs;
}
