using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Enums;
using IzaleSparkle.Domain.Exceptions;

namespace IzaleSparkle.Application.Admin.Commands;

// ── SHARED MAPPER ─────────────────────────────────────────────
internal static class OrderAdminMapper
{
    internal static AdminOrderResponse ToResponse(Order o) => new(
        Id:            o.Id,
        OrderNumber:   o.OrderNumber,
        CustomerEmail: o.CustomerEmail,
        Status:        o.Status.ToString(),
        PaymentMethod: o.PaymentMethod.ToString(),
        ShippingTier:  o.ShippingTier.ToString(),
        Subtotal:      o.Subtotal.Amount,
        Shipping:      o.ShippingCost.Amount,
        Vat:           o.Vat.Amount,
        Discount:      o.Discount.Amount,
        Total:         o.Total.Amount,
        PromoCode:     o.PromoCode,
        AdminNotes:    o.Notes,
        TrackingNumber: o.TrackingNumber,
        CreatedAt:     o.CreatedAt,
        ShipFirstName: o.ShippingAddress.FirstName,
        ShipLastName:  o.ShippingAddress.LastName,
        ShipLine1:     o.ShippingAddress.Line1,
        ShipLine2:     o.ShippingAddress.Line2,
        ShipCity:      o.ShippingAddress.City,
        ShipPostcode:  o.ShippingAddress.Postcode,
        ShipCountry:   o.ShippingAddress.Country,
        Items: o.Items.Select(i => new OrderItemResponse(
            ProductId:   i.ProductId,
            ProductName: i.ProductName,
            ImageUrl:    i.ImageUrl,
            UnitPrice:   i.UnitPrice.Amount,
            Quantity:    i.Quantity,
            LineTotal:   i.LineTotal.Amount,
            Metal:       i.Metal.ToString(),
            Size:        i.Size)).ToList());
}

// ── GET ALL ORDERS (admin) ────────────────────────────────────
public record GetAllOrdersAdminQuery(
    string?   Status = null,
    string?   Search = null,
    DateTime? From   = null,
    DateTime? To     = null
) : IRequest<OrdersReportResponse>;

public sealed class GetAllOrdersAdminQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetAllOrdersAdminQuery, OrdersReportResponse>
{
    public async Task<OrdersReportResponse> Handle(
        GetAllOrdersAdminQuery req, CancellationToken ct)
    {
        var all = (await uow.Orders.GetAllAsync(ct)).ToList();

        var filtered = all.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(req.Status))
            filtered = filtered.Where(o =>
                o.Status.ToString().Equals(req.Status, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(req.Search))
            filtered = filtered.Where(o =>
                o.OrderNumber.Contains(req.Search, StringComparison.OrdinalIgnoreCase) ||
                o.CustomerEmail.Contains(req.Search, StringComparison.OrdinalIgnoreCase));
        if (req.From.HasValue)
            filtered = filtered.Where(o => o.CreatedAt >= req.From.Value);
        if (req.To.HasValue)
            filtered = filtered.Where(o => o.CreatedAt <= req.To.Value.AddDays(1));

        var today = DateTime.UtcNow.Date;

        return new OrdersReportResponse(
            TotalOrders:      all.Count,
            PendingOrders:    all.Count(o => o.Status == OrderStatus.Pending),
            ProcessingOrders: all.Count(o => o.Status == OrderStatus.Processing),
            ShippedOrders:    all.Count(o => o.Status == OrderStatus.Shipped),
            DeliveredOrders:  all.Count(o => o.Status == OrderStatus.Delivered),
            CancelledOrders:  all.Count(o => o.Status == OrderStatus.Cancelled),
            TotalRevenue:     all
                .Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Refunded)
                .Sum(o => o.Total.Amount),
            TodayRevenue:     all
                .Where(o => o.CreatedAt.Date == today
                         && o.Status != OrderStatus.Cancelled
                         && o.Status != OrderStatus.Refunded)
                .Sum(o => o.Total.Amount),
            Orders: filtered
                .OrderByDescending(o => o.CreatedAt)
                .Select(OrderAdminMapper.ToResponse)
                .ToList());
    }
}

// ── GET SINGLE ORDER (admin) ──────────────────────────────────
public record GetOrderAdminQuery(int Id) : IRequest<AdminOrderResponse>;

public sealed class GetOrderAdminQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetOrderAdminQuery, AdminOrderResponse>
{
    public async Task<AdminOrderResponse> Handle(
        GetOrderAdminQuery req, CancellationToken ct)
    {
        var order = await uow.Orders.GetByIdAsync(req.Id, ct)
            ?? throw new NotFoundException(nameof(Order), req.Id);
        return OrderAdminMapper.ToResponse(order);
    }
}

// ── UPDATE ORDER STATUS ───────────────────────────────────────
public record UpdateOrderStatusCommand(UpdateOrderStatusRequest Request)
    : IRequest<AdminOrderResponse>;

