using IzaleSparkle.Domain.Common;
using IzaleSparkle.Domain.Enums;
using IzaleSparkle.Domain.Events;
using IzaleSparkle.Domain.ValueObjects;

namespace IzaleSparkle.Domain.Entities;

// ── PRODUCT ──────────────────────────────────────────────────
public class Product : BaseEntity
{
    public string Name        { get; private set; } = string.Empty;
    public string Slug        { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Material    { get; private set; } = string.Empty;
    public Money Price        { get; private set; } = Money.Zero;
    public Money? OldPrice    { get; private set; }
    public string Category { get; private set; } = "rings";
    public BadgeType? Badge   { get; private set; }
    public int Stars          { get; private set; } = 5;
    public string ImageUrl    { get; private set; } = string.Empty;
    public bool IsActive      { get; private set; } = true;
    /// <summary>Whether VAT (20%) applies to this product. Defaults to false (VAT exempt).</summary>
    public bool IsVatApplicable { get; private set; } = false;
    /// <summary>Internal purchase/cost price — never exposed to customers.</summary>
    public Money? CostPrice      { get; private set; }
    /// <summary>Minimum stock level before reorder alert is triggered.</summary>
    public int    ReorderPoint   { get; private set; } = 2;
    /// <summary>Lead time in days to restock from supplier.</summary>
    public int    LeadTimeDays   { get; private set; } = 14;
    /// <summary>Supplier name — admin-only.</summary>
    public string? Supplier      { get; private set; }
    /// <summary>Supplier SKU / reference — admin-only.</summary>
    public string? SupplierSku   { get; private set; }
    /// <summary>Current stock level — managed by StockPurchase records.</summary>
    public int    StockLevel     { get; set; } = 0;
    public bool   LowStock       => StockLevel <= ReorderPoint && StockLevel > 0;
    public bool   OutOfStock     => StockLevel <= 0;

    private readonly List<StockPurchase> _stockPurchases = new();
    public IReadOnlyList<StockPurchase> StockPurchases => _stockPurchases.AsReadOnly();
    public int TotalPurchased => _stockPurchases.Sum(p => p.Quantity);

    private readonly List<ProductImage> _images = new();
    public IReadOnlyList<ProductImage> Images => _images.AsReadOnly();

    private readonly List<Review> _reviews = new();
    public IReadOnlyList<Review> Reviews => _reviews.AsReadOnly();

    // EF ctor
    protected Product() { }

    public static Product Create(
        string name, string description, string material,
        decimal price, string category,
        string imageUrl, int stars = 5,
        decimal? oldPrice = null, BadgeType? badge = null,
        decimal? costPrice = null, bool isVatApplicable = false)
    {
        var product = new Product
        {
            Name        = name,
            Slug        = name.ToLower().Replace(" ", "-"),
            Description = description,
            Material    = material,
            Price       = new Money(price),
            OldPrice    = oldPrice.HasValue ? new Money(oldPrice.Value) : null,
            CostPrice   = costPrice.HasValue ? new Money(costPrice.Value) : null,
            Category    = Entities.Category.ToSlug(category),
            Badge       = badge,
            Stars       = stars,
            ImageUrl    = imageUrl,
            IsVatApplicable = isVatApplicable,
        };
        product.AddDomainEvent(new ProductCreatedEvent(product));
        return product;
    }

    public void AddImage(string url, bool isPrimary = false)
        => _images.Add(new ProductImage { Url = url, IsPrimary = isPrimary, ProductId = Id });

    public void ClearImages() => _images.Clear();

    public void UpdateCategory(string category) => Category = Entities.Category.ToSlug(category);

    public void AddStockPurchase(int qty, decimal unitCost, string? supplier, string? reference, DateTime? purchasedOn)
        => _stockPurchases.Add(new StockPurchase
        {
            ProductId    = Id,
            Quantity     = qty,
            UnitCost     = new IzaleSparkle.Domain.ValueObjects.Money(unitCost),
            Supplier     = supplier,
            Reference    = reference,
            PurchasedOn  = purchasedOn ?? DateTime.UtcNow,
        });

    /// <summary>Reduce stock by the specified quantity when an order is placed.</summary>
    public void ReduceStock(int qty)
    {
        if (qty <= 0) return;
        if (StockLevel < qty)
            throw new InvalidOperationException($"Cannot reduce stock by {qty} — only {StockLevel} items available for {Name}");
        StockLevel -= qty;
    }

    /// <summary>True when an OldPrice (RRP/original price) is set and higher than selling price.</summary>
    public bool IsOnSale  => OldPrice != null && OldPrice.Amount > Price.Amount;

    /// <summary>Saving percentage: how much % off the original price.
    /// Uses Ceiling so even a tiny discount shows at least 1%.
    /// OldPrice=13, Price=12 → saves £1 → (13-12)/13 = 7.7% → 8% saved.
    /// OldPrice=13, Price=12.99 → saves 1p → 0.08% → shows 1% (ceiling).</summary>
    public int? SavePercent => OldPrice != null && OldPrice.Amount > Price.Amount && OldPrice.Amount > 0
        ? Math.Max(1, (int)Math.Ceiling((OldPrice.Amount - Price.Amount) / OldPrice.Amount * 100))
        : null;
    /// <summary>Gross margin % — only meaningful if CostPrice is set.</summary>
    public int? MarginPercent => CostPrice != null && CostPrice.Amount > 0 && Price.Amount > 0
        ? (int)Math.Round((Price.Amount - CostPrice.Amount) / Price.Amount * 100)
        : null;
}

// ── PRODUCT IMAGE ─────────────────────────────────────────────
public class ProductImage
{
    public int Id          { get; set; }
    public int ProductId   { get; set; }
    public string Url      { get; set; } = string.Empty;
    public bool IsPrimary  { get; set; }
    public int SortOrder   { get; set; }
    public Product? Product { get; set; }
}

// ── REVIEW ───────────────────────────────────────────────────
public class Review : BaseEntity
{
    public int ProductId    { get; set; }
    public string AuthorName{ get; set; } = string.Empty;
    public string Title     { get; set; } = string.Empty;
    public string Body      { get; set; } = string.Empty;
    public int Stars        { get; set; }
    public bool Verified    { get; set; }
    public int HelpfulCount { get; set; }
    public Product? Product { get; set; }
}

// ── ORDER ────────────────────────────────────────────────────
public class Order : BaseEntity
{
    public string OrderNumber  { get; private set; } = string.Empty;
    public string CustomerEmail{ get; private set; } = string.Empty;
    public Address ShippingAddress { get; private set; } = null!;
    public OrderStatus Status  { get; private set; } = OrderStatus.Pending;
    public PaymentMethod PaymentMethod { get; private set; }
    public ShippingTier ShippingTier  { get; private set; }
    public Money ShippingCost  { get; private set; } = Money.Zero;
    public Money Discount      { get; private set; } = Money.Zero;
    public string? PromoCode   { get; private set; }
    public string? Notes          { get; private set; }
    public string? TrackingNumber { get; private set; }

