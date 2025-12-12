using WhatsAppArchiver.Application.Commands;
using WhatsAppArchiver.Application.Validators;
using WhatsAppArchiver.Domain.Formatting;

namespace WhatsAppArchiver.Application.Tests.Validators;

public class UploadToGoogleDocsCommandValidatorTests
{
    private readonly UploadToGoogleDocsCommandValidator _validator;

    public UploadToGoogleDocsCommandValidatorTests()
    {
        _validator = new UploadToGoogleDocsCommandValidator();
    }

    [Fact(DisplayName = "Validate with valid command returns valid result")]
    public void Validate_ValidCommand_ReturnsValidResult()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt",
            "John Doe",
            "doc-123",
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact(DisplayName = "Validate with empty file path returns invalid result")]
    public void Validate_EmptyFilePath_ReturnsInvalidResult()
    {
        var command = new UploadToGoogleDocsCommand(
            "",
            "John Doe",
            "doc-123",
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadToGoogleDocsCommand.FilePath));
    }

    [Fact(DisplayName = "Validate with null file path returns invalid result")]
    public void Validate_NullFilePath_ReturnsInvalidResult()
    {
        var command = new UploadToGoogleDocsCommand(
            null!,
            "John Doe",
            "doc-123",
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadToGoogleDocsCommand.FilePath));
    }

    [Fact(DisplayName = "Validate with empty sender returns invalid result")]
    public void Validate_EmptySender_ReturnsInvalidResult()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt",
            "",
            "doc-123",
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadToGoogleDocsCommand.Sender));
    }

    [Fact(DisplayName = "Validate with null sender returns invalid result")]
    public void Validate_NullSender_ReturnsInvalidResult()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt",
            null!,
            "doc-123",
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadToGoogleDocsCommand.Sender));
    }

    [Fact(DisplayName = "Validate with whitespace sender returns invalid result")]
    public void Validate_WhitespaceSender_ReturnsInvalidResult()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt",
            "   ",
            "doc-123",
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadToGoogleDocsCommand.Sender));
    }

    [Fact(DisplayName = "Validate with empty document ID returns invalid result")]
    public void Validate_EmptyDocumentId_ReturnsInvalidResult()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt",
            "John Doe",
            "",
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadToGoogleDocsCommand.DocumentId));
    }

    [Fact(DisplayName = "Validate with null document ID returns invalid result")]
    public void Validate_NullDocumentId_ReturnsInvalidResult()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt",
            "John Doe",
            null!,
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadToGoogleDocsCommand.DocumentId));
    }

    [Theory(DisplayName = "Validate with valid formatter type returns valid result")]
    [InlineData(MessageFormatType.Default)]
    [InlineData(MessageFormatType.Compact)]
    [InlineData(MessageFormatType.Verbose)]
    [InlineData(MessageFormatType.MarkdownDocument)]
    public void Validate_ValidFormatterType_ReturnsValidResult(MessageFormatType formatterType)
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt",
            "John Doe",
            "doc-123",
            formatterType);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact(DisplayName = "Validate with invalid formatter type returns invalid result")]
    public void Validate_InvalidFormatterType_ReturnsInvalidResult()
    {
        var command = new UploadToGoogleDocsCommand(
            "/path/to/chat.txt",
            "John Doe",
            "doc-123",
            (MessageFormatType)999);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UploadToGoogleDocsCommand.FormatterType));
    }

    [Fact(DisplayName = "Validate with multiple errors returns all errors")]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        var command = new UploadToGoogleDocsCommand(
            "",
            "",
            "",
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3);
    }

    [Fact(DisplayName = "Validate with path containing invalid characters returns invalid result")]
    public void Validate_PathWithInvalidCharacters_ReturnsInvalidResult()
    {
        var invalidPath = "/path/to/chat" + '\0' + ".txt";
        var command = new UploadToGoogleDocsCommand(
            invalidPath,
            "John Doe",
            "doc-123",
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e =>
            e.PropertyName == nameof(UploadToGoogleDocsCommand.FilePath) &&
            e.ErrorMessage.Contains("invalid characters"));
    }

    [Theory(DisplayName = "Validate with valid path formats returns valid result")]
    [InlineData("/home/user/chat.txt")]
    [InlineData("C:\\Users\\chat.txt")]
    [InlineData("./relative/path/chat.txt")]
    [InlineData("chat.txt")]
    public void Validate_ValidPathFormats_ReturnsValidResult(string filePath)
    {
        var command = new UploadToGoogleDocsCommand(
            filePath,
            "John Doe",
            "doc-123",
            MessageFormatType.Default);

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }
}
