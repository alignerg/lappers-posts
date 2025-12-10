using WhatsAppArchiver.Domain.Aggregates;
using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;
using WhatsAppArchiver.Domain.ValueObjects;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class IDocumentFormatterTests
{
    [Fact(DisplayName = "IDocumentFormatter extends IMessageFormatter")]
    public void IDocumentFormatter_ExtendsIMessageFormatter()
    {
        var documentFormatterType = typeof(IDocumentFormatter);
        var messageFormatterType = typeof(IMessageFormatter);

        Assert.True(messageFormatterType.IsAssignableFrom(documentFormatterType));
    }

    [Fact(DisplayName = "IDocumentFormatter has FormatDocument method")]
    public void IDocumentFormatter_HasFormatDocumentMethod()
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

    [Fact(DisplayName = "Document formatter throws NotSupportedException for FormatMessage")]
    public void FormatMessage_DocumentFormatter_ThrowsNotSupportedException()
    {
        var formatter = new TestDocumentFormatter();
        var message = ChatMessage.Create(DateTimeOffset.UtcNow, "Alice", "Test");

        var exception = Assert.Throws<NotSupportedException>(() => formatter.FormatMessage(message));

        Assert.Contains("Document-level formatters do not support individual message formatting", exception.Message);
    }

    [Fact(DisplayName = "FormatDocument with null export throws ArgumentNullException")]
    public void FormatDocument_NullChatExport_ThrowsArgumentNullException()
    {
        var formatter = new TestDocumentFormatter();

        var exception = Assert.Throws<ArgumentNullException>(() => formatter.FormatDocument(null!));

        Assert.Equal("chatExport", exception.ParamName);
    }

    private sealed class TestDocumentFormatter : IDocumentFormatter
    {
        public string FormatDocument(ChatExport chatExport)
        {
            ArgumentNullException.ThrowIfNull(chatExport);

            return $"Document with {chatExport.MessageCount} messages";
        }

        public string FormatMessage(ChatMessage message)
        {
            throw new NotSupportedException(
                "Document-level formatters do not support individual message formatting.");
        }
    }
}
