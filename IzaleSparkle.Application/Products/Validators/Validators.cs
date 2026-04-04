using FluentValidation;
using IzaleSparkle.Application.Products.Queries;

namespace IzaleSparkle.Application.Products.Validators;

public class GetProductsQueryValidator : AbstractValidator<GetProductsQuery>
{
    public GetProductsQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0).WithMessage("Page must be >= 1.");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("PageSize must be 1–100.");
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0).When(x => x.MinPrice.HasValue);
        RuleFor(x => x.MaxPrice).GreaterThan(0).When(x => x.MaxPrice.HasValue);
        RuleFor(x => x)
            .Must(x => x.MinPrice == null || x.MaxPrice == null || x.MinPrice <= x.MaxPrice)
            .WithMessage("MinPrice must be <= MaxPrice.");
    }
}
