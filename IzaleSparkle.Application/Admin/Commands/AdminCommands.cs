using AutoMapper;
using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Enums;
using IzaleSparkle.Domain.Exceptions;

namespace IzaleSparkle.Application.Admin.Commands;

// ── SHARED MAPPER — must be a real static class, not file-scoped ──────────
internal static class AdminMapper
{
    internal static AdminProductResponse ToResponse(Product p) => new(
        p.Id, p.Name, p.Slug, p.Description, p.Material,
        p.Price.Amount,
        p.OldPrice?.Amount,
        p.Category.ToString().ToLower(),
        p.Badge?.ToString()?.ToLower(),
        p.Stars,
        p.ImageUrl,
        p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
        p.IsActive,
        p.StockLevel,
        p.IsOnSale,
        p.SavePercent,
        p.CreatedAt,
        p.CostPrice?.Amount,
        p.MarginPercent,
        p.ReorderPoint,
        p.LeadTimeDays,
        p.Supplier,
        p.SupplierSku,
        p.LowStock,
        p.OutOfStock,
        p.TotalPurchased);
}

// ── CREATE PRODUCT ────────────────────────────────────────────────────────
public record CreateProductCommand(CreateProductRequest Request)
    : IRequest<AdminProductResponse>;

public sealed class CreateProductCommandHandler(IUnitOfWork uow)
    : IRequestHandler<CreateProductCommand, AdminProductResponse>
{
    public async Task<AdminProductResponse> Handle(CreateProductCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;

        var category = Enum.TryParse<ProductCategory>(req.Category, true, out var cat)
            ? cat : ProductCategory.Rings;
        var badge = string.IsNullOrWhiteSpace(req.Badge) ? (BadgeType?)null
            : Enum.TryParse<BadgeType>(req.Badge, true, out var b) ? b : (BadgeType?)null;

        var product = Product.Create(
            req.Name, req.Description, req.Material,
            req.Price, category, req.ImageUrl, req.Stars,
            req.OldPrice, badge, req.CostPrice);

        foreach (var (url, i) in req.Images.Select((u, i) => (u, i)))
            product.AddImage(url, i == 0);

        await uow.Products.AddAsync(product, ct);
        await uow.SaveChangesAsync(ct);

        return AdminMapper.ToResponse(product);
    }
}

// ── UPDATE PRODUCT ────────────────────────────────────────────────────────
public record UpdateProductCommand(UpdateProductRequest Request)
    : IRequest<AdminProductResponse>;

public sealed class UpdateProductCommandHandler(IUnitOfWork uow)
    : IRequestHandler<UpdateProductCommand, AdminProductResponse>
{
    public async Task<AdminProductResponse> Handle(UpdateProductCommand cmd, CancellationToken ct)
    {
        var req = cmd.Request;
        var product = await uow.Products.GetByIdAsync(req.Id, ct)
            ?? throw new NotFoundException(nameof(Product), req.Id);

        var cat   = Enum.TryParse<ProductCategory>(req.Category, true, out var c) ? c : ProductCategory.Rings;
        var badge = string.IsNullOrWhiteSpace(req.Badge) ? (BadgeType?)null
            : Enum.TryParse<BadgeType>(req.Badge, true, out var b) ? b : (BadgeType?)null;

        Set(product, "Name",        req.Name);
        Set(product, "Description", req.Description);
        Set(product, "Material",    req.Material);
        Set(product, "ImageUrl",    req.ImageUrl);
        Set(product, "Stars",       req.Stars);
        Set(product, "IsActive",    req.IsActive);
        Set(product, "Category",    cat);
        Set(product, "Badge",       badge);
        Set(product, "Price",       new Domain.ValueObjects.Money(req.Price));
        Set(product, "OldPrice",    req.OldPrice.HasValue
            ? new Domain.ValueObjects.Money(req.OldPrice.Value) : null);
        Set(product, "CostPrice",   req.CostPrice.HasValue
            ? new Domain.ValueObjects.Money(req.CostPrice.Value) : null);
        Set(product, "IsVatApplicable", req.IsVatApplicable);
        // Stock settings (passed through UpdateProductRequest optional fields)
        // These are updated via separate UpdateProductStockSettings endpoint

        uow.Products.Update(product);
        await uow.SaveChangesAsync(ct);

        return AdminMapper.ToResponse(product);
    }

    static void Set(object obj, string prop, object? val)
    {
        var p = obj.GetType().GetProperty(prop,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public   |
            System.Reflection.BindingFlags.NonPublic);
        p?.SetValue(obj, val);
    }
}

// ── DELETE PRODUCT ────────────────────────────────────────────────────────
public record DeleteProductCommand(int Id) : IRequest<bool>;

public sealed class DeleteProductCommandHandler(IUnitOfWork uow)
    : IRequestHandler<DeleteProductCommand, bool>
{
    public async Task<bool> Handle(DeleteProductCommand cmd, CancellationToken ct)
    {
        var product = await uow.Products.GetByIdAsync(cmd.Id, ct)
            ?? throw new NotFoundException(nameof(Product), cmd.Id);
        uow.Products.Delete(product);
        await uow.SaveChangesAsync(ct);
        return true;
    }
}

// ── GET ALL PRODUCTS (admin — includes inactive) ──────────────────────────
public record GetAllProductsAdminQuery() : IRequest<IEnumerable<AdminProductResponse>>;

public sealed class GetAllProductsAdminQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetAllProductsAdminQuery, IEnumerable<AdminProductResponse>>
{
    public async Task<IEnumerable<AdminProductResponse>> Handle(
        GetAllProductsAdminQuery req, CancellationToken ct)
    {
        var all = await uow.Products.GetAllAsync(ct);
        return all.Select(AdminMapper.ToResponse);
    }
}

// ── DASHBOARD ─────────────────────────────────────────────────────────────
public record GetAdminDashboardQuery() : IRequest<AdminDashboardResponse>;

public sealed class GetAdminDashboardQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetAdminDashboardQuery, AdminDashboardResponse>
{
    public async Task<AdminDashboardResponse> Handle(
        GetAdminDashboardQuery req, CancellationToken ct)
    {
        var products = (await uow.Products.GetAllAsync(ct)).ToList();
        var orders   = (await uow.Orders.GetAllAsync(ct)).ToList();

        var recentProducts = products
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(AdminMapper.ToResponse)
            .ToList();

        var recentOrders = orders
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .Select(o => new OrderResponse(
                o.Id, o.OrderNumber, o.CustomerEmail,
                o.Status.ToString(),
                o.Subtotal.Amount, o.ShippingCost.Amount,
                o.Vat.Amount, o.Discount.Amount, o.Total.Amount,
                o.PromoCode, o.CreatedAt, new List<OrderItemResponse>()))
            .ToList();

        return new AdminDashboardResponse(
            TotalProducts:  products.Count,
            ActiveProducts: products.Count(p => p.IsActive),
            TotalOrders:    orders.Count,
            PendingOrders:  orders.Count(o => o.Status == OrderStatus.Pending),
            TotalRevenue:   orders.Sum(o => o.Total.Amount),
            RecentProducts: recentProducts,
            RecentOrders:   recentOrders);
    }
}
