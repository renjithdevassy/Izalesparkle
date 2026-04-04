using System.Net.Http.Json;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Client.Services;

/// <summary>
/// Typed HTTP client that wraps all /api/* calls to the server.
/// Injected where pages need live data from the backend.
/// Falls back gracefully — pages can also use the in-memory ProductService offline.
/// </summary>
public interface IApiClient
{
    // Products
    Task<PagedResponse<ProductSummaryResponse>?> GetProductsAsync(GetProductsRequest req, CancellationToken ct = default);
    Task<ProductResponse?> GetProductAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<ProductSummaryResponse>?> GetFeaturedAsync(int count = 4, CancellationToken ct = default);
    Task<IEnumerable<ProductSummaryResponse>?> GetRelatedAsync(int id, int count = 4, CancellationToken ct = default);
    Task<IEnumerable<ReviewResponse>?> GetReviewsAsync(int productId, CancellationToken ct = default);
    // Orders
    Task<ApiResponse<OrderResponse>?> PlaceOrderAsync(PlaceOrderRequest req, CancellationToken ct = default);
    Task<OrderResponse?> GetOrderAsync(string orderNumber, CancellationToken ct = default);
    // Contact
    Task<ApiResponse<ContactResponse>?> SendContactAsync(ContactRequest req, CancellationToken ct = default);
    // Newsletter
    Task<ApiResponse<NewsletterResponse>?> SubscribeAsync(string email, CancellationToken ct = default);
    // Auth
    Task<AuthResponse?> RegisterAsync(RegisterRequest req, CancellationToken ct = default);
    Task<AuthResponse?> LoginAsync(LoginRequest req, CancellationToken ct = default);
    // Admin
    Task<AdminDashboardResponse?> GetDashboardAsync(CancellationToken ct = default);
    Task<IEnumerable<AdminProductResponse>?> GetAdminProductsAsync(CancellationToken ct = default);
    Task<ApiResponse<AdminProductResponse>?> CreateProductAsync(CreateProductRequest req, CancellationToken ct = default);
    Task<ApiResponse<AdminProductResponse>?> UpdateProductAsync(int id, UpdateProductRequest req, CancellationToken ct = default);
    Task<bool> DeleteProductAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<CategoryResponse>?> GetAdminCategoriesAsync(CancellationToken ct = default);
    Task<ApiResponse<CategoryResponse>?> CreateCategoryAsync(CreateCategoryRequest req, CancellationToken ct = default);
    Task<ApiResponse<CategoryResponse>?> UpdateCategoryAsync(int id, UpdateCategoryRequest req, CancellationToken ct = default);
    Task<bool> DeleteCategoryAsync(int id, CancellationToken ct = default);
    // Discount codes
    Task<IEnumerable<DiscountCodeResponse>?> GetDiscountCodesAsync(CancellationToken ct = default);
    Task<ApiResponse<DiscountCodeResponse>?> CreateDiscountCodeAsync(CreateDiscountCodeRequest req, CancellationToken ct = default);
    Task<ApiResponse<DiscountCodeResponse>?> UpdateDiscountCodeAsync(int id, UpdateDiscountCodeRequest req, CancellationToken ct = default);
    Task<bool> DeleteDiscountCodeAsync(int id, CancellationToken ct = default);
    Task<ValidateDiscountResponse?> ValidateDiscountAsync(string code, decimal subtotal, CancellationToken ct = default);
    // Customer orders
    Task<IEnumerable<OrderResponse>?> GetMyOrdersAsync(string email, CancellationToken ct = default);
    Task<IEnumerable<UserListResponse>?> GetAdminUsersAsync(CancellationToken ct = default);
    // Uploads
    Task<UploadImageResponse?> UploadImageAsync(Stream stream, string fileName, string contentType, CancellationToken ct = default);
    Task<bool> DeleteUploadedImageAsync(string fileName, CancellationToken ct = default);
    // Stock
    Task<StockReportResponse?> GetStockReportAsync(string? filter = null, CancellationToken ct = default);
    Task<IEnumerable<StockPurchaseResponse>?> GetStockPurchasesAsync(int? productId = null, CancellationToken ct = default);
    Task<ApiResponse<StockLevelResponse>?> AddStockPurchaseAsync(AddStockPurchaseRequest req, CancellationToken ct = default);
    Task<ApiResponse<StockLevelResponse>?> AdjustStockAsync(int productId, int newLevel, string? reason = null, CancellationToken ct = default);
    Task<ApiResponse<StockLevelResponse>?> UpdateStockSettingsAsync(int productId, UpdateProductStockSettingsRequest req, CancellationToken ct = default);
    // Admin — Orders
    Task<OrdersReportResponse?> GetAdminOrdersAsync(string? status = null, string? search = null, DateTime? from = null, DateTime? to = null, CancellationToken ct = default);
    Task<AdminOrderResponse?> GetAdminOrderAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<AdminOrderResponse>?> UpdateOrderAsync(int id, UpdateOrderStatusRequest req, CancellationToken ct = default);
    Task<bool> ResendInvoiceAsync(int orderId, CancellationToken ct = default);
    Task<ApiResponse<AdminOrderResponse>?> ModifyOrderItemsAsync(int orderId, List<ModifyOrderItemRequest> changes, CancellationToken ct = default);
    Task<ApiResponse<AdminOrderResponse>?> AddOrderItemAsync(int orderId, AddItemRequest req, CancellationToken ct = default);
    Task<ApiResponse<AdminOrderResponse>?> CancelOrderAdminAsync(int orderId, string reason, CancellationToken ct = default);
    Task<bool> CancelOrderCustomerAsync(string orderNumber, string reason, CancellationToken ct = default);
    Task<byte[]?> DownloadInvoiceAsync(int orderId, CancellationToken ct = default);
}

