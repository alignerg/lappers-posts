using WhatsAppArchiver.Domain.Entities;
using WhatsAppArchiver.Domain.Formatting;

namespace WhatsAppArchiver.Domain.Tests.Formatting;

public class VerboseMessageFormatterTests
{
    private readonly VerboseMessageFormatter _formatter = new();

    [Fact(DisplayName = "FormatMessage with valid message returns verbose format")]
    public void FormatMessage_ValidMessage_ReturnsVerboseFormat()
    {
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var message = ChatMessage.Create(timestamp, "John Doe", "Hello, World!");

        var result = _formatter.FormatMessage(message);

        Assert.Contains("Date:", result);
        Assert.Contains("Time:", result);
        Assert.Contains("From:", result);
        Assert.Contains("Message:", result);
    }

    [Fact(DisplayName = "FormatMessage with null message throws ArgumentNullException")]
    public void FormatMessage_NullMessage_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => _formatter.FormatMessage(null!));

        Assert.Equal("message", exception.ParamName);
    }

    [Fact(DisplayName = "FormatMessage includes full date with day of week")]
    public void FormatMessage_ValidMessage_IncludesFullDateWithDayOfWeek()
    {
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        var message = ChatMessage.Create(timestamp, "Alice", "Test");

        var result = _formatter.FormatMessage(message);

        Assert.Contains("Monday", result);
        Assert.Contains("January", result);
        Assert.Contains("15", result);
        Assert.Contains("2024", result);
    }

    [Fact(DisplayName = "FormatMessage includes sender in From field")]
    public void FormatMessage_ValidMessage_IncludesSenderInFromField()
    {
        var sender = "Test Sender Name";
        var message = ChatMessage.Create(DateTimeOffset.UtcNow, sender, "Content");

        var result = _formatter.FormatMessage(message);

        Assert.Contains($"From: {sender}", result);
    }

    [Fact(DisplayName = "FormatMessage includes content in Message field")]
    public void FormatMessage_ValidMessage_IncludesContentInMessageField()
    {
        var content = "This is the message content";
        var message = ChatMessage.Create(DateTimeOffset.UtcNow, "User", content);

        var result = _formatter.FormatMessage(message);

        Assert.Contains($"Message: {content}", result);
    }

    [Fact(DisplayName = "FormatMessage includes timezone offset")]
    public void FormatMessage_ValidMessage_IncludesTimezoneOffset()
    {
        var timestamp = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(5));
        var message = ChatMessage.Create(timestamp, "User", "Content");

        var result = _formatter.FormatMessage(message);

        Assert.Contains("+05:00", result);
    }
}