    private readonly List<OrderItem> _items = new();
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public Money Subtotal => new(_items.Sum(i => i.LineTotal.Amount));
    public Money? StoredVat   { get; private set; }  // set when order is created
    public Money Vat      => StoredVat ?? new Money(0);
    public Money Total    => new(Subtotal.Amount + ShippingCost.Amount + Vat.Amount - Discount.Amount);

    protected Order() { }

    public static Order Create(
        string email, Address address,
        PaymentMethod payment, ShippingTier shipping, decimal shippingCost,
        string? promoCode = null, decimal discount = 0, string? notes = null,
        decimal vatAmount = 0)
    {
        var order = new Order
        {
            OrderNumber    = $"IS{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}",
            CustomerEmail  = email,
            ShippingAddress= address,
            PaymentMethod  = payment,
            ShippingTier   = shipping,
            ShippingCost   = new Money(shippingCost),
            PromoCode      = promoCode,
            Discount       = new Money(discount),
            Notes          = notes,
            Status         = OrderStatus.Pending,
            StoredVat      = vatAmount > 0 ? new Money(vatAmount) : null,
        };
        order.AddDomainEvent(new OrderPlacedEvent(order));
        return order;
    }

    public void AddItem(Product product, int qty, MetalType metal, string? size)
        => _items.Add(OrderItem.Create(Id, product, qty, metal, size));

    public bool RemoveItem(int productId)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null) return false;
        _items.Remove(item);
        return true;
    }

    public bool UpdateItemQty(int productId, int newQty)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId);
        if (item == null) return false;
        if (newQty <= 0) { _items.Remove(item); return true; }
        item.Quantity = newQty;
        return true;
    }

    public void UpdateStatus(OrderStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new OrderStatusChangedEvent(this, status));
    }
}

