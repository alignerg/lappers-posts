using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class DefaultMessageFormatterTests
{
    private readonly DefaultMessageFormatter _formatter = new();

    [Fact(DisplayName = "FormatMessage with valid message returns expected format")]
    public void FormatMessage_ValidMessage_ReturnsExpectedFormat()
    {
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var message = ChatMessage.Create(timestamp, "John Doe", "Hello, World!");

        var result = _formatter.FormatMessage(message);

        Assert.Equal("[15/01/2024, 10:30:00] John Doe: Hello, World!", result);
    }

    [Fact(DisplayName = "FormatMessage with null message throws ArgumentNullException")]
    public void FormatMessage_NullMessage_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _formatter.FormatMessage(null!));

        Assert.Equal("message", exception.ParamName);
    }

    [Fact(DisplayName = "FormatMessage includes DD/MM/YYYY, HH:mm:ss timestamp format")]
    public void FormatMessage_ValidMessage_IncludesDdMmYyyyTimestamp()
    {
        var timestamp = new DateTimeOffset(2024, 6, 20, 14, 45, 30, TimeSpan.FromHours(2));
        var message = ChatMessage.Create(timestamp, "Alice", "Test message");

        var result = _formatter.FormatMessage(message);

        Assert.Contains("20/06/2024, 14:45:30", result);
    }

    [Fact(DisplayName = "FormatMessage preserves sender and content exactly")]
    public void FormatMessage_ValidMessage_PreservesSenderAndContent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var sender = "Test User With Spaces";
        var content = "Message with special chars: @#$%";
        var message = ChatMessage.Create(timestamp, sender, content);

        var result = _formatter.FormatMessage(message);

        Assert.Contains(sender, result);
        Assert.Contains(content, result);
    }
}
