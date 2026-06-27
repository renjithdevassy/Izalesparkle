using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using IzaleSparkle.Domain.Common;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Enums;
using IzaleSparkle.Domain.ValueObjects;

namespace IzaleSparkle.Infrastructure.Persistence;

// ── DBCONTEXT ─────────────────────────────────────────────────
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product>      Products      => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<Review>       Reviews       => Set<Review>();
    public DbSet<Order>        Orders        => Set<Order>();
    public DbSet<OrderItem>    OrderItems    => Set<OrderItem>();
    public DbSet<AppUser>         Users           => Set<AppUser>();
    public DbSet<StockPurchase>   StockPurchases  => Set<StockPurchase>();
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<DiscountCode>    DiscountCodes   => Set<DiscountCode>();
    public DbSet<Category>         Categories      => Set<Category>();
    public DbSet<SiteVisit>        SiteVisits      => Set<SiteVisit>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Explicitly tell EF Core to ignore BaseEvent — it is a domain-only
        // in-memory concept and must never be mapped to a database table.
        // This is the belt-and-braces companion to [NotMapped] on BaseEntity.DomainEvents.
        mb.Ignore<BaseEvent>();

        // ── Category ─────────────────────────────────────────────
        mb.Entity<Category>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Name).HasMaxLength(100).IsRequired();
            b.Property(c => c.Slug).HasMaxLength(100).IsRequired();
            b.HasIndex(c => c.Slug).IsUnique();
            b.Property(c => c.Icon).HasMaxLength(10).HasDefaultValue("💎");
            b.Ignore(c => c.ProductCount);
        });

        // ── DiscountCode ──────────────────────────────────────────
        mb.Entity<DiscountCode>(b =>
        {
            b.HasKey(d => d.Id);
            b.Property(d => d.Code).HasMaxLength(50).IsRequired();
            b.HasIndex(d => d.Code).IsUnique();
            b.Property(d => d.DiscountPercent).HasPrecision(5, 2);
        });
        mb.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(mb);
    }
}

