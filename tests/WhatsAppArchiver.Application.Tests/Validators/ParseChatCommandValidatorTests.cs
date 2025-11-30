using WhatsAppArchiver.Application.Commands;
using WhatsAppArchiver.Application.Validators;

namespace WhatsAppArchiver.Application.Tests.Validators;

public class ParseChatCommandValidatorTests
{
    private readonly ParseChatCommandValidator _validator;

    public ParseChatCommandValidatorTests()
    {
        _validator = new ParseChatCommandValidator();
    }

    [Fact(DisplayName = "Validate with valid file path returns valid result")]
    public void Validate_ValidFilePath_ReturnsValidResult()
    {
        var command = new ParseChatCommand("/path/to/chat.txt");

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "Validate with empty file path returns invalid result")]
    public void Validate_EmptyFilePath_ReturnsInvalidResult()
    {
        var command = new ParseChatCommand("");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ParseChatCommand.FilePath));
    }

    [Fact(DisplayName = "Validate with null file path returns invalid result")]
    public void Validate_NullFilePath_ReturnsInvalidResult()
    {
        var command = new ParseChatCommand(null!);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ParseChatCommand.FilePath));
    }

    [Fact(DisplayName = "Validate with whitespace file path returns invalid result")]
    public void Validate_WhitespaceFilePath_ReturnsInvalidResult()
    {
        var command = new ParseChatCommand("   ");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(ParseChatCommand.FilePath));
    }

    [Theory(DisplayName = "Validate with valid path formats returns valid result")]
    [InlineData("/home/user/chat.txt")]
    [InlineData("C:\\Users\\chat.txt")]
    [InlineData("./relative/path/chat.txt")]
    [InlineData("chat.txt")]
    public void Validate_ValidPathFormats_ReturnsValidResult(string filePath)
    {
        var command = new ParseChatCommand(filePath);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "Validate with optional sender filter returns valid result")]
    public void Validate_WithOptionalSenderFilter_ReturnsValidResult()
    {
        var command = new ParseChatCommand("/path/to/chat.txt", "John Doe");

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "Validate with null sender filter returns valid result")]
    public void Validate_NullSenderFilter_ReturnsValidResult()
    {
        var command = new ParseChatCommand("/path/to/chat.txt", null);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "Validate with path containing invalid characters returns invalid result")]
    public void Validate_PathWithInvalidCharacters_ReturnsInvalidResult()
    {
        var invalidPath = "/path/to/chat" + '\0' + ".txt";
        var command = new ParseChatCommand(invalidPath);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(ParseChatCommand.FilePath) &&
            e.ErrorMessage.Contains("invalid characters"));
    }
}
