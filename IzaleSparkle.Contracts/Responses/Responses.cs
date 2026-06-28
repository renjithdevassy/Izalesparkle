namespace IzaleSparkle.Contracts.Responses;

// ── ENVELOPE ─────────────────────────────────────────────────
public record ApiResponse<T>(bool Success, T? Data, string? Message = null, IEnumerable<string>? Errors = null)
{
    public static ApiResponse<T> Ok(T data, string? msg = null)  => new(true, data, msg);
    public static ApiResponse<T> Fail(string msg, IEnumerable<string>? errors = null) => new(false, default, msg, errors);
}

public record PagedResponse<T>(
    IEnumerable<T> Items,
    int Page, int PageSize, int TotalCount
)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext   => Page < TotalPages;
    public bool HasPrev   => Page > 1;
}

// ── PRODUCTS ─────────────────────────────────────────────────
public record ProductResponse(
    int     Id,
    string  Name,
    string  Slug,
    string  Description,
    string  Material,
    decimal Price,
    decimal? OldPrice,
    string  Category,
    string? Badge,
    int     Stars,
    string  ImageUrl,
    List<string> Images,
    bool    IsOnSale,
    int?    SavePercent,
    int     StockLevel = 0,
    bool    IsVatApplicable = false
);

public record ProductSummaryResponse(
    int     Id,
    string  Name,
    string  Slug,
    string  Material,
    decimal Price,
    decimal? OldPrice,
    string  Category,
    string? Badge,
    int     Stars,
    string  ImageUrl,
    bool    IsOnSale,
    int?    SavePercent,
    bool    IsVatApplicable = false,
    int     StockLevel = 0
);

public record ReviewResponse(
    int     Id,
    string  AuthorName,
    string  Date,
    int     Stars,
    string  Title,
    string  Body,
    bool    Verified,
    int     HelpfulCount
);

// ── ORDERS ───────────────────────────────────────────────────
public record OrderResponse(
    int     Id,
    string  OrderNumber,
    string  CustomerEmail,
    string  Status,
    decimal Subtotal,
    decimal Shipping,
    decimal Vat,
    decimal Discount,
    decimal Total,
    string? PromoCode,
    DateTime CreatedAt,
    List<OrderItemResponse> Items
);

public record OrderItemResponse(
    int     ProductId,
    string  ProductName,
    string  ImageUrl,
    decimal UnitPrice,
    int     Quantity,
    decimal LineTotal,
    string  Metal,
    string? Size
);

// ── CONTACT ──────────────────────────────────────────────────
public record ContactResponse(bool Sent, string Message);

// ── NEWSLETTER ───────────────────────────────────────────────
public record NewsletterResponse(bool Subscribed, string Message);

// ── ADMIN ─────────────────────────────────────────────────────
public record AdminProductResponse(
    int     Id,
    string  Name,
    string  Slug,
    string  Description,
    string  Material,
    decimal Price,
    decimal? OldPrice,
    string  Category,
    string? Badge,
    int     Stars,
    string  ImageUrl,
    List<string> Images,
    bool    IsActive,
    int     StockLevel,
    bool    IsOnSale,
    int?    SavePercent,
    DateTime CreatedAt,
    decimal? CostPrice,
    int?    MarginPercent,
    int     ReorderPoint,
    int     LeadTimeDays,
    string? Supplier,
    string? SupplierSku,
    bool    LowStock,
    bool    OutOfStock,
    int     TotalPurchased,
    bool    IsVatApplicable = false
);

/// <summary>Result of importing products from the WhatsApp/Meta catalog.</summary>
public record WhatsAppSyncResponse(
    int Created,
    int Updated,
    int Skipped,
    int Total,
    List<string> Messages
);

public record CategoryResponse(
    int    Id,
    string Name,
    string Slug,
    string Description,
    string Icon,
    int    SortOrder,
    bool   IsActive,
    int    ProductCount
);

