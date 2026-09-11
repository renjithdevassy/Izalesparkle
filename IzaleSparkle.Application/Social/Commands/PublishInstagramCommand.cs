using MediatR;
using FluentValidation;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Application.Social.Commands;

// ── PUBLISH ONE INSTAGRAM PHOTO POST ─────────────────────────
/// <summary>
/// The image URL here is already absolute — the controller expands the
/// "/uploads/..." path it receives against Site:BaseUrl, because Meta fetches
/// the image from the public internet rather than receiving the bytes.
/// </summary>
public record PublishInstagramCommand(string ImageUrl, string Caption)
    : IRequest<InstagramPublishResult>;

public class PublishInstagramHandler(IInstagramPublisher instagram)
    : IRequestHandler<PublishInstagramCommand, InstagramPublishResult>
{
    public Task<InstagramPublishResult> Handle(PublishInstagramCommand cmd, CancellationToken ct)
        => instagram.PublishPhotoAsync(cmd.ImageUrl, cmd.Caption, ct);
}

public class PublishInstagramValidator : AbstractValidator<PublishInstagramCommand>
{
    public PublishInstagramValidator()
    {
        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("Pick the photo to post.");

        RuleFor(x => x.Caption)
            .NotEmpty().WithMessage("A post needs a caption.")
            .MaximumLength(2200).WithMessage("Instagram captions cap out at 2200 characters.");
    }
}
