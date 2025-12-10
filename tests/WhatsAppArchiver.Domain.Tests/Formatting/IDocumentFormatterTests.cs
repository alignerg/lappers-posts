using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class IDocumentFormatterTests
{
    [Fact(DisplayName = "IDocumentFormatter is a standalone interface")]
    public void TypeInheritance_IDocumentFormatter_DoesNotExtendIMessageFormatter()
    {
        var documentFormatterType = typeof(IDocumentFormatter);
        var messageFormatterType = typeof(IMessageFormatter);

        Assert.False(messageFormatterType.IsAssignableFrom(documentFormatterType));
    }

    [Fact(DisplayName = "IDocumentFormatter has FormatDocument method")]
    public void InterfaceStructure_IDocumentFormatter_HasFormatDocumentMethod()
    {
        var method = typeof(IDocumentFormatter).GetMethod("FormatDocument");

        Assert.NotNull(method);
        Assert.Equal(typeof(string), method.ReturnType);
        
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(ChatExport), parameters[0].ParameterType);
        Assert.Equal("chatExport", parameters[0].Name);
    }

    [Fact(DisplayName = "Document formatter implementation formats entire export")]
    public void FormatDocument_ValidChatExport_ReturnsFormattedDocument()
    {
        var formatter = new TestDocumentFormatter();
        var messages = new[]
        {
            ChatMessage.Create(DateTimeOffset.UtcNow, "Alice", "First message"),
            ChatMessage.Create(DateTimeOffset.UtcNow.AddMinutes(1), "Bob", "Second message")
        };
        var metadata = ParsingMetadata.Create("test.txt", DateTimeOffset.UtcNow, messages.Length, messages.Length, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = formatter.FormatDocument(chatExport);

        Assert.NotNull(result);
        Assert.Contains("Document with 2 messages", result);
    }

    [Fact(DisplayName = "FormatDocument with null export throws ArgumentNullException")]
    public void FormatDocument_NullChatExport_ThrowsArgumentNullException()
    {
        var formatter = new TestDocumentFormatter();

        var exception = Assert.Throws<ArgumentNullException>(() => formatter.FormatDocument(null!));

        Assert.Equal("chatExport", exception.ParamName);
    }

    [Fact(DisplayName = "Document formatter can compose message formatter")]
    public void FormatDocument_WithMessageFormatter_UsesComposition()
    {
        var messageFormatter = new DefaultMessageFormatter();
        var formatter = new CompositeDocumentFormatter(messageFormatter);
        var messages = new[]
        {
            ChatMessage.Create(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero), "Alice", "Hello")
        };
        var metadata = ParsingMetadata.Create("test.txt", DateTimeOffset.UtcNow, messages.Length, messages.Length, 0);
        var chatExport = ChatExport.Create(messages, metadata);

        var result = formatter.FormatDocument(chatExport);

        Assert.Contains("[15/01/2024, 10:30:00] Alice: Hello", result);
    }

    private sealed class TestDocumentFormatter : IDocumentFormatter
    {
        public string FormatDocument(ChatExport chatExport)
        {
            ArgumentNullException.ThrowIfNull(chatExport);

            return $"Document with {chatExport.MessageCount} messages";
        }
    }

    private sealed class CompositeDocumentFormatter : IDocumentFormatter
    {
        private readonly IMessageFormatter _messageFormatter;

        public CompositeDocumentFormatter(IMessageFormatter messageFormatter)
        {
            _messageFormatter = messageFormatter;
        }

        public string FormatDocument(ChatExport chatExport)
        {
            ArgumentNullException.ThrowIfNull(chatExport);

            var formatted = new System.Text.StringBuilder();
            foreach (var message in chatExport.Messages)
            {
                formatted.AppendLine(_messageFormatter.FormatMessage(message));
            }
            return formatted.ToString();
        }
    }
}
