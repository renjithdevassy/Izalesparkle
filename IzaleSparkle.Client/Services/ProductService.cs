using IzaleSparkle.Client.Models;

namespace IzaleSparkle.Client.Services;

public class ProductService
{
    private readonly List<Product> _products = new()
    {
        new Product { Id=1,  Name="Diamond Solitaire Ring",     Material="18K White Gold & 1.2ct Diamond",    Price=3850, Badge="new",        Category="rings",     Stars=5, Variant="Size 7 · White Gold",
            ImageUrl="https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=900&q=90","https://images.unsplash.com/photo-1573408301185-9519f94bf22b?w=900&q=90","https://images.unsplash.com/photo-1602173574767-37ac01994b2a?w=900&q=90","https://images.unsplash.com/photo-1515562141207-7a88fb7ce338?w=900&q=90","https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=900&q=90"},
            Description="Our most iconic ring — a 1.2ct GIA-certified brilliant-cut diamond in a signature four-claw setting. Handcrafted in solid 18K gold with free engraving." },

        new Product { Id=2,  Name="Sapphire Pendant Necklace", Material="18K Gold & Ceylon Sapphire",         Price=2240, OldPrice=2800, Badge="sale", Category="necklaces", Stars=5, Variant="45cm · Yellow Gold",
            ImageUrl="https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=900&q=90","https://images.unsplash.com/photo-1506630448388-4e683c67ddb0?w=900&q=90","https://images.unsplash.com/photo-1608042314453-ae338d9c6ad1?w=900&q=90","https://images.unsplash.com/photo-1611591437281-460bfbe1220a?w=900&q=90","https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=900&q=90"},
            Description="A stunning Ceylon sapphire pendant set in 18K yellow gold. The 45cm chain allows the stone to sit beautifully at the neckline." },

        new Product { Id=3,  Name="Diamond Drop Earrings",     Material="18K Rose Gold & Pavé Diamonds",      Price=1680, Badge=null,         Category="earrings",  Stars=4, Variant="Drop · Rose Gold",
            ImageUrl="https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?w=900&q=90","https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=900&q=90","https://images.unsplash.com/photo-1589207212797-cfd578532af8?w=900&q=90","https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=900&q=90","https://images.unsplash.com/photo-1611085583191-a3b181a88401?w=900&q=90"},
            Description="Elegant drop earrings featuring pavé-set diamonds in 18K rose gold. Perfectly weighted for all-day wear." },

        new Product { Id=4,  Name="Eternity Diamond Band",     Material="18K Gold & Full Pavé Diamonds",      Price=4200, Badge="new",        Category="rings",     Stars=5, Variant="Size 6 · Yellow Gold",
            ImageUrl="https://images.unsplash.com/photo-1573408301185-9519f94bf22b?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1573408301185-9519f94bf22b?w=900&q=90","https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=900&q=90","https://images.unsplash.com/photo-1602173574767-37ac01994b2a?w=900&q=90","https://images.unsplash.com/photo-1515562141207-7a88fb7ce338?w=900&q=90","https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=900&q=90"},
            Description="A full eternity band with pavé-set diamonds all the way around. The symbol of endless love." },

        new Product { Id=5,  Name="Gold Bangle Bracelet",      Material="Solid 18K Yellow Gold",              Price=1950, Badge=null,         Category="bracelets", Stars=5, Variant="16cm · Yellow Gold",
            ImageUrl="https://images.unsplash.com/photo-1611085583191-a3b181a88401?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1611085583191-a3b181a88401?w=900&q=90","https://images.unsplash.com/photo-1611591437281-460bfbe1220a?w=900&q=90","https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=900&q=90","https://images.unsplash.com/photo-1573408301185-9519f94bf22b?w=900&q=90","https://images.unsplash.com/photo-1608042314453-ae338d9c6ad1?w=900&q=90"},
            Description="Solid 18K yellow gold bangle with a polished finish. Timeless elegance that pairs beautifully with any look." },

        new Product { Id=6,  Name="Sapphire Cocktail Ring",    Material="18K White Gold & Ceylon Sapphire",   Price=5600, Badge="bestseller", Category="rings",     Stars=5, Variant="Size 7 · White Gold",
            ImageUrl="https://images.unsplash.com/photo-1602173574767-37ac01994b2a?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1602173574767-37ac01994b2a?w=900&q=90","https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=900&q=90","https://images.unsplash.com/photo-1573408301185-9519f94bf22b?w=900&q=90","https://images.unsplash.com/photo-1515562141207-7a88fb7ce338?w=900&q=90","https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=900&q=90"},
            Description="A statement cocktail ring featuring a deep blue Ceylon sapphire surrounded by a halo of brilliant-cut diamonds." },

        new Product { Id=7,  Name="Pearl Drop Earrings",       Material="18K Gold & South Sea Pearls",        Price=890,  OldPrice=1100, Badge="sale", Category="earrings",  Stars=4, Variant="Stud · Yellow Gold",
            ImageUrl="https://images.unsplash.com/photo-1589207212797-cfd578532af8?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1589207212797-cfd578532af8?w=900&q=90","https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?w=900&q=90","https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=900&q=90","https://images.unsplash.com/photo-1611085583191-a3b181a88401?w=900&q=90","https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=900&q=90"},
            Description="South Sea pearl drop earrings in 18K yellow gold. Classic elegance that never goes out of style." },

        new Product { Id=8,  Name="Diamond Tennis Necklace",   Material="18K White Gold & Diamond Chain",     Price=6800, Badge="new",        Category="necklaces", Stars=5, Variant="42cm · White Gold",
            ImageUrl="https://images.unsplash.com/photo-1506630448388-4e683c67ddb0?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1506630448388-4e683c67ddb0?w=900&q=90","https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=900&q=90","https://images.unsplash.com/photo-1608042314453-ae338d9c6ad1?w=900&q=90","https://images.unsplash.com/photo-1611591437281-460bfbe1220a?w=900&q=90","https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=900&q=90"},
            Description="A continuous row of brilliant-cut diamonds set in 18K white gold. The ultimate jewellery statement piece." },

        new Product { Id=9,  Name="Ruby Cluster Ring",         Material="18K Rose Gold & Ruby Diamonds",      Price=3200, OldPrice=3900, Badge="sale", Category="rings",     Stars=5, Variant="Size 6 · Rose Gold",
            ImageUrl="https://images.unsplash.com/photo-1515562141207-7a88fb7ce338?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1515562141207-7a88fb7ce338?w=900&q=90","https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=900&q=90","https://images.unsplash.com/photo-1573408301185-9519f94bf22b?w=900&q=90","https://images.unsplash.com/photo-1602173574767-37ac01994b2a?w=900&q=90","https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=900&q=90"},
            Description="A stunning cluster of rubies and diamonds in 18K rose gold. Bold, beautiful, and utterly unique." },

        new Product { Id=10, Name="Diamond Charm Bracelet",    Material="18K Gold & Diamond Charms",          Price=2750, Badge="new",        Category="bracelets", Stars=5, Variant="17cm · Yellow Gold",
            ImageUrl="https://images.unsplash.com/photo-1611591437281-460bfbe1220a?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1611591437281-460bfbe1220a?w=900&q=90","https://images.unsplash.com/photo-1611085583191-a3b181a88401?w=900&q=90","https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=900&q=90","https://images.unsplash.com/photo-1608042314453-ae338d9c6ad1?w=900&q=90","https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=900&q=90"},
            Description="A delicate charm bracelet in 18K gold with diamond-set charms. Add your own story bead by bead." },

        new Product { Id=11, Name="Emerald Statement Necklace",Material="18K White Gold & Colombian Emerald",  Price=8400, Badge="new",        Category="necklaces", Stars=5, Variant="40cm · White Gold",
            ImageUrl="https://images.unsplash.com/photo-1608042314453-ae338d9c6ad1?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1608042314453-ae338d9c6ad1?w=900&q=90","https://images.unsplash.com/photo-1506630448388-4e683c67ddb0?w=900&q=90","https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=900&q=90","https://images.unsplash.com/photo-1611591437281-460bfbe1220a?w=900&q=90","https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=900&q=90"},
            Description="A rare Colombian emerald set in an 18K white gold halo pendant. An investment piece and a statement of extraordinary taste." },

        new Product { Id=12, Name="Akoya Pearl Stud Earrings", Material="18K Gold & Akoya Pearls",            Price=1240, Badge=null,         Category="earrings",  Stars=4, Variant="Stud · Yellow Gold",
            ImageUrl="https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=600&q=80",
            Images=new(){"https://images.unsplash.com/photo-1617038260897-41a1f14a8ca0?w=900&q=90","https://images.unsplash.com/photo-1589207212797-cfd578532af8?w=900&q=90","https://images.unsplash.com/photo-1535632066927-ab7c9ab60908?w=900&q=90","https://images.unsplash.com/photo-1611085583191-a3b181a88401?w=900&q=90","https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=900&q=90"},
            Description="Classic Akoya pearl studs in 18K gold. Every woman's essential jewellery piece, perfected." },
    };

    private readonly List<Review> _reviews = new()
    {
        new Review { AuthorName="Amara K.",        Date="14 January 2024",   Stars=5, Title="Absolutely breathtaking",           Body="The diamond catches every ray of light and sparkles beautifully. The packaging was stunning and the engraving was perfect.",  Verified=true, HelpfulCount=11 },
        new Review { AuthorName="Sophie R.",       Date="3 December 2023",   Stars=5, Title="Better in person than the photos",  Body="The ring is even more spectacular in person. Diamond quality is exceptional and I get compliments every single day.",          Verified=true, HelpfulCount=8  },
        new Review { AuthorName="James & Eleanor", Date="22 October 2023",   Stars=5, Title="She cried. The ring was perfect.",  Body="I proposed with this ring and my fiancée was in tears. Customer service helped me choose the right size and engraving.",      Verified=true, HelpfulCount=24 },
        new Review { AuthorName="Priya M.",        Date="8 September 2023",  Stars=4, Title="Stunning quality, great service",  Body="The ring itself is gorgeous. I had a minor sizing issue which was resolved with a complimentary resize. Now perfect.",          Verified=true, HelpfulCount=6  },
    };

    public List<Product> GetAll() => _products;

    public List<Product> GetByCategory(string? category) =>
        string.IsNullOrEmpty(category) || category == "all"
            ? _products
            : _products.Where(p => p.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

    public List<Product> GetFeatured(int count = 4) => _products.Take(count).ToList();

    public Product? GetById(int id) => _products.FirstOrDefault(p => p.Id == id);

    public List<Product> GetRelated(int productId, int count = 4)
    {
        var p = GetById(productId);
        if (p == null) return _products.Take(count).ToList();
        return _products
            .Where(x => x.Id != productId && x.Category == p.Category)
            .Concat(_products.Where(x => x.Id != productId && x.Category != p.Category))
            .Take(count).ToList();
    }

    public List<Product> Search(string query, string? category = null, decimal maxPrice = 99999, string sort = "default")
    {
        var results = GetByCategory(category)
            .Where(p => string.IsNullOrEmpty(query) || p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(p => p.Price <= maxPrice)
            .ToList();

        return sort switch
        {
            "price-asc"  => results.OrderBy(p => p.Price).ToList(),
            "price-desc" => results.OrderByDescending(p => p.Price).ToList(),
            "rating"     => results.OrderByDescending(p => p.Stars).ToList(),
            "sale"       => results.Where(p => p.IsOnSale).ToList(),
            _            => results
        };
    }

    public List<Review> GetReviews(int productId) => _reviews;

    public List<string> Categories => new() { "all", "rings", "necklaces", "earrings", "bracelets" };
}
