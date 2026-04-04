namespace IzaleSparkle.Contracts.Requests;

// ── PRODUCTS ─────────────────────────────────────────────────
public record GetProductsRequest(
    string? Category    = null,
    string? Search      = null,
    decimal? MinPrice   = null,
    decimal? MaxPrice   = null,
    string  Sort        = "default",
    int     Page        = 1,
    int     PageSize    = 12
);

// ── ORDERS ───────────────────────────────────────────────────
public record PlaceOrderRequest(
    string  Email,
    string  FirstName,
    string  LastName,
    string  AddressLine1,
    string? AddressLine2,
    string  City,
    string  Postcode,
    string  Country,
    string  PaymentMethod,
    string  ShippingTier,
    string? PromoCode,
    string? GiftMessage,
    List<OrderItemRequest> Items
);

public record OrderItemRequest(
    int     ProductId,
    int     Quantity,
    string  Metal,
    string? Size
);

// ── CONTACT ──────────────────────────────────────────────────
public record ContactRequest(
    string  FirstName,
    string  LastName,
    string  Email,
    string? Phone,
    string  EnquiryType,
    string  Message
);

// ── NEWSLETTER ───────────────────────────────────────────────
public record NewsletterSubscribeRequest(string Email);

// ── ADMIN: PRODUCTS ───────────────────────────────────────────
public record CreateProductRequest(
    string  Name,
    string  Description,
    string  Material,
    decimal Price,
    decimal? OldPrice,
    string  Category,
    string? Badge,
    int     Stars,
    string  ImageUrl,
    List<string> Images,
    decimal? CostPrice = null,
    bool    IsVatApplicable = true
);

public record UpdateProductRequest(
    int     Id,
    string  Name,
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
    decimal? CostPrice = null,
    bool    IsVatApplicable = true
);

// ── ADMIN: CATEGORIES ─────────────────────────────────────────


// ── AUTH ─────────────────────────────────────────────────────
public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmPassword
);

public record LoginRequest(
    string Email,
    string Password
);

// ── ADMIN: ORDERS ─────────────────────────────────────────────
public record UpdateOrderStatusRequest(
    int      Id,
    string   Status,
    string?  AdminNotes,
    string?  DiscountCode    = null,
    decimal? DiscountAmount  = null,
    string?  TrackingNumber  = null
);

// ── STOCK PURCHASE ────────────────────────────────────────────
public record AddStockPurchaseRequest(
    int      ProductId,
    int      Quantity,
    decimal  UnitCost,
    string?  Supplier,
    string?  Reference,
    string?  Notes,
    DateTime? PurchasedOn
);

public record UpdateProductStockSettingsRequest(
    int     ProductId,
    int     ReorderPoint,
    int     LeadTimeDays,
    string? Supplier,
    string? SupplierSku
);

public record AdjustStockRequest(int NewLevel, string? Reason);

// ── DISCOUNT CODE ─────────────────────────────────────────────
public record CreateDiscountCodeRequest(
    string   Code,
    string   Description,
    decimal  DiscountPercent,
    bool     IsActive = true,
    int?     MaxUses  = null,
    DateTime? ExpiresAt = null
);
public record UpdateDiscountCodeRequest(
    int      Id,
    string   Code,
    string   Description,
    decimal  DiscountPercent,
    bool     IsActive,
    int?     MaxUses,
    DateTime? ExpiresAt
);

public record ValidateDiscountRequest(string Code, decimal Subtotal);

public record AddItemRequest(int ProductId, int Quantity, string Metal = "YellowGold18K");
public record CancelOrderRequest(string Reason = "");

public record ModifyOrderItemRequest(int ProductId, int Quantity);

public record CreateCategoryRequest(
    string Name,
    string Description,
    string Icon      = "💎",
    int    SortOrder = 0,
    bool   IsActive  = true
);
public record UpdateCategoryRequest(
    int    Id,
    string Name,
    string Description,
    string Icon,
    int    SortOrder,
    bool   IsActive
);
