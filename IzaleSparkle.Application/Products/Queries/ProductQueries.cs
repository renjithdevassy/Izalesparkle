using AutoMapper;
using MediatR;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Exceptions;

namespace IzaleSparkle.Application.Products.Queries;

// ── GET PRODUCTS (paged, filterable) ─────────────────────────
public record GetProductsQuery(
    string? Category  = null,
    string? Search    = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string  Sort      = "default",
    int     Page      = 1,
    int     PageSize  = 12
) : IRequest<PagedResponse<ProductSummaryResponse>>;

public sealed class GetProductsQueryHandler(
    IUnitOfWork uow,
    IMapper mapper)
    : IRequestHandler<GetProductsQuery, PagedResponse<ProductSummaryResponse>>
{
    public async Task<PagedResponse<ProductSummaryResponse>> Handle(
        GetProductsQuery req, CancellationToken ct)
    {
        var products = await uow.Products.SearchAsync(
            req.Search, req.Category, req.MinPrice, req.MaxPrice, ct);

        // Sort
        products = req.Sort switch
        {
            "price-asc"  => products.OrderBy(p => p.Price.Amount),
            "price-desc" => products.OrderByDescending(p => p.Price.Amount),
            "rating"     => products.OrderByDescending(p => p.Stars),
            "sale"       => products.Where(p => p.IsOnSale),
            _            => products
        };

        var total = products.Count();
        var paged = products.Skip((req.Page - 1) * req.PageSize).Take(req.PageSize);
        var dtos  = mapper.Map<IEnumerable<ProductSummaryResponse>>(paged);

        return new PagedResponse<ProductSummaryResponse>(dtos, req.Page, req.PageSize, total);
    }
}

// ── GET PRODUCT BY ID ─────────────────────────────────────────
public record GetProductByIdQuery(int Id) : IRequest<ProductResponse>;

public sealed class GetProductByIdQueryHandler(
    IUnitOfWork uow,
    IMapper mapper)
    : IRequestHandler<GetProductByIdQuery, ProductResponse>
{
    public async Task<ProductResponse> Handle(GetProductByIdQuery req, CancellationToken ct)
    {
        var product = await uow.Products.GetByIdAsync(req.Id, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.Product), req.Id);

        return mapper.Map<ProductResponse>(product);
    }
}

// ── GET FEATURED ─────────────────────────────────────────────
public record GetFeaturedProductsQuery(int Count = 4)
    : IRequest<IEnumerable<ProductSummaryResponse>>;

public sealed class GetFeaturedProductsQueryHandler(
    IUnitOfWork uow,
    IMapper mapper)
    : IRequestHandler<GetFeaturedProductsQuery, IEnumerable<ProductSummaryResponse>>
{
    public async Task<IEnumerable<ProductSummaryResponse>> Handle(
        GetFeaturedProductsQuery req, CancellationToken ct)
    {
        var products = await uow.Products.GetFeaturedAsync(req.Count, ct);
        return mapper.Map<IEnumerable<ProductSummaryResponse>>(products);
    }
}

// ── GET RELATED ───────────────────────────────────────────────
public record GetRelatedProductsQuery(int ProductId, int Count = 4)
    : IRequest<IEnumerable<ProductSummaryResponse>>;

public sealed class GetRelatedProductsQueryHandler(
    IUnitOfWork uow,
    IMapper mapper)
    : IRequestHandler<GetRelatedProductsQuery, IEnumerable<ProductSummaryResponse>>
{
    public async Task<IEnumerable<ProductSummaryResponse>> Handle(
        GetRelatedProductsQuery req, CancellationToken ct)
    {
        var products = await uow.Products.GetRelatedAsync(req.ProductId, req.Count, ct);
        return mapper.Map<IEnumerable<ProductSummaryResponse>>(products);
    }
}

// ── GET REVIEWS ───────────────────────────────────────────────
public record GetProductReviewsQuery(int ProductId) : IRequest<IEnumerable<ReviewResponse>>;

public sealed class GetProductReviewsQueryHandler(
    IUnitOfWork uow,
    IMapper mapper)
    : IRequestHandler<GetProductReviewsQuery, IEnumerable<ReviewResponse>>
{
    public async Task<IEnumerable<ReviewResponse>> Handle(
        GetProductReviewsQuery req, CancellationToken ct)
    {
        var reviews = await uow.Reviews.GetByProductIdAsync(req.ProductId, ct);
        return mapper.Map<IEnumerable<ReviewResponse>>(reviews);
    }
}