public sealed class UpdateOrderStatusCommandHandler(IUnitOfWork uow, IEmailService email)
    : IRequestHandler<UpdateOrderStatusCommand, AdminOrderResponse>
{
    public async Task<AdminOrderResponse> Handle(
        UpdateOrderStatusCommand cmd, CancellationToken ct)
    {
        var req   = cmd.Request;
        var order = await uow.Orders.GetByIdAsync(req.Id, ct)
            ?? throw new NotFoundException(nameof(Order), req.Id);

        if (Enum.TryParse<OrderStatus>(req.Status, true, out var status))
            order.UpdateStatus(status);

        if (req.AdminNotes != null)
            SetPrivate(order, "Notes", req.AdminNotes);

        // Apply discount code override if provided
        if (!string.IsNullOrEmpty(req.DiscountCode) && req.DiscountAmount.HasValue)
        {
            SetPrivate(order, "PromoCode", req.DiscountCode);
            SetPrivate(order, "Discount",  new IzaleSparkle.Domain.ValueObjects.Money(req.DiscountAmount.Value));
        }

        // Set tracking number if provided
        if (!string.IsNullOrEmpty(req.TrackingNumber))
            SetPrivate(order, "TrackingNumber", req.TrackingNumber);

        var previousStatus = order.Status.ToString();
        order.UpdatedAt = DateTime.UtcNow;
        uow.Orders.Update(order);
        await uow.SaveChangesAsync(ct);

        // Send status update email if status changed
        var newStatus = req.Status;
        var notifyStatuses = new[] { "PaymentReceived","Processing","Shipped","Delivered","Cancelled","Refunded" };
        if (Array.IndexOf(notifyStatuses, newStatus) >= 0)
        {
            var customerName = $"{order.ShippingAddress.FirstName} {order.ShippingAddress.LastName}";
            _ = Task.Run(async () =>
            {
                try { await email.SendOrderStatusUpdateAsync(
                    order.CustomerEmail, customerName, order.OrderNumber,
                    newStatus, order.TrackingNumber, CancellationToken.None); }
                catch { /* logged inside service */ }
            }, CancellationToken.None);
        }

        return OrderAdminMapper.ToResponse(order);
    }

    static void SetPrivate(object obj, string prop, object? val)
    {
        var p = obj.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public   |
            System.Reflection.BindingFlags.NonPublic);
        p?.SetValue(obj, val);
    }
}

// ── RESEND INVOICE TO CUSTOMER ────────────────────────────────
public record ResendInvoiceCommand(int OrderId) : IRequest<bool>;

public sealed class ResendInvoiceCommandHandler(IUnitOfWork uow, IEmailService email)
    : IRequestHandler<ResendInvoiceCommand, bool>
{
    public async Task<bool> Handle(ResendInvoiceCommand cmd, CancellationToken ct)
    {
        var order = await uow.Orders.GetByIdAsync(cmd.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), cmd.OrderId);

        var data = new OrderEmailData(
            CustomerEmail:   order.CustomerEmail,
            CustomerName:    $"{order.ShippingAddress.FirstName} {order.ShippingAddress.LastName}",
            OrderNumber:     order.OrderNumber,
            Status:          order.Status.ToString(),
            Subtotal:        order.Subtotal.Amount,
            Shipping:        order.ShippingCost.Amount,
            Vat:             order.Vat.Amount,
            Discount:        order.Discount.Amount,
            Total:           order.Total.Amount,
            PromoCode:       order.PromoCode,
            ShippingAddress: $"{order.ShippingAddress.FirstName} {order.ShippingAddress.LastName}\n{order.ShippingAddress.Line1}\n{(string.IsNullOrEmpty(order.ShippingAddress.Line2) ? "" : order.ShippingAddress.Line2 + "\n")}{order.ShippingAddress.City}\n{order.ShippingAddress.Postcode}\n{order.ShippingAddress.Country}",
            ShippingTier:    order.ShippingTier.ToString(),
            Items: order.Items.Select(i => new OrderEmailItem(
                i.ProductName, i.Metal.ToString(), i.Quantity,
                i.UnitPrice.Amount, i.LineTotal.Amount)).ToList());

        await email.SendOrderConfirmationAsync(data, ct);
        return true;
    }
}

// ── GENERATE INVOICE PDF BYTES (for admin download) ───────────
public record GenerateInvoicePdfQuery(int OrderId) : IRequest<(byte[] Bytes, string Filename)>;

