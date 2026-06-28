using Microsoft.Extensions.Configuration;
using IzaleSparkle.Domain.Entities;
using IzaleSparkle.Domain.Enums;
using IzaleSparkle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IzaleSparkle.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db,
        Microsoft.Extensions.Configuration.IConfiguration? config = null)
    {
        await db.Database.EnsureCreatedAsync();

        // The schema is created via EnsureCreated, which does NOT add new tables to an
        // already-existing database. Guard-create tables added after the first deploy so
        // existing production databases pick them up without a full migration.
        await EnsureSiteVisitsTableAsync(db);
        await EnsurePasswordResetColumnsAsync(db);

        // Seed admin user
        if (!await db.Users.AnyAsync(u => u.Role == UserRole.Admin))
        {
            var adminEmail    = config?["AdminSeed:Email"]     ?? "admin@izalesparkle.com";
            var adminPassword = config?["AdminSeed:Password"];
            if (string.IsNullOrWhiteSpace(adminPassword)) adminPassword = "Admin@123!";
            var adminFirst    = config?["AdminSeed:FirstName"] ?? "Izale";
            var adminLast     = config?["AdminSeed:LastName"]  ?? "Admin";
            var hash          = global::BCrypt.Net.BCrypt.HashPassword(adminPassword, workFactor: 12);
            var admin         = AppUser.Create(adminEmail, adminFirst, adminLast, hash, UserRole.Admin);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }

        // Seed default categories without removing custom admin-created categories.
        if (!await db.Categories.AnyAsync())
        {
            db.Categories.AddRange(DefaultCategories());
            await db.SaveChangesAsync();
        }

        // Normalize legacy enum-style product categories ("Rings") to slugs ("rings")
        // while preserving custom categories added in the admin panel.
        var existingProducts = await db.Products.ToListAsync();
        foreach (var product in existingProducts)
        {
            var normalized = Category.ToSlug(product.Category);
            if (!product.Category.Equals(normalized, StringComparison.Ordinal))
                product.UpdateCategory(normalized);
        }
        if (existingProducts.Any())
            await db.SaveChangesAsync();

        if (await db.Products.AnyAsync())
        {
            await SeedDiscountCodesAsync(db);
            return;
        }

        var products = new[]
        {
            // ── RINGS (10) ──────────────────────────────────────
            Make("Diamond Solitaire Ring",
                "18K White Gold & 1.2ct Diamond",
                "Our most iconic ring — a 1.2ct GIA-certified brilliant-cut diamond in a signature four-claw setting. Handcrafted in solid 18K gold with free engraving.",
                3850, ProductCategory.Rings, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=600&q=80",
                Imgs("1605100804763-247f67b3557e","1535632066927-ab7c9ab60908","1602173574767-37ac01994b2a","1515562141207-7a88fb7ce338","1617038260897-41a1f14a8ca0")),

            Make("Eternity Diamond Band",
                "18K Yellow Gold & Full Pavé Diamonds",
                "A full eternity band featuring pavé-set diamonds all the way around. The ultimate symbol of endless love.",
                4200, ProductCategory.Rings, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?w=600&q=80",
                Imgs("1535632066927-ab7c9ab60908","1605100804763-247f67b3557e","1602173574767-37ac01994b2a","1515562141207-7a88fb7ce338","1617038260897-41a1f14a8ca0")),

            Make("Sapphire Cocktail Ring",
                "18K White Gold & Ceylon Sapphire",
                "A statement cocktail ring featuring a deep blue Ceylon sapphire surrounded by a halo of brilliant-cut diamonds.",
                5600, ProductCategory.Rings, 5, BadgeType.Bestseller, null,
                "https://images.unsplash.com/photo-1602173574767-37ac01994b2a?w=600&q=80",
                Imgs("1602173574767-37ac01994b2a","1605100804763-247f67b3557e","1535632066927-ab7c9ab60908","1515562141207-7a88fb7ce338","1617038260897-41a1f14a8ca0")),

            Make("Ruby Cluster Ring",
                "18K Rose Gold & Burmese Rubies",
                "A stunning cluster of vivid Burmese rubies and diamonds in 18K rose gold. Bold, beautiful, and utterly unique.",
                3200, ProductCategory.Rings, 5, BadgeType.Sale, 3900m,
                "https://images.unsplash.com/photo-1515562141207-7a88fb7ce338?w=600&q=80",
                Imgs("1515562141207-7a88fb7ce338","1605100804763-247f67b3557e","1535632066927-ab7c9ab60908","1602173574767-37ac01994b2a","1617038260897-41a1f14a8ca0")),

            Make("Rose Gold Diamond Ring",
                "18K Rose Gold & 0.8ct Diamond",
                "A delicate rose gold band cradling a round brilliant diamond. Modern romance at its finest.",
                2850, ProductCategory.Rings, 4, null, null,
                "https://images.unsplash.com/photo-1589207212797-cfd578532af8?w=600&q=80",
                Imgs("1589207212797-cfd578532af8","1605100804763-247f67b3557e","1535632066927-ab7c9ab60908","1602173574767-37ac01994b2a","1617038260897-41a1f14a8ca0")),

            Make("Emerald Three Stone Ring",
                "18K Platinum & Colombian Emeralds",
                "Three matched Colombian emeralds set in platinum. A trio of colour and brilliance for the woman who commands attention.",
                6800, ProductCategory.Rings, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=600&q=80",
                Imgs("1617038260897-41a1f14a8ca0","1605100804763-247f67b3557e","1535632066927-ab7c9ab60908","1602173574767-37ac01994b2a","1515562141207-7a88fb7ce338")),

            Make("Princess Cut Engagement Ring",
                "18K White Gold & Princess Cut Diamond",
                "A princess-cut diamond of exceptional clarity, set in a modern tension-inspired platinum mount. For the bold and the brilliant.",
                7200, ProductCategory.Rings, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?w=600&q=80",
                Imgs("1535632066927-ab7c9ab60908","1605100804763-247f67b3557e","1535632066927-ab7c9ab60908","1602173574767-37ac01994b2a","1617038260897-41a1f14a8ca0")),

            Make("Vintage Art Deco Ring",
                "18K Yellow Gold & Old Cut Diamonds",
                "Inspired by the golden age of Art Deco, this ring features old-cut diamonds in a geometric milgrain setting. Timeless glamour.",
                4100, ProductCategory.Rings, 5, null, null,
                "https://images.unsplash.com/photo-1611591437281-460bfbe1220a?w=600&q=80",
                Imgs("1611591437281-460bfbe1220a","1605100804763-247f67b3557e","1535632066927-ab7c9ab60908","1602173574767-37ac01994b2a","1617038260897-41a1f14a8ca0")),

            Make("Aquamarine Halo Ring",
                "18K White Gold & Aquamarine",
                "A sea-blue aquamarine ringed by a halo of white diamonds. Cool, crisp and effortlessly sophisticated.",
                2400, ProductCategory.Rings, 4, BadgeType.Sale, 2900m,
                "https://images.unsplash.com/photo-1608042314453-ae338d9c6ad1?w=600&q=80",
                Imgs("1608042314453-ae338d9c6ad1","1605100804763-247f67b3557e","1535632066927-ab7c9ab60908","1602173574767-37ac01994b2a","1617038260897-41a1f14a8ca0")),

            Make("Gold Stackable Band Set",
                "18K Yellow Gold — Set of 3",
                "Three delicate 18K yellow gold bands designed to be worn together or separately. Mix and match for a personalised look.",
                1650, ProductCategory.Rings, 5, null, null,
                "https://images.unsplash.com/photo-1506630448388-4e683c67ddb0?w=600&q=80",
                Imgs("1506630448388-4e683c67ddb0","1605100804763-247f67b3557e","1535632066927-ab7c9ab60908","1602173574767-37ac01994b2a","1617038260897-41a1f14a8ca0")),

            // ── NECKLACES (8) ────────────────────────────────────
            Make("Sapphire Pendant Necklace",
                "18K Yellow Gold & Ceylon Sapphire",
                "A stunning Ceylon sapphire pendant set in 18K yellow gold. The 45cm chain allows the stone to sit beautifully at the neckline.",
                2240, ProductCategory.Necklaces, 5, BadgeType.Sale, 2800m,
                "https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=600&q=80",
                Imgs("1599643478518-a784e5dc4c8f","1506630448388-4e683c67ddb0","1608042314453-ae338d9c6ad1","1611591437281-460bfbe1220a","1617038260897-41a1f14a8ca0")),

            Make("Diamond Tennis Necklace",
                "18K White Gold & Diamond Chain",
                "A continuous row of brilliant-cut diamonds set in 18K white gold. The ultimate jewellery statement piece.",
                6800, ProductCategory.Necklaces, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1506630448388-4e683c67ddb0?w=600&q=80",
                Imgs("1506630448388-4e683c67ddb0","1599643478518-a784e5dc4c8f","1608042314453-ae338d9c6ad1","1611591437281-460bfbe1220a","1617038260897-41a1f14a8ca0")),

            Make("Emerald Statement Necklace",
                "18K White Gold & Colombian Emerald",
                "A rare Colombian emerald set in an 18K white gold halo pendant. An investment piece and a statement of extraordinary taste.",
                8400, ProductCategory.Necklaces, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1608042314453-ae338d9c6ad1?w=600&q=80",
                Imgs("1608042314453-ae338d9c6ad1","1506630448388-4e683c67ddb0","1599643478518-a784e5dc4c8f","1611591437281-460bfbe1220a","1617038260897-41a1f14a8ca0")),

            Make("Diamond Solitaire Pendant",
                "18K White Gold & 0.5ct Diamond",
                "A timeless floating diamond solitaire in 18K white gold. Simple, stunning, and endlessly elegant.",
                1850, ProductCategory.Necklaces, 5, null, null,
                "https://images.unsplash.com/photo-1611591437281-460bfbe1220a?w=600&q=80",
                Imgs("1611591437281-460bfbe1220a","1506630448388-4e683c67ddb0","1599643478518-a784e5dc4c8f","1608042314453-ae338d9c6ad1","1617038260897-41a1f14a8ca0")),

            Make("Gold Chain Necklace",
                "Solid 18K Yellow Gold · 50cm",
                "A classic solid 18K yellow gold curb chain. The foundation of every jewellery wardrobe.",
                1480, ProductCategory.Necklaces, 4, null, null,
                "https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=600&q=80",
                Imgs("1617038260897-41a1f14a8ca0","1506630448388-4e683c67ddb0","1599643478518-a784e5dc4c8f","1608042314453-ae338d9c6ad1","1611591437281-460bfbe1220a")),

            Make("Pearl Statement Necklace",
                "18K Gold & South Sea Pearls",
                "A graduated strand of lustrous South Sea pearls on an 18K gold clasp. Classic, refined, unforgettable.",
                3200, ProductCategory.Necklaces, 5, BadgeType.Bestseller, null,
                "https://images.unsplash.com/photo-1515562141207-7a88fb7ce338?w=600&q=80",
                Imgs("1515562141207-7a88fb7ce338","1506630448388-4e683c67ddb0","1599643478518-a784e5dc4c8f","1608042314453-ae338d9c6ad1","1617038260897-41a1f14a8ca0")),

            Make("Rose Gold Heart Pendant",
                "18K Rose Gold & Diamond",
                "A heart-shaped pendant traced in pavé diamonds on an 18K rose gold chain. The perfect expression of love.",
                1620, ProductCategory.Necklaces, 4, BadgeType.Sale, 2000m,
                "https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?w=600&q=80",
                Imgs("1535632066927-ab7c9ab60908","1506630448388-4e683c67ddb0","1599643478518-a784e5dc4c8f","1608042314453-ae338d9c6ad1","1617038260897-41a1f14a8ca0")),

            Make("Layered Gold Necklace Set",
                "18K Gold · Set of 2",
                "Two complementary 18K gold necklaces designed to layer beautifully. The shorter choker and longer pendant work in harmony.",
                2100, ProductCategory.Necklaces, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1602173574767-37ac01994b2a?w=600&q=80",
                Imgs("1602173574767-37ac01994b2a","1506630448388-4e683c67ddb0","1599643478518-a784e5dc4c8f","1608042314453-ae338d9c6ad1","1617038260897-41a1f14a8ca0")),

            // ── EARRINGS (7) ─────────────────────────────────────
            Make("Diamond Drop Earrings",
                "18K Rose Gold & Pavé Diamonds",
                "Elegant drop earrings featuring pavé-set diamonds in 18K rose gold. Perfectly weighted for all-day wear.",
                1680, ProductCategory.Earrings, 4, null, null,
                "https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?w=600&q=80",
                Imgs("1535632066927-ab7c9ab60908","1617038260897-41a1f14a8ca0","1589207212797-cfd578532af8","1605100804763-247f67b3557e","1611085583191-a3b181a88401")),

            Make("Pearl Drop Earrings",
                "18K Gold & South Sea Pearls",
                "South Sea pearl drop earrings in 18K yellow gold. Classic elegance that never goes out of style.",
                890, ProductCategory.Earrings, 4, BadgeType.Sale, 1100m,
                "https://images.unsplash.com/photo-1589207212797-cfd578532af8?w=600&q=80",
                Imgs("1589207212797-cfd578532af8","1535632066927-ab7c9ab60908","1617038260897-41a1f14a8ca0","1611085583191-a3b181a88401","1605100804763-247f67b3557e")),

            Make("Akoya Pearl Stud Earrings",
                "18K Gold & Akoya Pearls",
                "Classic Akoya pearl studs in 18K gold. Every woman's essential jewellery piece, perfected.",
                1240, ProductCategory.Earrings, 4, null, null,
                "https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=600&q=80",
                Imgs("1617038260897-41a1f14a8ca0","1589207212797-cfd578532af8","1535632066927-ab7c9ab60908","1611085583191-a3b181a88401","1605100804763-247f67b3557e")),

            Make("Diamond Hoop Earrings",
                "18K White Gold & Diamond Hoops",
                "Sleek 18K white gold hoops set with brilliant-cut diamonds. The ultimate day-to-evening earring.",
                2200, ProductCategory.Earrings, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1611085583191-a3b181a88401?w=600&q=80",
                Imgs("1611085583191-a3b181a88401","1535632066927-ab7c9ab60908","1617038260897-41a1f14a8ca0","1589207212797-cfd578532af8","1605100804763-247f67b3557e")),

            Make("Sapphire Stud Earrings",
                "18K White Gold & Blue Sapphires",
                "A matched pair of vivid blue sapphires in 18K white gold settings. Simply beautiful.",
                1750, ProductCategory.Earrings, 5, BadgeType.Bestseller, null,
                "https://images.unsplash.com/photo-1611591437281-460bfbe1220a?w=600&q=80",
                Imgs("1611591437281-460bfbe1220a","1535632066927-ab7c9ab60908","1617038260897-41a1f14a8ca0","1589207212797-cfd578532af8","1605100804763-247f67b3557e")),

            Make("Emerald Ear Climbers",
                "18K Gold & Colombian Emeralds",
                "Avant-garde ear climbers featuring Colombian emeralds that trace the ear's curve. For the woman who dares.",
                3100, ProductCategory.Earrings, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1608042314453-ae338d9c6ad1?w=600&q=80",
                Imgs("1608042314453-ae338d9c6ad1","1535632066927-ab7c9ab60908","1617038260897-41a1f14a8ca0","1589207212797-cfd578532af8","1605100804763-247f67b3557e")),

            Make("Gold Threader Earrings",
                "18K Yellow Gold",
                "Delicate 18K gold threads that weave through the ear, catching the light with every movement.",
                680, ProductCategory.Earrings, 4, BadgeType.Sale, 850m,
                "https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=600&q=80",
                Imgs("1599643478518-a784e5dc4c8f","1535632066927-ab7c9ab60908","1617038260897-41a1f14a8ca0","1589207212797-cfd578532af8","1605100804763-247f67b3557e")),

            // ── BRACELETS (5) ────────────────────────────────────
            Make("Gold Bangle Bracelet",
                "Solid 18K Yellow Gold",
                "Solid 18K yellow gold bangle with a polished finish. Timeless elegance that pairs beautifully with any look.",
                1950, ProductCategory.Bracelets, 5, null, null,
                "https://images.unsplash.com/photo-1611085583191-a3b181a88401?w=600&q=80",
                Imgs("1611085583191-a3b181a88401","1611591437281-460bfbe1220a","1605100804763-247f67b3557e","1535632066927-ab7c9ab60908","1608042314453-ae338d9c6ad1")),

            Make("Diamond Charm Bracelet",
                "18K Gold & Diamond Charms",
                "A delicate charm bracelet in 18K gold with diamond-set charms. Add your own story, bead by bead.",
                2750, ProductCategory.Bracelets, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1611591437281-460bfbe1220a?w=600&q=80",
                Imgs("1611591437281-460bfbe1220a","1611085583191-a3b181a88401","1605100804763-247f67b3557e","1608042314453-ae338d9c6ad1","1599643478518-a784e5dc4c8f")),

            Make("Diamond Tennis Bracelet",
                "18K White Gold & Diamond Line",
                "A classic diamond tennis bracelet — a continuous line of brilliant-cut diamonds in 18K white gold.",
                5400, ProductCategory.Bracelets, 5, BadgeType.Bestseller, null,
                "https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=600&q=80",
                Imgs("1605100804763-247f67b3557e","1611085583191-a3b181a88401","1611591437281-460bfbe1220a","1608042314453-ae338d9c6ad1","1599643478518-a784e5dc4c8f")),

            Make("Pearl Strand Bracelet",
                "18K Gold & Freshwater Pearls",
                "A single strand of perfectly matched freshwater pearls on a fine 18K gold clasp. Quietly luxurious.",
                980, ProductCategory.Bracelets, 4, BadgeType.Sale, 1200m,
                "https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?w=600&q=80",
                Imgs("1535632066927-ab7c9ab60908","1611085583191-a3b181a88401","1611591437281-460bfbe1220a","1608042314453-ae338d9c6ad1","1605100804763-247f67b3557e")),

            Make("Rose Gold Cuff Bracelet",
                "18K Rose Gold · Open Cuff",
                "A sculptural open cuff in 18K rose gold. Architecturally bold, beautifully wearable. One size fits most.",
                1680, ProductCategory.Bracelets, 5, BadgeType.New, null,
                "https://images.unsplash.com/photo-1602173574767-37ac01994b2a?w=600&q=80",
                Imgs("1602173574767-37ac01994b2a","1611085583191-a3b181a88401","1611591437281-460bfbe1220a","1608042314453-ae338d9c6ad1","1605100804763-247f67b3557e")),
        };

        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        await SeedDiscountCodesAsync(db);
    }

    /// <summary>
    /// Creates the SiteVisits table on existing databases (EnsureCreated only creates
    /// tables when the database itself is new). Safe to run repeatedly. SQL Server only.
    /// </summary>
    static async Task EnsureSiteVisitsTableAsync(AppDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SiteVisits')
BEGIN
    CREATE TABLE [SiteVisits] (
        [Id]        INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_SiteVisits] PRIMARY KEY,
        [Path]      NVARCHAR(300) NULL,
        [CreatedAt] DATETIME2 NOT NULL,
        [UpdatedAt] DATETIME2 NULL
    );
    CREATE INDEX [IX_SiteVisits_CreatedAt] ON [SiteVisits] ([CreatedAt]);