// ── ENTITY CONFIGURATIONS ────────────────────────────────────
public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasKey(p => p.Id);
        b.Property(p => p.Name).IsRequired().HasMaxLength(200);
        b.Property(p => p.Slug).IsRequired().HasMaxLength(250);
        b.HasIndex(p => p.Slug).IsUnique();
        b.Property(p => p.Description).HasMaxLength(2000);
        b.Property(p => p.Material).HasMaxLength(200);
        b.Property(p => p.Category).HasMaxLength(100).IsRequired();
        b.Property(p => p.Badge).HasConversion<string?>();
        b.Property(p => p.IsActive).HasDefaultValue(true);

        // Money value objects as owned types
        b.OwnsOne(p => p.Price, m =>
        {
            m.Property(x => x.Amount).HasColumnName("Price").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("Currency").HasMaxLength(3).HasDefaultValue("GBP");
        });
        b.OwnsOne(p => p.OldPrice, m =>
        {
            m.Property(x => x.Amount).HasColumnName("OldPrice").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("OldPriceCurrency").HasMaxLength(3).HasDefaultValue("GBP");
        });
        b.OwnsOne(p => p.CostPrice, m =>
        {
            m.Property(x => x.Amount).HasColumnName("CostPrice").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("CostPriceCurrency").HasMaxLength(3).HasDefaultValue("GBP");
        });

        b.HasMany(p => p.Images).WithOne(i => i.Product).HasForeignKey(i => i.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(p => p.Reviews).WithOne(r => r.Product).HasForeignKey(r => r.ProductId).OnDelete(DeleteBehavior.Cascade);

        // Computed properties — not stored in DB
        b.Property(p => p.ReorderPoint).HasDefaultValue(2);
        b.Property(p => p.LeadTimeDays).HasDefaultValue(14);
        b.Property(p => p.Supplier).HasMaxLength(200);
        b.Property(p => p.SupplierSku).HasMaxLength(100);

        b.HasMany(p => p.StockPurchases).WithOne(s => s.Product).HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.Ignore(p => p.IsOnSale);
        b.Ignore(p => p.SavePercent);
        b.Ignore(p => p.MarginPercent);
        b.Property(p => p.IsVatApplicable).HasDefaultValue(true);
        b.Ignore(p => p.LowStock);
        b.Ignore(p => p.OutOfStock);
        b.Ignore(p => p.TotalPurchased);
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.HasKey(o => o.Id);
        b.Property(o => o.OrderNumber).IsRequired().HasMaxLength(30);
        b.HasIndex(o => o.OrderNumber).IsUnique();
        b.Property(o => o.CustomerEmail).IsRequired().HasMaxLength(256);
        b.Property(o => o.Status).HasConversion<string>();
        b.Property(o => o.PaymentMethod).HasConversion<string>();
        b.Property(o => o.ShippingTier).HasConversion<string>();

        b.OwnsOne(o => o.ShippingAddress, a =>
        {
            a.Property(x => x.FirstName).HasColumnName("ShipFirstName").HasMaxLength(100);
            a.Property(x => x.LastName).HasColumnName("ShipLastName").HasMaxLength(100);
            a.Property(x => x.Line1).HasColumnName("ShipLine1").HasMaxLength(200);
            a.Property(x => x.Line2).HasColumnName("ShipLine2").HasMaxLength(200);
            a.Property(x => x.City).HasColumnName("ShipCity").HasMaxLength(100);
            a.Property(x => x.Postcode).HasColumnName("ShipPostcode").HasMaxLength(20);
            a.Property(x => x.Country).HasColumnName("ShipCountry").HasMaxLength(100);
        });

        b.OwnsOne(o => o.ShippingCost, m =>
        {
            m.Property(x => x.Amount).HasColumnName("ShippingCost").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("ShippingCurrency").HasMaxLength(3).HasDefaultValue("GBP");
        });
        b.OwnsOne(o => o.Discount, m =>
        {
            m.Property(x => x.Amount).HasColumnName("Discount").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("DiscountCurrency").HasMaxLength(3).HasDefaultValue("GBP");
        });

        b.HasMany(o => o.Items).WithOne(i => i.Order).HasForeignKey(i => i.OrderId).OnDelete(DeleteBehavior.Cascade);

        // Computed — not stored
        b.Property(o => o.TrackingNumber).HasMaxLength(200);
        b.Ignore(o => o.Subtotal);
        b.Ignore(o => o.Vat);
        b.Ignore(o => o.Total);
        b.OwnsOne(o => o.StoredVat, m =>
        {
            m.Property(x => x.Amount).HasColumnName("VatAmount").HasPrecision(18,2);
            m.Property(x => x.Currency).HasColumnName("VatCurrency").HasMaxLength(3).HasDefaultValue("GBP");
        });
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.HasKey(i => i.Id);
        b.Property(i => i.ProductName).HasMaxLength(200);
        b.Property(i => i.Metal).HasConversion<string>();

        b.OwnsOne(i => i.UnitPrice, m =>
        {
            m.Property(x => x.Amount).HasColumnName("UnitPrice").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("UnitPriceCurrency").HasMaxLength(3).HasDefaultValue("GBP");
        });

        b.Ignore(i => i.LineTotal);
    }
}

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> b)
    {
        b.HasKey(u => u.Id);
        b.Property(u => u.Email).IsRequired().HasMaxLength(256);
        b.HasIndex(u => u.Email).IsUnique();
        b.Property(u => u.FirstName).HasMaxLength(100);
        b.Property(u => u.LastName).HasMaxLength(100);
        b.Property(u => u.PasswordHash).HasMaxLength(512);
        b.Property(u => u.Role).HasConversion<string>();
    }
}

public class StockPurchaseConfiguration : IEntityTypeConfiguration<StockPurchase>
{
    public void Configure(EntityTypeBuilder<StockPurchase> b)
    {
        b.HasKey(s => s.Id);
        b.Property(s => s.Supplier).HasMaxLength(200);
        b.Property(s => s.Reference).HasMaxLength(100);
        b.Property(s => s.Notes).HasMaxLength(500);
        b.OwnsOne(s => s.UnitCost, m =>
        {
            m.Property(x => x.Amount).HasColumnName("UnitCost").HasPrecision(18, 2);
            m.Property(x => x.Currency).HasColumnName("UnitCostCurrency").HasMaxLength(3).HasDefaultValue("GBP");
        });
        b.Ignore(s => s.TotalCost);
    }
}

public class ProductAttributeConfiguration : IEntityTypeConfiguration<ProductAttribute>
{
    public void Configure(EntityTypeBuilder<ProductAttribute> b)
    {
        b.HasKey(a => a.Id);
        b.Property(a => a.Name).IsRequired().HasMaxLength(100);
        b.Property(a => a.Value).IsRequired().HasMaxLength(500);
        b.Property(a => a.IsEnabled).HasDefaultValue(false);
    }
}

public class SiteVisitConfiguration : IEntityTypeConfiguration<SiteVisit>
{
    public void Configure(EntityTypeBuilder<SiteVisit> b)
    {
        b.HasKey(v => v.Id);
        b.Property(v => v.Path).HasMaxLength(300);
        b.HasIndex(v => v.CreatedAt);
    }
}