public class ApiClient(HttpClient http, AuthService auth) : IApiClient
{
    // ── AUTH HEADER ──────────────────────────────────────────────
    // Called before every request so the header is always current
    // (handles login/logout without needing to reconstruct HttpClient)
    void SetAuthHeader()
    {
        if (!string.IsNullOrEmpty(auth.Token))
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
        else
            http.DefaultRequestHeaders.Authorization = null;
    }

    // ── PRODUCTS ─────────────────────────────────────────────────
    public async Task<PagedResponse<ProductSummaryResponse>?> GetProductsAsync(
        GetProductsRequest req, CancellationToken ct = default)
    {
        var qs = BuildProductQuery(req);
        var resp = await http.GetFromJsonAsync<ApiResponse<PagedResponse<ProductSummaryResponse>>>(
            $"api/products?{qs}", ct);
        return resp?.Data;
    }

    public async Task<ProductResponse?> GetProductAsync(int id, CancellationToken ct = default)
    {
        SetAuthHeader();
        var resp = await http.GetFromJsonAsync<ApiResponse<ProductResponse>>($"api/products/{id}", ct);
        return resp?.Data;
    }

    public async Task<IEnumerable<ProductSummaryResponse>?> GetFeaturedAsync(
        int count = 4, CancellationToken ct = default)
    {
        var resp = await http.GetFromJsonAsync<ApiResponse<IEnumerable<ProductSummaryResponse>>>(
            $"api/products/featured?count={count}", ct);
        return resp?.Data;
    }

    public async Task<IEnumerable<ProductSummaryResponse>?> GetRelatedAsync(
        int id, int count = 4, CancellationToken ct = default)
    {
        var resp = await http.GetFromJsonAsync<ApiResponse<IEnumerable<ProductSummaryResponse>>>(
            $"api/products/{id}/related?count={count}", ct);
        return resp?.Data;
    }

    public async Task<IEnumerable<ReviewResponse>?> GetReviewsAsync(
        int productId, CancellationToken ct = default)
    {
        var resp = await http.GetFromJsonAsync<ApiResponse<IEnumerable<ReviewResponse>>>(
            $"api/products/{productId}/reviews", ct);
        return resp?.Data;
    }

    // ── ORDERS ───────────────────────────────────────────────────
    public async Task<ApiResponse<OrderResponse>?> PlaceOrderAsync(
        PlaceOrderRequest req, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/orders", req, ct);
        return await response.Content.ReadFromJsonAsync<ApiResponse<OrderResponse>>(cancellationToken: ct);
    }

    public async Task<OrderResponse?> GetOrderAsync(string orderNumber, CancellationToken ct = default)
    {
        SetAuthHeader();
        var resp = await http.GetFromJsonAsync<ApiResponse<OrderResponse>>(
            $"api/orders/{orderNumber}", ct);
        return resp?.Data;
    }

    // ── CONTACT ──────────────────────────────────────────────────
    public async Task<ApiResponse<ContactResponse>?> SendContactAsync(
        ContactRequest req, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/contact", req, ct);
        return await response.Content.ReadFromJsonAsync<ApiResponse<ContactResponse>>(cancellationToken: ct);
    }