END");
        }
        catch
        {
            // Non-fatal: counter is a nice-to-have. If the provider isn't SQL Server
            // (e.g. tests) or the statement fails, the site still runs normally.
        }
    }

    /// <summary>
    /// Adds the password-reset columns to the Users table on existing databases
    /// (EnsureCreated only creates columns when the table is new). Safe to re-run.
    /// </summary>
    static async Task EnsurePasswordResetColumnsAsync(AppDbContext db)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
IF COL_LENGTH('Users', 'PasswordResetTokenHash') IS NULL
    ALTER TABLE [Users] ADD [PasswordResetTokenHash] NVARCHAR(128) NULL;
IF COL_LENGTH('Users', 'PasswordResetExpiresAt') IS NULL
    ALTER TABLE [Users] ADD [PasswordResetExpiresAt] DATETIME2 NULL;");
        }
        catch
        {
            // Non-fatal — see EnsureSiteVisitsTableAsync.
        }
    }

    static IEnumerable<Category> DefaultCategories() => new[]
    {
        new Category { Name="Rings",     Slug="rings",     Description="Engagement rings, eternity bands, cocktail rings", Icon="💍", SortOrder=1, IsActive=true },
        new Category { Name="Necklaces", Slug="necklaces", Description="Pendants, chains, statement necklaces",             Icon="📿", SortOrder=2, IsActive=true },
        new Category { Name="Earrings",  Slug="earrings",  Description="Studs, drops, hoops, ear cuffs",                    Icon="✨", SortOrder=3, IsActive=true },
        new Category { Name="Bracelets", Slug="bracelets", Description="Bangles, tennis bracelets, charm bracelets",         Icon="💎", SortOrder=4, IsActive=true }
    };

    static async Task SeedDiscountCodesAsync(AppDbContext db)
    {
        if (await db.DiscountCodes.AnyAsync()) return;

        db.DiscountCodes.AddRange(
            new DiscountCode { Code="SPARKLE10", Description="10% off — Welcome", DiscountPercent=10, IsActive=true },
            new DiscountCode { Code="IZALE10",   Description="10% off — Izale code", DiscountPercent=10, IsActive=true },
            new DiscountCode { Code="WELCOME15", Description="15% off — First order", DiscountPercent=15, IsActive=true, MaxUses=1 }
        );
        await db.SaveChangesAsync();
    }

    static Product Make(
        string name, string material, string desc,
        decimal price, ProductCategory cat, int stars,
        BadgeType? badge, decimal? oldPrice,
        string imageUrl, string[] images)
    {
        var p = Product.Create(name, desc, material, price, cat.ToString(), imageUrl, stars, oldPrice, badge);
        for (int i = 0; i < images.Length; i++)
            p.AddImage(images[i], i == 0);
        return p;
    }

    static string[] Imgs(params string[] ids) =>
        ids.Select(id => $"https://images.unsplash.com/photo-{id}?w=900&q=90").ToArray();
}