public record AdminDashboardResponse(
    int   TotalProducts,
    int   ActiveProducts,
    int   TotalOrders,
    int   PendingOrders,
    decimal TotalRevenue,
    List<AdminProductResponse> RecentProducts,
    List<OrderResponse>        RecentOrders,
    decimal TodayRevenue       = 0,
    decimal AverageOrderValue  = 0,
    int     CancelledOrders    = 0,
    int     DeliveredOrders    = 0,
    int     LowStockCount      = 0,
    int     TotalViews         = 0,
    int     TodayViews         = 0
);

// ── AUTH ─────────────────────────────────────────────────────
public record AuthResponse(
    bool   Success,
    string? Token,
    string? Email,
    string? FirstName,
    string? LastName,
    string? Role,
    string? Message
);

/// <summary>Simple success/message result for auth flows like password reset.</summary>
public record AuthMessageResponse(
    bool   Success,
    string Message
);

// ── ADMIN ORDER (full detail) ─────────────────────────────────
public record AdminOrderResponse(
    int      Id,
    string   OrderNumber,
    string   CustomerEmail,
    string   Status,
    string   PaymentMethod,
    string   ShippingTier,
    decimal  Subtotal,
    decimal  Shipping,
    decimal  Vat,
    decimal  Discount,
    decimal  Total,
    string?  PromoCode,
    string?  AdminNotes,
    string?  TrackingNumber,
    DateTime CreatedAt,
    // Shipping address
    string   ShipFirstName,
    string   ShipLastName,
    string   ShipLine1,
    string?  ShipLine2,
    string   ShipCity,
    string   ShipPostcode,
    string   ShipCountry,
    List<OrderItemResponse> Items
);

public record OrdersReportResponse(
    int     TotalOrders,
    int     PendingOrders,
    int     ProcessingOrders,
    int     CollectionReadyOrders,
    int     ShippedOrders,
    int     DeliveredOrders,
    int     CancelledOrders,
    decimal TotalRevenue,
    decimal TodayRevenue,
    List<AdminOrderResponse> Orders
);

// ── UPLOADS ──────────────────────────────────────────────────
public record UploadImageResponse(string Url, string FileName, long SizeBytes);

// ── STOCK ─────────────────────────────────────────────────────
public record StockPurchaseResponse(
    int      Id,
    int      ProductId,
    string   ProductName,
    int      Quantity,
    decimal  UnitCost,
    decimal  TotalCost,
    string?  Supplier,
    string?  Reference,
    string?  Notes,
    DateTime PurchasedOn,
    DateTime CreatedAt
);

public record StockLevelResponse(
    int      ProductId,
    string   ProductName,
    string   Category,
    string   ImageUrl,
    int      StockLevel,
    int      ReorderPoint,
    int      LeadTimeDays,
    string?  Supplier,
    string?  SupplierSku,
    bool     LowStock,
    bool     OutOfStock,
    int      TotalPurchased,
    decimal? CostPrice,
    List<StockPurchaseResponse> RecentPurchases
);

public record StockReportResponse(
    int     TotalProducts,
    int     InStockProducts,
    int     LowStockProducts,
    int     OutOfStockProducts,
    decimal TotalStockValue,
    decimal TotalCostValue,
    List<StockLevelResponse> Products
);

// ── ADMIN USER VIEW ───────────────────────────────────────────
public record UserListResponse(
    int      Id,
    string   Email,
    string   FirstName,
    string   LastName,
    string   FullName,
    string   Role,
    bool     IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

// ── DISCOUNT CODE ─────────────────────────────────────────────
public record DiscountCodeResponse(
    int      Id,
    string   Code,
    string   Description,
    decimal  DiscountPercent,
    bool     IsActive,
    int?     MaxUses,
    int      TimesUsed,
    DateTime? ExpiresAt,
    bool     IsValid
);

// ── DISCOUNT VALIDATION ───────────────────────────────────────
public record ValidateDiscountResponse(
    bool    Valid,
    string? Message,
    decimal DiscountAmount,
    decimal DiscountPercent
);