public sealed class GenerateInvoicePdfQueryHandler(IUnitOfWork uow, IEmailService email)
    : IRequestHandler<GenerateInvoicePdfQuery, (byte[] Bytes, string Filename)>
{
    public async Task<(byte[] Bytes, string Filename)> Handle(
        GenerateInvoicePdfQuery req, CancellationToken ct)
    {
        var order = await uow.Orders.GetByIdAsync(req.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), req.OrderId);

        var data = new OrderEmailData(
            CustomerEmail:   order.CustomerEmail,
            CustomerName:    $"{order.ShippingAddress.FirstName} {order.ShippingAddress.LastName}",
            OrderNumber:     order.OrderNumber,
            Status:          order.Status.ToString(),
            Subtotal:        order.Subtotal.Amount,
            Shipping:        order.ShippingCost.Amount,
            Vat:             order.Vat.Amount,
            Discount:        order.Discount.Amount,
            Total:           order.Total.Amount,
            PromoCode:       order.PromoCode,
            ShippingAddress: $"{order.ShippingAddress.FirstName} {order.ShippingAddress.LastName}\n{order.ShippingAddress.Line1}\n{(string.IsNullOrEmpty(order.ShippingAddress.Line2) ? "" : order.ShippingAddress.Line2 + "\n")}{order.ShippingAddress.City}\n{order.ShippingAddress.Postcode}\n{order.ShippingAddress.Country}",
            ShippingTier:    order.ShippingTier.ToString(),
            Items: order.Items.Select(i => new OrderEmailItem(
                i.ProductName, i.Metal.ToString(), i.Quantity,
                i.UnitPrice.Amount, i.LineTotal.Amount)).ToList());

        var bytes = await email.GenerateInvoicePdfAsync(data, ct);
        return (bytes, $"Izale-Sparkle-Invoice-{order.OrderNumber}.pdf");
    }
}

// ── MODIFY ORDER ITEMS ────────────────────────────────────────
public record ModifyOrderItemsCommand(int OrderId, List<ModifyOrderItemRequest> Changes)
    : IRequest<AdminOrderResponse>;

public sealed class ModifyOrderItemsCommandHandler(IUnitOfWork uow)
    : IRequestHandler<ModifyOrderItemsCommand, AdminOrderResponse>
{
    public async Task<AdminOrderResponse> Handle(
        ModifyOrderItemsCommand cmd, CancellationToken ct)
    {
        var order = await uow.Orders.GetByIdAsync(cmd.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), cmd.OrderId);

        foreach (var change in cmd.Changes)
        {
            if (change.Quantity <= 0)
                order.RemoveItem(change.ProductId);
            else
                order.UpdateItemQty(change.ProductId, change.Quantity);
        }

        order.UpdatedAt = DateTime.UtcNow;
        uow.Orders.Update(order);
        await uow.SaveChangesAsync(ct);
        return OrderAdminMapper.ToResponse(order);
    }
}

// ── ADD ITEM TO ORDER ─────────────────────────────────────────
public record AddItemToOrderCommand(int OrderId, int ProductId, int Quantity, string Metal)
    : IRequest<AdminOrderResponse>;

public sealed class AddItemToOrderCommandHandler(IUnitOfWork uow)
    : IRequestHandler<AddItemToOrderCommand, AdminOrderResponse>
{
    public async Task<AdminOrderResponse> Handle(
        AddItemToOrderCommand cmd, CancellationToken ct)
    {
        var order = await uow.Orders.GetByIdAsync(cmd.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), cmd.OrderId);

        var product = await uow.Products.GetByIdAsync(cmd.ProductId, ct)
            ?? throw new NotFoundException(nameof(IzaleSparkle.Domain.Entities.Product), cmd.ProductId);

        var metal = Enum.TryParse<IzaleSparkle.Domain.Enums.MetalType>(
            cmd.Metal.Replace(" ", ""), true, out var m)
            ? m : IzaleSparkle.Domain.Enums.MetalType.YellowGold18K;

        order.AddItem(product, cmd.Quantity, metal, null);
        order.UpdatedAt = DateTime.UtcNow;
        uow.Orders.Update(order);
        await uow.SaveChangesAsync(ct);
        return OrderAdminMapper.ToResponse(order);
    }
}

// ── CANCEL ORDER (admin or customer) ─────────────────────────
public record CancelOrderCommand(int OrderId, string Reason, bool IsCustomer = false)
    : IRequest<AdminOrderResponse>;

public sealed class CancelOrderCommandHandler(IUnitOfWork uow)
    : IRequestHandler<CancelOrderCommand, AdminOrderResponse>
{
    public async Task<AdminOrderResponse> Handle(
        CancelOrderCommand cmd, CancellationToken ct)
    {
        var order = await uow.Orders.GetByIdAsync(cmd.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), cmd.OrderId);

        // Customers can only cancel Pending orders
        if (cmd.IsCustomer && order.Status != IzaleSparkle.Domain.Enums.OrderStatus.Pending)
            throw new IzaleSparkle.Domain.Exceptions.BusinessRuleException(
                "Cancel", "Only pending orders can be cancelled.");

        order.UpdateStatus(IzaleSparkle.Domain.Enums.OrderStatus.Cancelled);

        var notes = string.IsNullOrWhiteSpace(cmd.Reason)
            ? "Cancelled" : $"Cancelled: {cmd.Reason}";
        SetPrivate(order, "Notes", notes);

        order.UpdatedAt = DateTime.UtcNow;
        uow.Orders.Update(order);
        await uow.SaveChangesAsync(ct);
        return OrderAdminMapper.ToResponse(order);
    }

    static void SetPrivate(object obj, string prop, object? val)
    {
        var p = obj.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic);
        p?.SetValue(obj, val);
    }
}