// ── ORDER ITEM ────────────────────────────────────────────────
public class OrderItem : BaseEntity
{
    public int OrderId      { get; private set; }
    public int ProductId    { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string ImageUrl  { get; private set; } = string.Empty;
    public Money UnitPrice  { get; private set; } = Money.Zero;
    public int Quantity     { get; set; }          // mutable for admin edits
    public MetalType Metal  { get; private set; }
    public string? Size     { get; private set; }
    public Money LineTotal  => UnitPrice.Multiply(Quantity);

    public Order? Order     { get; private set; }
    public Product? Product { get; private set; }

    protected OrderItem() { }

    public static OrderItem Create(int orderId, Product product, int qty, MetalType metal, string? size) => new()
    {
        OrderId     = orderId,
        ProductId   = product.Id,
        ProductName = product.Name,
        ImageUrl    = product.ImageUrl,
        UnitPrice   = product.Price,
        Quantity    = qty,
        Metal       = metal,
        Size        = size,
    };
}

// ── APP USER ──────────────────────────────────────────────────
public class AppUser : BaseEntity
{
    public string Email          { get; private set; } = string.Empty;
    public string FirstName      { get; private set; } = string.Empty;
    public string LastName       { get; private set; } = string.Empty;
    public string PasswordHash   { get; private set; } = string.Empty;
    public UserRole Role         { get; private set; } = UserRole.Customer;
    public bool    IsActive      { get; private set; } = true;
    public DateTime? LastLoginAt { get; set; }

    protected AppUser() { }

    public static AppUser Create(string email, string firstName, string lastName,
        string passwordHash, UserRole role = UserRole.Customer)
        => new()
        {
            Email        = email.ToLower().Trim(),
            FirstName    = firstName.Trim(),
            LastName     = lastName.Trim(),
            PasswordHash = passwordHash,
            Role         = role,
            IsActive     = true,
        };

    public string FullName => $"{FirstName} {LastName}";
}

// ── STOCK PURCHASE ────────────────────────────────────────────
/// <summary>Records a single stock purchase / goods-in event.</summary>
public class StockPurchase : BaseEntity
{
    public int      ProductId   { get; set; }
    public int      Quantity    { get; set; }
    public IzaleSparkle.Domain.ValueObjects.Money UnitCost { get; set; } = IzaleSparkle.Domain.ValueObjects.Money.Zero;
    public string?  Supplier    { get; set; }
    public string?  Reference   { get; set; }  // PO number / invoice ref
    public string?  Notes       { get; set; }
    public DateTime PurchasedOn { get; set; } = DateTime.UtcNow;
    public Product? Product     { get; set; }

    public IzaleSparkle.Domain.ValueObjects.Money TotalCost =>
        new(UnitCost.Amount * Quantity, UnitCost.Currency);
}

// ── PRODUCT ATTRIBUTE (future feature) ───────────────────────
/// <summary>
/// Optional product attributes (colour, size range, material variants, etc.).
/// Marked IsEnabled=false until the feature is activated.
/// Not shown on the storefront — admin-only placeholder.
/// </summary>
public class ProductAttribute : BaseEntity
{
    public int     ProductId  { get; set; }
    public string  Name       { get; set; } = string.Empty;  // e.g. "Colour", "Stone"
    public string  Value      { get; set; } = string.Empty;  // e.g. "Rose Gold", "Sapphire"
    public int     SortOrder  { get; set; } = 0;
    public bool    IsEnabled  { get; set; } = false;  // hidden until feature is activated
    public Product? Product   { get; set; }
}

// ── DISCOUNT CODE ─────────────────────────────────────────────
/// <summary>Admin-managed discount codes. Replaces the hardcoded promo set.</summary>
public class DiscountCode : BaseEntity
{
    public string  Code            { get; set; } = string.Empty;
    public string  Description     { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }         // e.g. 10 = 10%
    public bool    IsActive        { get; set; } = true;
    public int?    MaxUses         { get; set; }         // null = unlimited
    public int     TimesUsed       { get; set; } = 0;
    public DateTime? ExpiresAt     { get; set; }         // null = never expires

    public bool IsValid =>
        IsActive &&
        (MaxUses == null || TimesUsed < MaxUses) &&
        (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);

    public decimal Apply(decimal subtotal) =>
        Math.Round(subtotal * DiscountPercent / 100, 2);
}

// ── CATEGORY ──────────────────────────────────────────────────
/// <summary>Storefront product category — managed via admin panel.</summary>
public class Category : BaseEntity
{
    public string  Name        { get; set; } = string.Empty;
    public string  Slug        { get; set; } = string.Empty;
    public string  Description { get; set; } = string.Empty;
    public string  Icon        { get; set; } = "💎";       // emoji icon
    public int     SortOrder   { get; set; } = 0;
    public bool    IsActive    { get; set; } = true;

    // Computed — not stored
    public int ProductCount    { get; set; } = 0;

    public static string ToSlug(string name) =>
        name.ToLower().Trim().Replace(" ", "-")
            .Replace("'", "").Replace("&", "and");
}

// ── SITE VISIT ───────────────────────────────────────────────
/// <summary>
/// One row per visit to the public website. Used to show a simple
/// "website views" counter in the admin dashboard. CreatedAt (from
/// BaseEntity) records the visit time.
/// </summary>
public class SiteVisit : BaseEntity
{
    /// <summary>Relative path that was visited (e.g. "/", "shop"). Optional.</summary>
    public string? Path { get; set; }
}
