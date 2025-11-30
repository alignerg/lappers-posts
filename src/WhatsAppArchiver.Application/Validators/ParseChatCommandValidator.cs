using FluentValidation;
using WhatsAppArchiver.Application.Commands;

namespace WhatsAppArchiver.Application.Validators;

/// <summary>
/// Validator for <see cref="ParseChatCommand"/>.
/// </summary>
/// <remarks>
/// Validates that the file path is not empty and represents a valid path format.
/// </remarks>
public sealed class ParseChatCommandValidator : AbstractValidator<ParseChatCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParseChatCommandValidator"/> class.
    /// </summary>
    public ParseChatCommandValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty()
            .WithMessage("File path is required.")
            .Must(BeValidPath)
            .WithMessage("File path contains invalid characters.");
    }

    /// <summary>
    /// Validates that the path does not contain invalid characters.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <returns>True if the path is valid; otherwise, false.</returns>
    private static bool BeValidPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        var invalidChars = Path.GetInvalidPathChars();

        return !filePath.Any(c => invalidChars.Contains(c));
    }
}
