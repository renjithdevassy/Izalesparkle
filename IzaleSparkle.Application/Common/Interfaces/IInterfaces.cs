using IzaleSparkle.Domain.Entities;

namespace IzaleSparkle.Application.Common.Interfaces;

// ── REPOSITORIES (generic + specific) ───────────────────────
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}

public interface IProductRepository : IRepository<Product>
{
    Task<IEnumerable<Product>> GetByCategoryAsync(string? category, CancellationToken ct = default);
    Task<IEnumerable<Product>> SearchAsync(string? query, string? category, decimal? minPrice, decimal? maxPrice, CancellationToken ct = default);
    Task<IEnumerable<Product>> GetFeaturedAsync(int count, CancellationToken ct = default);
    Task<IEnumerable<Product>> GetRelatedAsync(int productId, int count, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(string? category, string? query, CancellationToken ct = default);
}

public interface ICategoryRepository : IRepository<Category>
{
    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IEnumerable<Category>> GetAllActiveAsync(CancellationToken ct = default);
}

public interface IDiscountCodeRepository : IRepository<DiscountCode>
{
    Task<DiscountCode?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IEnumerable<DiscountCode>> GetAllAsync(CancellationToken ct = default);
}

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);
    Task<IEnumerable<Order>> GetByEmailAsync(string email, CancellationToken ct = default);
}

public interface IReviewRepository : IRepository<Review>
{
    Task<IEnumerable<Review>> GetByProductIdAsync(int productId, CancellationToken ct = default);
}

// ── UNIT OF WORK ─────────────────────────────────────────────
public interface IUnitOfWork : IAsyncDisposable
{
    IProductRepository Products { get; }
    ICategoryRepository     Categories    { get; }
    IDiscountCodeRepository DiscountCodes { get; }
    IOrderRepository   Orders   { get; }
    IReviewRepository  Reviews  { get; }
    IUserRepository    Users    { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// ── EXTERNAL SERVICES ────────────────────────────────────────
public interface IEmailService
{
    Task SendOrderConfirmationAsync(OrderEmailData data, CancellationToken ct = default);
    Task SendAdminOrderNotificationAsync(OrderEmailData data, CancellationToken ct = default);
    Task SendOrderStatusUpdateAsync(string customerEmail, string customerName, string orderNumber, string newStatus, string? trackingNumber, CancellationToken ct = default);
    Task<byte[]> GenerateInvoicePdfAsync(OrderEmailData data, CancellationToken ct = default);
    Task SendContactEmailAsync(string from, string name, string subject, string body, CancellationToken ct = default);
    Task SendNewsletterWelcomeAsync(string email, CancellationToken ct = default);
    Task SendPasswordResetAsync(string email, string firstName, string rawToken, CancellationToken ct = default);
}

/// <summary>All data needed to render a full order confirmation email.</summary>
public record OrderEmailData(
    string  CustomerEmail,
    string  CustomerName,
    string  OrderNumber,
    string  Status,
    decimal Subtotal,
    decimal Shipping,
    decimal Vat,
    decimal Discount,
    decimal Total,
    string? PromoCode,
    string  ShippingAddress,
    string  ShippingTier,
    List<OrderEmailItem> Items
);
public record OrderEmailItem(string Name, string Material, int Qty, decimal UnitPrice, decimal LineTotal);

/// <summary>Records and reports public website visits for the admin dashboard.</summary>
public interface ISiteAnalytics
{
    /// <summary>Record a single website visit. Never throws.</summary>
    Task RecordVisitAsync(string? path, CancellationToken ct = default);

    /// <summary>Total views all-time and today (UTC).</summary>
    Task<(int Total, int Today)> GetViewCountsAsync(CancellationToken ct = default);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default) where T : class;
    Task RemoveAsync(string key, CancellationToken ct = default);
}

public interface IUserRepository : IRepository<AppUser>
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool>     EmailExistsAsync(string email, CancellationToken ct = default);
}

public interface IAuthService
{
    string HashPassword(string password);
    bool   VerifyPassword(string password, string hash);
    string GenerateJwtToken(AppUser user);
}

public interface IWhatsAppService
{
    Task SendOrderNotificationAsync(OrderEmailData data, CancellationToken ct = default);
    Task SendContactNotificationAsync(string fromName, string email, string message, CancellationToken ct = default);
    Task SendLowStockAlertAsync(string productName, int stockLevel, CancellationToken ct = default);
}

/// <summary>Reads products from the linked WhatsApp/Meta Commerce catalog (Graph API).</summary>
public interface IWhatsAppCatalogService
{
    Task<IReadOnlyList<WhatsAppCatalogItem>> FetchProductsAsync(CancellationToken ct = default);
}

/// <summary>A single product item read from the Meta/WhatsApp commerce catalog.</summary>
public record WhatsAppCatalogItem(
    string  RetailerId,
    string  Name,
    string  Description,
    decimal Price,
    string? Currency,
    string  ImageUrl,
    bool    Available
);
