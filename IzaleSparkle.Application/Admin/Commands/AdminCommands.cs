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
        Category.ToSlug(p.Category),
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
        p.TotalPurchased,
        p.IsVatApplicable);
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

        var category = Category.ToSlug(req.Category);
        var badge = string.IsNullOrWhiteSpace(req.Badge) ? (BadgeType?)null
            : Enum.TryParse<BadgeType>(req.Badge, true, out var b) ? b : (BadgeType?)null;

        var product = Product.Create(
            req.Name, req.Description, req.Material,
            req.Price, category, req.ImageUrl, req.Stars,
            req.OldPrice, badge, req.CostPrice, req.IsVatApplicable);

        foreach (var (url, i) in req.Images.Select((u, i) => (u, i)))
            product.AddImage(url, i == 0);

        await uow.Products.AddAsync(product, ct);
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

        var cat   = Category.ToSlug(req.Category);
        var badge = string.IsNullOrWhiteSpace(req.Badge) ? (BadgeType?)null
            : Enum.TryParse<BadgeType>(req.Badge, true, out var b) ? b : (BadgeType?)null;

        // Update category explicitly (EF needs direct call to detect change)
        product.UpdateCategory(cat);

        Set(product, "Name",        req.Name);
        Set(product, "Description", req.Description);
        Set(product, "Material",    req.Material);
        Set(product, "ImageUrl",    req.ImageUrl);
        Set(product, "Stars",       req.Stars);
        Set(product, "IsActive",    req.IsActive);
        Set(product, "Badge",       badge);
        Set(product, "Price",       new Domain.ValueObjects.Money(req.Price));
        Set(product, "OldPrice",    req.OldPrice.HasValue
            ? new Domain.ValueObjects.Money(req.OldPrice.Value) : null);
        Set(product, "CostPrice",   req.CostPrice.HasValue
            ? new Domain.ValueObjects.Money(req.CostPrice.Value) : null);
        Set(product, "IsVatApplicable", req.IsVatApplicable);
        // Stock settings (passed through UpdateProductRequest optional fields)
        // These are updated via separate UpdateProductStockSettings endpoint

        // Clear existing images and add new ones from request
        product.ClearImages();
        foreach (var (url, i) in req.Images.Select((u, i) => (u, i)))
            product.AddImage(url, i == 0);

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

public sealed class GetAdminDashboardQueryHandler(IUnitOfWork uow, ISiteAnalytics analytics)
    : IRequestHandler<GetAdminDashboardQuery, AdminDashboardResponse>
{
    public async Task<AdminDashboardResponse> Handle(
        GetAdminDashboardQuery req, CancellationToken ct)
    {
        var products = (await uow.Products.GetAllAsync(ct)).ToList();
        var orders   = (await uow.Orders.GetAllAsync(ct)).ToList();
        var (totalViews, todayViews) = await analytics.GetViewCountsAsync(ct);

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

        // Revenue excludes cancelled & refunded orders — those funds were never realised.
        var revenueOrders = orders
            .Where(o => o.Status != OrderStatus.Cancelled && o.Status != OrderStatus.Refunded)
            .ToList();
        var totalRevenue = revenueOrders.Sum(o => o.Total.Amount);

        var today = DateTime.UtcNow.Date;

        return new AdminDashboardResponse(
            TotalProducts:  products.Count,
            ActiveProducts: products.Count(p => p.IsActive),
            TotalOrders:    orders.Count,
            PendingOrders:  orders.Count(o => o.Status == OrderStatus.Pending),
            TotalRevenue:   totalRevenue,
            RecentProducts: recentProducts,
            RecentOrders:   recentOrders,
            TodayRevenue:   revenueOrders
                .Where(o => o.CreatedAt.Date == today)
                .Sum(o => o.Total.Amount),
            AverageOrderValue: revenueOrders.Count > 0
                ? totalRevenue / revenueOrders.Count : 0m,
            CancelledOrders: orders.Count(o => o.Status == OrderStatus.Cancelled),
            DeliveredOrders: orders.Count(o => o.Status == OrderStatus.Delivered),
            LowStockCount:   products.Count(p => p.LowStock),
            TotalViews:      totalViews,
            TodayViews:      todayViews);
    }
}
