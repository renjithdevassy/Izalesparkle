namespace IzaleSparkle.Client.Models;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? OldPrice { get; set; }
    public string? Badge { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Stars { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public string Variant { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsVatApplicable { get; set; } = false;
    public int StockLevel { get; set; }

    public bool IsOnSale => OldPrice.HasValue && OldPrice > Price;
    public int? SavePercent => IsOnSale
        ? Math.Max(1, (int)Math.Round((1 - Price / OldPrice!.Value) * 100))
        : null;
    public string StarsDisplay => new string('★', Stars) + new string('☆', 5 - Stars);
}

public class CartItem
{
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; } = 1;
    public string? SelectedSize { get; set; }
    public string? SelectedMetal { get; set; }

    public decimal LineTotal => Product.Price * Quantity;
}

public class Review
{
    public string AuthorName { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public int Stars { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool Verified { get; set; }
    public int HelpfulCount { get; set; }
}