    // ── NEWSLETTER ───────────────────────────────────────────────
    public async Task<ApiResponse<NewsletterResponse>?> SubscribeAsync(
        string email, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/newsletter/subscribe",
            new NewsletterSubscribeRequest(email), ct);
        return await response.Content.ReadFromJsonAsync<ApiResponse<NewsletterResponse>>(cancellationToken: ct);
    }

    // ── STOCK ────────────────────────────────────────────────────
    public async Task<StockReportResponse?> GetStockReportAsync(string? filter = null, CancellationToken ct = default)
    {
        SetAuthHeader();
        var url = "api/admin/stock" + (filter != null ? $"?filter={filter}" : "");
        var resp = await http.GetFromJsonAsync<ApiResponse<StockReportResponse>>(url, ct);
        return resp?.Data;
    }

    public async Task<IEnumerable<StockPurchaseResponse>?> GetStockPurchasesAsync(int? productId = null, CancellationToken ct = default)
    {
        SetAuthHeader();
        var url = "api/admin/stock/purchases" + (productId.HasValue ? $"?productId={productId}" : "");
        var resp = await http.GetFromJsonAsync<ApiResponse<IEnumerable<StockPurchaseResponse>>>(url, ct);
        return resp?.Data;
    }

    public async Task<ApiResponse<StockLevelResponse>?> AddStockPurchaseAsync(AddStockPurchaseRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var response = await http.PostAsJsonAsync("api/admin/stock/purchase", req, ct);
        return await response.Content.ReadFromJsonAsync<ApiResponse<StockLevelResponse>>(cancellationToken: ct);
    }

    public async Task<ApiResponse<StockLevelResponse>?> AdjustStockAsync(int productId, int newLevel, string? reason = null, CancellationToken ct = default)
    {
        SetAuthHeader();
        var response = await http.PutAsJsonAsync($"api/admin/stock/adjust/{productId}", new AdjustStockRequest(newLevel, reason), ct);
        return await response.Content.ReadFromJsonAsync<ApiResponse<StockLevelResponse>>(cancellationToken: ct);
    }

    public async Task<ApiResponse<StockLevelResponse>?> UpdateStockSettingsAsync(int productId, UpdateProductStockSettingsRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var response = await http.PutAsJsonAsync($"api/admin/stock/settings/{productId}", req, ct);
        return await response.Content.ReadFromJsonAsync<ApiResponse<StockLevelResponse>>(cancellationToken: ct);
    }

    // ── UPLOADS ──────────────────────────────────────────────────
    public async Task<UploadImageResponse?> UploadImageAsync(
        Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        SetAuthHeader();
        using var content2 = new MultipartFormDataContent();
        using var sc = new StreamContent(stream);
        sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content2.Add(sc, "file", fileName);
        var response = await http.PostAsync("api/uploads/image", content2, ct);
        var result   = await response.Content
            .ReadFromJsonAsync<ApiResponse<UploadImageResponse>>(cancellationToken: ct);
        return result?.Data;
    }

    public async Task<bool> DeleteUploadedImageAsync(string fileName, CancellationToken ct = default)
    {
        SetAuthHeader();
        var response = await http.DeleteAsync($"api/uploads/{fileName}", ct);
        return response.IsSuccessStatusCode;
    }

    // ── INVOICE ──────────────────────────────────────────────────
    public async Task<ApiResponse<AdminOrderResponse>?> ModifyOrderItemsAsync(
        int orderId, List<ModifyOrderItemRequest> changes, CancellationToken ct = default)
    {
        SetAuthHeader();
        var r = await http.PutAsJsonAsync($"api/admin/orders/{orderId}/items", changes, ct);
        return await r.Content.ReadFromJsonAsync<ApiResponse<AdminOrderResponse>>(cancellationToken: ct);
    }
    public async Task<ApiResponse<AdminOrderResponse>?> AddOrderItemAsync(
        int orderId, AddItemRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var r = await http.PostAsJsonAsync($"api/admin/orders/{orderId}/items", req, ct);
        return await r.Content.ReadFromJsonAsync<ApiResponse<AdminOrderResponse>>(cancellationToken: ct);
    }
    public async Task<ApiResponse<AdminOrderResponse>?> CancelOrderAdminAsync(
        int orderId, string reason, CancellationToken ct = default)
    {
        SetAuthHeader();
        var r = await http.PostAsJsonAsync($"api/admin/orders/{orderId}/cancel",
            new { Reason = reason }, ct);
        return await r.Content.ReadFromJsonAsync<ApiResponse<AdminOrderResponse>>(cancellationToken: ct);
    }
    public async Task<bool> CancelOrderCustomerAsync(
        string orderNumber, string reason, CancellationToken ct = default)
    {
        var r = await http.PostAsJsonAsync($"api/orders/{orderNumber}/cancel",
            new { Reason = reason }, ct);
        return r.IsSuccessStatusCode;
    }

    public async Task<bool> ResendInvoiceAsync(int orderId, CancellationToken ct = default)
    {
        SetAuthHeader();
        var response = await http.PostAsync($"api/admin/orders/{orderId}/resend-invoice", null, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<byte[]?> DownloadInvoiceAsync(int orderId, CancellationToken ct = default)
    {
        SetAuthHeader();
        var response = await http.GetAsync($"api/admin/orders/{orderId}/invoice", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    // ── ADMIN ORDERS ─────────────────────────────────────────────
    public async Task<OrdersReportResponse?> GetAdminOrdersAsync(
        string? status = null, string? search = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        SetAuthHeader();
        var qs = new List<string>();
        if (!string.IsNullOrEmpty(status)) qs.Add($"status={Uri.EscapeDataString(status)}");
        if (!string.IsNullOrEmpty(search)) qs.Add($"search={Uri.EscapeDataString(search)}");
        if (from.HasValue) qs.Add($"from={from.Value:yyyy-MM-dd}");
        if (to.HasValue)   qs.Add($"to={to.Value:yyyy-MM-dd}");
        var url = "api/admin/orders" + (qs.Any() ? "?" + string.Join("&", qs) : "");
        var resp = await http.GetFromJsonAsync<ApiResponse<OrdersReportResponse>>(url, ct);
        return resp?.Data;
    }

    public async Task<AdminOrderResponse?> GetAdminOrderAsync(int id, CancellationToken ct = default)
    {
        SetAuthHeader();
        var resp = await http.GetFromJsonAsync<ApiResponse<AdminOrderResponse>>($"api/admin/orders/{id}", ct);
        return resp?.Data;
    }

    public async Task<ApiResponse<AdminOrderResponse>?> UpdateOrderAsync(
        int id, UpdateOrderStatusRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var response = await http.PutAsJsonAsync($"api/admin/orders/{id}", req, ct);
        return await response.Content.ReadFromJsonAsync<ApiResponse<AdminOrderResponse>>(cancellationToken: ct);
    }

    // ── DISCOUNT CODES ───────────────────────────────────────────
    public async Task<IEnumerable<DiscountCodeResponse>?> GetDiscountCodesAsync(CancellationToken ct = default)
    {
        SetAuthHeader();
        var resp = await http.GetFromJsonAsync<ApiResponse<IEnumerable<DiscountCodeResponse>>>("api/admin/discounts", ct);
        return resp?.Data;
    }
    public async Task<ApiResponse<DiscountCodeResponse>?> CreateDiscountCodeAsync(CreateDiscountCodeRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var r = await http.PostAsJsonAsync("api/admin/discounts", req, ct);
        return await r.Content.ReadFromJsonAsync<ApiResponse<DiscountCodeResponse>>(cancellationToken: ct);
    }
    public async Task<ApiResponse<DiscountCodeResponse>?> UpdateDiscountCodeAsync(int id, UpdateDiscountCodeRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var r = await http.PutAsJsonAsync($"api/admin/discounts/{id}", req, ct);
        return await r.Content.ReadFromJsonAsync<ApiResponse<DiscountCodeResponse>>(cancellationToken: ct);
    }
    public async Task<bool> DeleteDiscountCodeAsync(int id, CancellationToken ct = default)
    {
        SetAuthHeader();
        var r = await http.DeleteAsync($"api/admin/discounts/{id}", ct);
        return r.IsSuccessStatusCode;
    }
    public async Task<ValidateDiscountResponse?> ValidateDiscountAsync(string code, decimal subtotal, CancellationToken ct = default)
    {
        var r = await http.PostAsJsonAsync("api/orders/validate-discount",
            new { Code = code, Subtotal = subtotal }, ct);
        var resp = await r.Content.ReadFromJsonAsync<ApiResponse<ValidateDiscountResponse>>(cancellationToken: ct);
        return resp?.Data;
    }
    public async Task<IEnumerable<OrderResponse>?> GetMyOrdersAsync(string email, CancellationToken ct = default)
    {
        var resp = await http.GetFromJsonAsync<ApiResponse<IEnumerable<OrderResponse>>>(
            $"api/orders/my?email={Uri.EscapeDataString(email)}", ct);
        return resp?.Data;
    }

    // ── USERS ────────────────────────────────────────────────────
    public async Task<IEnumerable<UserListResponse>?> GetAdminUsersAsync(CancellationToken ct = default)
    {
        SetAuthHeader();
        var resp = await http.GetFromJsonAsync<ApiResponse<IEnumerable<UserListResponse>>>("api/admin/users", ct);
        return resp?.Data;
    }

    // ── AUTH ─────────────────────────────────────────────────────
    public async Task<AuthResponse?> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var response = await http.PostAsJsonAsync("api/auth/register", req, ct);
        return await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var response = await http.PostAsJsonAsync("api/auth/login", req, ct);
        return await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
    }

    // ── ADMIN ────────────────────────────────────────────────────
    public async Task<AdminDashboardResponse?> GetDashboardAsync(CancellationToken ct = default)
    {
        SetAuthHeader();
        var resp = await http.GetFromJsonAsync<ApiResponse<AdminDashboardResponse>>("api/admin/dashboard", ct);
        return resp?.Data;
    }

    public async Task<IEnumerable<AdminProductResponse>?> GetAdminProductsAsync(CancellationToken ct = default)
    {
        var resp = await http.GetFromJsonAsync<ApiResponse<IEnumerable<AdminProductResponse>>>("api/admin/products", ct);
        return resp?.Data;
    }

    public async Task<ApiResponse<AdminProductResponse>?> CreateProductAsync(
        CreateProductRequest req, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/admin/products", req, ct);
        return await response.Content.ReadFromJsonAsync<ApiResponse<AdminProductResponse>>(cancellationToken: ct);
    }

    public async Task<ApiResponse<AdminProductResponse>?> UpdateProductAsync(
        int id, UpdateProductRequest req, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"api/admin/products/{id}", req, ct);
        return await response.Content.ReadFromJsonAsync<ApiResponse<AdminProductResponse>>(cancellationToken: ct);
    }

    public async Task<bool> DeleteProductAsync(int id, CancellationToken ct = default)
    {
        SetAuthHeader();
        var response = await http.DeleteAsync($"api/admin/products/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<IEnumerable<CategoryResponse>?> GetAdminCategoriesAsync(CancellationToken ct = default)
    {
        var resp = await http.GetFromJsonAsync<ApiResponse<IEnumerable<CategoryResponse>>>("api/admin/categories", ct);
        return resp?.Data;
    }
    public async Task<ApiResponse<CategoryResponse>?> CreateCategoryAsync(
        CreateCategoryRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var r = await http.PostAsJsonAsync("api/admin/categories", req, ct);
        return await r.Content.ReadFromJsonAsync<ApiResponse<CategoryResponse>>(cancellationToken: ct);
    }
    public async Task<ApiResponse<CategoryResponse>?> UpdateCategoryAsync(
        int id, UpdateCategoryRequest req, CancellationToken ct = default)
    {
        SetAuthHeader();
        var r = await http.PutAsJsonAsync($"api/admin/categories/{id}", req, ct);
        return await r.Content.ReadFromJsonAsync<ApiResponse<CategoryResponse>>(cancellationToken: ct);
    }
    public async Task<bool> DeleteCategoryAsync(int id, CancellationToken ct = default)
    {
        SetAuthHeader();
        var r = await http.DeleteAsync($"api/admin/categories/{id}", ct);
        return r.IsSuccessStatusCode;
    }

    // ── HELPERS ──────────────────────────────────────────────────
    private static string BuildProductQuery(GetProductsRequest r)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(r.Category)) parts.Add($"category={Uri.EscapeDataString(r.Category)}");
        if (!string.IsNullOrWhiteSpace(r.Search))   parts.Add($"search={Uri.EscapeDataString(r.Search)}");
        if (r.MinPrice.HasValue)  parts.Add($"minPrice={r.MinPrice}");
        if (r.MaxPrice.HasValue)  parts.Add($"maxPrice={r.MaxPrice}");
        if (r.Sort != "default")  parts.Add($"sort={r.Sort}");
        parts.Add($"page={r.Page}");
        parts.Add($"pageSize={r.PageSize}");
        return string.Join("&", parts);
    }
}