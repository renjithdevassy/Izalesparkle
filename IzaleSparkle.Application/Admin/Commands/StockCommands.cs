using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Exceptions;
using IzaleSparkle.Domain.ValueObjects;

namespace IzaleSparkle.Application.Admin.Commands;

// ── ADD STOCK PURCHASE ────────────────────────────────────────
public record AddStockPurchaseCommand(AddStockPurchaseRequest Request)
    : IRequest<StockLevelResponse>;

public sealed class AddStockPurchaseCommandHandler(IUnitOfWork uow)
    : IRequestHandler<AddStockPurchaseCommand, StockLevelResponse>
{
    public async Task<StockLevelResponse> Handle(AddStockPurchaseCommand cmd, CancellationToken ct)
    {
        var req     = cmd.Request;
        var product = await uow.Products.GetByIdAsync(req.ProductId, ct)
            ?? throw new NotFoundException(nameof(Product), req.ProductId);

        product.AddStockPurchase(
            req.Quantity, req.UnitCost,
            req.Supplier, req.Reference, req.PurchasedOn);

        // Update running StockLevel
        product.StockLevel += req.Quantity;

        uow.Products.Update(product);
        await uow.SaveChangesAsync(ct);

        return StockMapper.ToLevel(product);
    }
}

// ── UPDATE STOCK SETTINGS ─────────────────────────────────────
public record UpdateProductStockSettingsCommand(UpdateProductStockSettingsRequest Request)
    : IRequest<StockLevelResponse>;

public sealed class UpdateProductStockSettingsCommandHandler(IUnitOfWork uow)
    : IRequestHandler<UpdateProductStockSettingsCommand, StockLevelResponse>
{
    public async Task<StockLevelResponse> Handle(
        UpdateProductStockSettingsCommand cmd, CancellationToken ct)
    {
        var req     = cmd.Request;
        var product = await uow.Products.GetByIdAsync(req.ProductId, ct)
            ?? throw new NotFoundException(nameof(Product), req.ProductId);

        SetPrivate(product, "ReorderPoint", req.ReorderPoint);
        SetPrivate(product, "LeadTimeDays", req.LeadTimeDays);
        SetPrivate(product, "Supplier",     req.Supplier);
        SetPrivate(product, "SupplierSku",  req.SupplierSku);

        uow.Products.Update(product);
        await uow.SaveChangesAsync(ct);

        return StockMapper.ToLevel(product);
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

// ── ADJUST STOCK LEVEL MANUALLY ───────────────────────────────
public record AdjustStockCommand(int ProductId, int NewLevel, string? Reason)
    : IRequest<StockLevelResponse>;

public sealed class AdjustStockCommandHandler(IUnitOfWork uow)
    : IRequestHandler<AdjustStockCommand, StockLevelResponse>
{
    public async Task<StockLevelResponse> Handle(AdjustStockCommand cmd, CancellationToken ct)
    {
        var product = await uow.Products.GetByIdAsync(cmd.ProductId, ct)
            ?? throw new NotFoundException(nameof(Product), cmd.ProductId);

        product.StockLevel = cmd.NewLevel;
        uow.Products.Update(product);
        await uow.SaveChangesAsync(ct);

        return StockMapper.ToLevel(product);
    }
}

// ── GET STOCK REPORT ──────────────────────────────────────────
public record GetStockReportQuery(string? Filter = null) : IRequest<StockReportResponse>;

public sealed class GetStockReportQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetStockReportQuery, StockReportResponse>
{
    public async Task<StockReportResponse> Handle(
        GetStockReportQuery req, CancellationToken ct)
    {
        var all = (await uow.Products.GetAllAsync(ct)).ToList();

        var filtered = all.AsEnumerable();
        if (req.Filter == "low")    filtered = all.Where(p => p.LowStock);
        if (req.Filter == "out")    filtered = all.Where(p => p.OutOfStock);
        if (req.Filter == "instock")filtered = all.Where(p => !p.OutOfStock && !p.LowStock);

        return new StockReportResponse(
            TotalProducts:      all.Count,
            InStockProducts:    all.Count(p => p.StockLevel > p.ReorderPoint),
            LowStockProducts:   all.Count(p => p.LowStock),
            OutOfStockProducts: all.Count(p => p.OutOfStock),
            TotalStockValue:    all.Sum(p => p.Price.Amount * p.StockLevel),
            TotalCostValue:     all.Sum(p => (p.CostPrice?.Amount ?? 0) * p.StockLevel),
            Products: filtered
                .OrderBy(p => p.StockLevel)
                .ThenBy(p => p.Name)
                .Select(StockMapper.ToLevel)
                .ToList());
    }
}

// ── GET STOCK PURCHASES (all or per product) ──────────────────
public record GetStockPurchasesQuery(int? ProductId = null) : IRequest<IEnumerable<StockPurchaseResponse>>;

public sealed class GetStockPurchasesQueryHandler(IUnitOfWork uow)
    : IRequestHandler<GetStockPurchasesQuery, IEnumerable<StockPurchaseResponse>>
{
    public async Task<IEnumerable<StockPurchaseResponse>> Handle(
        GetStockPurchasesQuery req, CancellationToken ct)
    {
        var products = (await uow.Products.GetAllAsync(ct)).ToList();

        var purchases = products
            .SelectMany(p => p.StockPurchases.Select(s => new { s, p }));

        if (req.ProductId.HasValue)
            purchases = purchases.Where(x => x.p.Id == req.ProductId.Value);

        return purchases
            .OrderByDescending(x => x.s.PurchasedOn)
            .Select(x => new StockPurchaseResponse(
                x.s.Id,
                x.p.Id,
                x.p.Name,
                x.s.Quantity,
                x.s.UnitCost.Amount,
                x.s.TotalCost.Amount,
                x.s.Supplier,
                x.s.Reference,
                x.s.Notes,
                x.s.PurchasedOn,
                x.s.CreatedAt));
    }
}

// ── SHARED MAPPER ─────────────────────────────────────────────
internal static class StockMapper
{
    internal static StockLevelResponse ToLevel(Product p) => new(
        ProductId:      p.Id,
        ProductName:    p.Name,
        Category:       Category.ToSlug(p.Category),
        ImageUrl:       p.ImageUrl,
        StockLevel:     p.StockLevel,
        ReorderPoint:   p.ReorderPoint,
        LeadTimeDays:   p.LeadTimeDays,
        Supplier:       p.Supplier,
        SupplierSku:    p.SupplierSku,
        LowStock:       p.LowStock,
        OutOfStock:     p.OutOfStock,
        TotalPurchased: p.TotalPurchased,
        CostPrice:      p.CostPrice?.Amount,
        RecentPurchases: p.StockPurchases
            .OrderByDescending(s => s.PurchasedOn)
            .Take(5)
            .Select(s => new StockPurchaseResponse(
                s.Id, p.Id, p.Name, s.Quantity,
                s.UnitCost.Amount, s.TotalCost.Amount,
                s.Supplier, s.Reference, s.Notes,
                s.PurchasedOn, s.CreatedAt))
            .ToList());
}
