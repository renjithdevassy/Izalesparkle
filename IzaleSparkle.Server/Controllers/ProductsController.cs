using MediatR;
using Microsoft.AspNetCore.Mvc;
using IzaleSparkle.Application.Products.Queries;
using IzaleSparkle.Application.Admin.Commands;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProductsController(IMediator mediator) : ControllerBase
{
    /// <summary>Get all products with optional filtering, sorting, and pagination.</summary>
    [HttpGet]
    [ResponseCache(Duration = 60, VaryByQueryKeys = new[] { "*" })]
    [ProducesResponseType(typeof(ApiResponse<PagedResponse<ProductSummaryResponse>>), 200)]
    public async Task<IActionResult> GetProducts([FromQuery] GetProductsRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new GetProductsQuery(
            req.Category, req.Search, req.MinPrice, req.MaxPrice,
            req.Sort, req.Page, req.PageSize), ct);
        return Ok(ApiResponse<PagedResponse<ProductSummaryResponse>>.Ok(result));
    }

    /// <summary>Get a single product by ID with full image set.</summary>
    [HttpGet("{id:int}")]
    [ResponseCache(Duration = 120)]
    [ProducesResponseType(typeof(ApiResponse<ProductResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetProductByIdQuery(id), ct);
        return Ok(ApiResponse<ProductResponse>.Ok(result));
    }

    /// <summary>Get featured products for the homepage.</summary>
    [HttpGet("featured")]
    [ResponseCache(Duration = 300)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProductSummaryResponse>>), 200)]
    public async Task<IActionResult> GetFeatured([FromQuery] int count = 4, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetFeaturedProductsQuery(count), ct);
        return Ok(ApiResponse<IEnumerable<ProductSummaryResponse>>.Ok(result));
    }

    /// <summary>Get related products for a product page.</summary>
    [HttpGet("{id:int}/related")]
    [ResponseCache(Duration = 120)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProductSummaryResponse>>), 200)]
    public async Task<IActionResult> GetRelated(int id, [FromQuery] int count = 4, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetRelatedProductsQuery(id, count), ct);
        return Ok(ApiResponse<IEnumerable<ProductSummaryResponse>>.Ok(result));
    }

    /// <summary>Get reviews for a product.</summary>
    [HttpGet("{id:int}/reviews")]
    [ResponseCache(Duration = 60)]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ReviewResponse>>), 200)]
    public async Task<IActionResult> GetReviews(int id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetProductReviewsQuery(id), ct);
        return Ok(ApiResponse<IEnumerable<ReviewResponse>>.Ok(result));
    }

    /// <summary>Get list of available categories.</summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CategoryResponse>>), 200)]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await mediator.Send(new GetCategoriesAdminQuery(), ct);
        return Ok(ApiResponse<IEnumerable<CategoryResponse>>.Ok(result));
    }
}
