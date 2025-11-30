using FluentValidation;
using WhatsAppArchiver.Application.Commands;

namespace WhatsAppArchiver.Application.Validators;

/// <summary>
/// Validator for <see cref="UploadToGoogleDocsCommand"/>.
/// </summary>
/// <remarks>
/// Validates that the file path, sender, and document ID are properly specified.
/// </remarks>
public sealed class UploadToGoogleDocsCommandValidator : AbstractValidator<UploadToGoogleDocsCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UploadToGoogleDocsCommandValidator"/> class.
    /// </summary>
    public UploadToGoogleDocsCommandValidator()
    {
        RuleFor(x => x.FilePath)
            .NotEmpty()
            .WithMessage("File path is required.")
            .Must(BeValidPath)
            .WithMessage("File path contains invalid characters.");

        RuleFor(x => x.Sender)
            .NotEmpty()
            .WithMessage("Sender is required.");

        RuleFor(x => x.DocumentId)
            .NotEmpty()
            .WithMessage("Document ID is required.");

        RuleFor(x => x.FormatterType)
            .IsInEnum()
            .WithMessage("Invalid formatter type.");
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
