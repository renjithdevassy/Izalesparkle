using FluentValidation;
using IzaleSparkle.Application.Ai.Commands;

namespace IzaleSparkle.Application.Ai.Validators;

public class GenerateProductContentValidator : AbstractValidator<GenerateProductContentCommand>
{
    private const int MaxImages     = 4;
    private const int MaxBase64Size = 6 * 1024 * 1024; // ~4.5 MB of image once decoded

    public GenerateProductContentValidator()
    {
        RuleFor(x => x.Request)
            .NotNull().WithMessage("Nothing to generate from.");

        RuleFor(x => x.Request)
            .Must(r => (r.Images is { Count: > 0 }) || !string.IsNullOrWhiteSpace(r.ImageUrl))
            .WithMessage("Add at least one photo of the piece — the model writes from what it can see.");

        RuleFor(x => x.Request.Images!)
            .Must(imgs => imgs.Count <= MaxImages)
            .When(x => x.Request.Images is not null)
            .WithMessage($"Send at most {MaxImages} photos per request.");

        RuleForEach(x => x.Request.Images!)
            .ChildRules(img =>
            {
                img.RuleFor(i => i.MimeType)
                   .Must(m => m.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                   .WithMessage("Only image files can be sent to the model.");

                img.RuleFor(i => i.Base64Data)
                   .NotEmpty().WithMessage("An image was empty.")
                   .Must(d => d.Length <= MaxBase64Size)
                   .WithMessage("That photo is too large — resize it under about 4 MB first.");
            })
            .When(x => x.Request.Images is not null);

        RuleFor(x => x.Request.Temperature)
            .InclusiveBetween(0, 2)
            .When(x => x.Request.Temperature.HasValue)
            .WithMessage("Temperature must be between 0 and 2.");

        RuleFor(x => x.Request.Price)
            .GreaterThanOrEqualTo(0)
            .When(x => x.Request.Price.HasValue);
    }
}
