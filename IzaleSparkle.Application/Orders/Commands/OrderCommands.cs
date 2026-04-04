using AutoMapper;
using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Enums;
using IzaleSparkle.Domain.Exceptions;
using IzaleSparkle.Domain.ValueObjects;

namespace IzaleSparkle.Application.Orders.Commands;

// ── PLACE ORDER COMMAND ───────────────────────────────────────
public record PlaceOrderCommand(PlaceOrderRequest Request) : IRequest<OrderResponse>;

public sealed class PlaceOrderCommandHandler(
    IUnitOfWork uow,
    IMapper mapper,
    IEmailService email)
    : IRequestHandler<PlaceOrderCommand, OrderResponse>
{
    private static readonly Dictionary<string, decimal> ShippingCosts = new()
    {
        ["standard"] = 0m,
        ["express"]  = 12m,
        ["sameday"]  = 28m,
    };

    public async Task<OrderResponse> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;

        // 1. Validate promo code against DB
        decimal discount = 0;
        DiscountCode? promoCode = null;
        if (!string.IsNullOrWhiteSpace(req.PromoCode))
        {
            promoCode = await uow.DiscountCodes.GetByCodeAsync(req.PromoCode, ct);
            if (promoCode == null || !promoCode.IsValid)
                throw new BusinessRuleException("PromoCode", $"Code '{req.PromoCode}' is not valid or has expired.");
        }

        // 2. Resolve shipping
        var shippingKey  = req.ShippingTier.ToLower().Replace(" ", "");
        var shippingCost = ShippingCosts.GetValueOrDefault(shippingKey, 0m);

        // 3. Parse enums
        var payment  = Enum.TryParse<PaymentMethod>(req.PaymentMethod.Replace(" ", "").Replace("/", ""), true, out var pm)  ? pm  : PaymentMethod.Card;
        var shipping = Enum.TryParse<ShippingTier>(req.ShippingTier.Replace(" ", ""),  true, out var st)  ? st  : ShippingTier.Standard;

        // 4. Build address value object
        var address = new Address(req.FirstName, req.LastName, req.AddressLine1,
                                  req.AddressLine2, req.City, req.Postcode, req.Country);

        // 5. Calculate discount (10% off subtotal)
        var itemsTotal = 0m;
        var orderItems = new List<(Domain.Entities.Product product, int qty, MetalType metal, string? size)>();

        foreach (var item in req.Items)
        {
            var product = await uow.Products.GetByIdAsync(item.ProductId, ct)
                ?? throw new NotFoundException(nameof(Domain.Entities.Product), item.ProductId);

            var metal = Enum.TryParse<MetalType>(
                item.Metal.Replace(" ","").Replace("K","K"), true, out var mt)
                ? mt : MetalType.WhiteGold18K;

            orderItems.Add((product, item.Quantity, metal, item.Size));
            itemsTotal += product.Price.Amount * item.Quantity;
        }

        if (promoCode != null && promoCode.IsValid)
        {
            discount = promoCode.Apply(itemsTotal);
            promoCode.TimesUsed++;
            uow.DiscountCodes.Update(promoCode);
        }

        // 6. Calculate VAT per product (only on VAT-applicable items)
        var vatTotal = Math.Round(
            orderItems.Sum(x => x.product.IsVatApplicable
                ? x.product.Price.Amount * x.qty * 0.20m
                : 0m), 2);

        // 6. Create order aggregate
        var order = Order.Create(req.Email, address, payment, shipping, shippingCost,
                                 req.PromoCode, discount, req.GiftMessage, vatTotal);

        foreach (var (product, qty, metal, size) in orderItems)
            order.AddItem(product, qty, metal, size);

        // 7. Persist
        await uow.Orders.AddAsync(order, ct);
        await uow.SaveChangesAsync(ct);

        // 8. Build notification payload
        var emailData = new OrderEmailData(
            CustomerEmail:   req.Email,
            CustomerName:    $"{req.FirstName} {req.LastName}",
            OrderNumber:     order.OrderNumber,
            Status:          order.Status.ToString(),
            Subtotal:        order.Subtotal.Amount,
            Shipping:        order.ShippingCost.Amount,
            Vat:             order.Vat.Amount,
            Discount:        order.Discount.Amount,
            Total:           order.Total.Amount,
            PromoCode:       order.PromoCode,
            ShippingAddress: $"{req.FirstName} {req.LastName}\n{req.AddressLine1}\n{(string.IsNullOrEmpty(req.AddressLine2) ? "" : req.AddressLine2 + "\n")}{req.City}\n{req.Postcode}\n{req.Country}",
            ShippingTier:    req.ShippingTier,
            Items: orderItems.Select(x => new OrderEmailItem(
                x.product.Name,
                x.product.Material,
                x.qty,
                x.product.Price.Amount,
                x.product.Price.Amount * x.qty)).ToList()
        );

        // Fire and forget — never fail the order due to notification errors
        _ = Task.Run(async () =>
        {
            // Confirmation to customer
            try { await email.SendOrderConfirmationAsync(emailData, CancellationToken.None); }
            catch { /* logged inside service */ }
            // Order details to admin
            try { await email.SendAdminOrderNotificationAsync(emailData, CancellationToken.None); }
            catch { /* logged inside service */ }
        }, CancellationToken.None);

        return mapper.Map<OrderResponse>(order);
    }
}

// ── GET ORDER BY NUMBER ───────────────────────────────────────
public record GetOrderByNumberQuery(string OrderNumber) : IRequest<OrderResponse>;

public sealed class GetOrderByNumberQueryHandler(
    IUnitOfWork uow,
    IMapper mapper)
    : IRequestHandler<GetOrderByNumberQuery, OrderResponse>
{
    public async Task<OrderResponse> Handle(GetOrderByNumberQuery req, CancellationToken ct)
    {
        var order = await uow.Orders.GetByOrderNumberAsync(req.OrderNumber, ct)
            ?? throw new NotFoundException(nameof(Order), req.OrderNumber);
        return mapper.Map<OrderResponse>(order);
    }
}

// ── GET ORDERS BY EMAIL ───────────────────────────────────────
public record GetOrdersByEmailQuery(string Email) : IRequest<IEnumerable<OrderResponse>>;

public sealed class GetOrdersByEmailQueryHandler(IUnitOfWork uow, IMapper mapper)
    : IRequestHandler<GetOrdersByEmailQuery, IEnumerable<OrderResponse>>
{
    public async Task<IEnumerable<OrderResponse>> Handle(
        GetOrdersByEmailQuery req, CancellationToken ct)
    {
        var orders = await uow.Orders.GetByEmailAsync(req.Email, ct);
        return orders
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => mapper.Map<OrderResponse>(o));
    }
}
