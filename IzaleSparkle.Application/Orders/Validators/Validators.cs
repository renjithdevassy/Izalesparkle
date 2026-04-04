using FluentValidation;
using IzaleSparkle.Application.Orders.Commands;

namespace IzaleSparkle.Application.Orders.Validators;

public class PlaceOrderCommandValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderCommandValidator()
    {
        RuleFor(x => x.Request.Email)
            .NotEmpty().EmailAddress().WithMessage("A valid email is required.");
        RuleFor(x => x.Request.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.AddressLine1).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Postcode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Request.Items)
            .NotEmpty().WithMessage("Order must contain at least one item.");
        RuleForEach(x => x.Request.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0);
            item.RuleFor(i => i.Quantity).InclusiveBetween(1, 10);
        });
    }
}
