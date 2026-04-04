using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IzaleSparkle.Application.Admin.Commands;
using Microsoft.AspNetCore.Authorization;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Server.Controllers;

[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin")]
[Produces("application/json")]
public class AdminController(IMediator mediator) : ControllerBase
{
    // ── DASHBOARD ────────────────────────────────────────────────
    /// <summary>Admin dashboard summary — counts, revenue, recent items.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardResponse>), 200)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAdminDashboardQuery(), ct);
        return Ok(ApiResponse<AdminDashboardResponse>.Ok(result));
    }

    // ── PRODUCTS ─────────────────────────────────────────────────
    /// <summary>Get all products including inactive — admin only.</summary>
    [HttpGet("products")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AdminProductResponse>>), 200)]
    public async Task<IActionResult> GetAllProducts(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAllProductsAdminQuery(), ct);
        return Ok(ApiResponse<IEnumerable<AdminProductResponse>>.Ok(result));
    }

    /// <summary>Create a new product.</summary>
    [HttpPost("products")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductResponse>), 201)]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateProductCommand(req), ct);
        return CreatedAtAction("GetAllProducts", null,
            ApiResponse<AdminProductResponse>.Ok(result, "Product created successfully."));
    }

    /// <summary>Update an existing product.</summary>
    [HttpPut("products/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminProductResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductRequest req, CancellationToken ct)
    {
        if (req.Id != id) return BadRequest(ApiResponse<object>.Fail("ID mismatch."));
        var result = await mediator.Send(new UpdateProductCommand(req), ct);
        return Ok(ApiResponse<AdminProductResponse>.Ok(result, "Product updated successfully."));
    }

    /// <summary>Delete a product.</summary>
    [HttpDelete("products/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        await mediator.Send(new DeleteProductCommand(id), ct);
        return Ok(ApiResponse<bool>.Ok(true, "Product deleted."));
    }

    // ── ORDERS ───────────────────────────────────────────────────
    /// <summary>Get all orders with stats and optional filters.</summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(ApiResponse<OrdersReportResponse>), 200)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetAllOrdersAdminQuery(status, search, from, to), ct);
        return Ok(ApiResponse<OrdersReportResponse>.Ok(result));
    }

    /// <summary>Get a single order by ID (full detail).</summary>
    [HttpGet("orders/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminOrderResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetOrder(int id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetOrderAdminQuery(id), ct);
        return Ok(ApiResponse<AdminOrderResponse>.Ok(result));
    }

    /// <summary>Update order status and/or admin notes.</summary>
    [HttpPut("orders/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<AdminOrderResponse>), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateOrder(int id,
        [FromBody] UpdateOrderStatusRequest req, CancellationToken ct)
    {
        if (req.Id != id) return BadRequest(ApiResponse<object>.Fail("ID mismatch."));
        var result = await mediator.Send(new UpdateOrderStatusCommand(req), ct);
        return Ok(ApiResponse<AdminOrderResponse>.Ok(result, "Order updated successfully."));
    }

    // ── STOCK ────────────────────────────────────────────────────
    [HttpGet("stock")]
    public async Task<IActionResult> GetStockReport([FromQuery] string? filter, CancellationToken ct)
    {
        var result = await mediator.Send(new GetStockReportQuery(filter), ct);
        return Ok(ApiResponse<StockReportResponse>.Ok(result));
    }

    [HttpGet("stock/purchases")]
    public async Task<IActionResult> GetPurchases([FromQuery] int? productId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetStockPurchasesQuery(productId), ct);
        return Ok(ApiResponse<IEnumerable<StockPurchaseResponse>>.Ok(result));
    }

    [HttpPost("stock/purchase")]
    public async Task<IActionResult> AddPurchase([FromBody] AddStockPurchaseRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new AddStockPurchaseCommand(req), ct);
        return Ok(ApiResponse<StockLevelResponse>.Ok(result, "Stock purchase recorded."));
    }

    [HttpPut("stock/adjust/{productId:int}")]
    public async Task<IActionResult> AdjustStock(int productId, [FromBody] AdjustStockRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new AdjustStockCommand(productId, req.NewLevel, req.Reason), ct);
        return Ok(ApiResponse<StockLevelResponse>.Ok(result, "Stock level adjusted."));
    }

    [HttpPut("stock/settings/{productId:int}")]
    public async Task<IActionResult> UpdateStockSettings(int productId,
        [FromBody] UpdateProductStockSettingsRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateProductStockSettingsCommand(req with { ProductId = productId }), ct);
        return Ok(ApiResponse<StockLevelResponse>.Ok(result));
    }

    // ── RESEND INVOICE ───────────────────────────────────────────
    /// <summary>Resend order confirmation + invoice PDF to the customer.</summary>
    [HttpPost("orders/{id:int}/resend-invoice")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    public async Task<IActionResult> ResendInvoice(int id, CancellationToken ct)
    {
        var result = await mediator.Send(new ResendInvoiceCommand(id), ct);
        return Ok(ApiResponse<bool>.Ok(result, "Invoice sent to customer."));
    }

    // ── USERS ────────────────────────────────────────────────────
    /// <summary>List all registered users — view only.</summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<UserListResponse>>), 200)]
    public async Task<IActionResult> GetUsers(CancellationToken ct)
    {
        var users = await mediator.Send(new GetUsersAdminQuery(), ct);
        return Ok(ApiResponse<IEnumerable<UserListResponse>>.Ok(users));
    }

    // ── INVOICE ──────────────────────────────────────────────────
    /// <summary>Download invoice PDF for an order.</summary>
    [HttpGet("orders/{id:int}/invoice")]
    public async Task<IActionResult> DownloadInvoice(int id, CancellationToken ct)
    {
        var (bytes, filename) = await mediator.Send(new GenerateInvoicePdfQuery(id), ct);
        return File(bytes, "application/pdf", filename);
    }

    // ── ORDER ITEMS ──────────────────────────────────────────────
    /// <summary>Update item quantities in an order (qty=0 removes the item).</summary>
    [HttpPut("orders/{id:int}/items")]
    public async Task<IActionResult> ModifyOrderItems(int id,
        [FromBody] List<ModifyOrderItemRequest> changes, CancellationToken ct)
    {
        var result = await mediator.Send(new ModifyOrderItemsCommand(id, changes), ct);
        return Ok(ApiResponse<AdminOrderResponse>.Ok(result, "Order items updated."));
    }

    /// <summary>Add a product to an existing order.</summary>
    [HttpPost("orders/{id:int}/items")]
    public async Task<IActionResult> AddOrderItem(int id,
        [FromBody] AddItemRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new AddItemToOrderCommand(id, req.ProductId, req.Quantity, req.Metal), ct);
        return Ok(ApiResponse<AdminOrderResponse>.Ok(result, "Item added to order."));
    }

    /// <summary>Cancel an order (admin version — can cancel any status).</summary>
    [HttpPost("orders/{id:int}/cancel")]
    public async Task<IActionResult> CancelOrder(int id,
        [FromBody] CancelOrderRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(
            new CancelOrderCommand(id, req.Reason, IsCustomer: false), ct);
        return Ok(ApiResponse<AdminOrderResponse>.Ok(result, "Order cancelled."));
    }

    // ── DISCOUNT CODES ───────────────────────────────────────────
    [HttpGet("discounts")]
    public async Task<IActionResult> GetDiscounts(CancellationToken ct)
    {
        var result = await mediator.Send(new GetDiscountCodesQuery(), ct);
        return Ok(ApiResponse<IEnumerable<DiscountCodeResponse>>.Ok(result));
    }

    [HttpPost("discounts")]
    public async Task<IActionResult> CreateDiscount(
        [FromBody] CreateDiscountCodeRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateDiscountCodeCommand(req), ct);
        return Ok(ApiResponse<DiscountCodeResponse>.Ok(result, "Discount code created."));
    }

    [HttpPut("discounts/{id:int}")]
    public async Task<IActionResult> UpdateDiscount(int id,
        [FromBody] UpdateDiscountCodeRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateDiscountCodeCommand(req with { Id = id }), ct);
        return Ok(ApiResponse<DiscountCodeResponse>.Ok(result, "Discount code updated."));
    }

    [HttpDelete("discounts/{id:int}")]
    public async Task<IActionResult> DeleteDiscount(int id, CancellationToken ct)
    {
        await mediator.Send(new DeleteDiscountCodeCommand(id), ct);
        return Ok(ApiResponse<bool>.Ok(true, "Discount code deleted."));
    }

    // ── CATEGORIES ───────────────────────────────────────────────
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await mediator.Send(new GetCategoriesAdminQuery(), ct);
        return Ok(ApiResponse<IEnumerable<CategoryResponse>>.Ok(result));
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new CreateCategoryAdminCommand(req), ct);
        return Ok(ApiResponse<CategoryResponse>.Ok(result, "Category created."));
    }

    [HttpPut("categories/{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id,
        [FromBody] UpdateCategoryRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateCategoryAdminCommand(req with { Id = id }), ct);
        return Ok(ApiResponse<CategoryResponse>.Ok(result, "Category updated."));
    }

    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        await mediator.Send(new DeleteCategoryAdminCommand(id), ct);
        return Ok(ApiResponse<bool>.Ok(true, "Category deleted."));
    }
}
