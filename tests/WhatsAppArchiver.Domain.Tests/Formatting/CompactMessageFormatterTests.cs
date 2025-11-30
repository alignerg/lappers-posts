using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class CompactMessageFormatterTests
{
    private readonly CompactMessageFormatter _formatter = new();

    [Fact(DisplayName = "FormatMessage with valid message returns compact format")]
    public void FormatMessage_ValidMessage_ReturnsCompactFormat()
    {
        var message = ChatMessage.Create(DateTimeOffset.UtcNow, "John Doe", "Hello, World!");

        var result = _formatter.FormatMessage(message);

        Assert.Equal("John Doe: Hello, World!", result);
    }

    [Fact(DisplayName = "FormatMessage with null message throws ArgumentNullException")]
    public void FormatMessage_NullMessage_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _formatter.FormatMessage(null!));

        Assert.Equal("message", exception.ParamName);
    }

    [Fact(DisplayName = "FormatMessage does not include timestamp")]
    public void FormatMessage_ValidMessage_DoesNotIncludeTimestamp()
    {
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var message = ChatMessage.Create(timestamp, "Alice", "Test message");

        var result = _formatter.FormatMessage(message);

        Assert.DoesNotContain("2024", result);
        Assert.DoesNotContain("[", result);
        Assert.DoesNotContain("]", result);
    }

    [Fact(DisplayName = "FormatMessage preserves sender and content exactly")]
    public void FormatMessage_ValidMessage_PreservesSenderAndContent()
    {
        var sender = "User Name";
        var content = "Message content here!";
        var message = ChatMessage.Create(DateTimeOffset.UtcNow, sender, content);

        var result = _formatter.FormatMessage(message);

        Assert.Equal($"{sender}: {content}", result);
    }
}
