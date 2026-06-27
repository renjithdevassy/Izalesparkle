using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IzaleSparkle.Infrastructure.Persistence;

namespace IzaleSparkle.Server.Controllers;

/// <summary>
/// Public product feed in Meta's catalog CSV format.
/// Add the URL of this endpoint as a scheduled Data Feed in Commerce Manager
/// (Catalog → Data Sources → Data Feeds) and Meta pulls products automatically.
/// No access token or developer account required — Meta fetches the URL itself,
/// so it must be reachable on the public internet.
/// </summary>
[ApiController]
[Route("")]
public class CatalogFeedController(AppDbContext db, IConfiguration config) : ControllerBase
{
    // Public site URL Meta uses for product links/images — set via Site:BaseUrl in config.
    private string BaseUrl => (config["Site:BaseUrl"] ?? "https://izalesparkle.com").TrimEnd('/');
    private const string Brand = "Izale Sparkle";

    // Meta required columns + sale_price. Order matches the values written below.
    private static readonly string[] Header =
    {
        "id", "title", "description", "availability", "condition",
        "price", "sale_price", "link", "image_link", "brand"
    };

    [HttpGet("feed/products.csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> ProductsCsv(CancellationToken ct)
    {
        var products = await db.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Id)
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Header));

        foreach (var p in products)
        {
            var inStock     = p.StockLevel > 0;
            var onSale      = p.OldPrice != null && p.OldPrice.Amount > p.Price.Amount;

            // Meta: `price` is the regular/RRP, `sale_price` is the discounted price.
            var regular     = onSale ? p.OldPrice!.Amount : p.Price.Amount;
            var salePrice   = onSale ? p.Price.Amount : (decimal?)null;

            var imageLink   = AbsoluteUrl(p.ImageUrl);

            var row = new[]
            {
                p.Id.ToString(CultureInfo.InvariantCulture),
                p.Name,
                string.IsNullOrWhiteSpace(p.Description) ? p.Name : p.Description,
                inStock ? "in stock" : "out of stock",
                "new",
                FormatPrice(regular, p.Price.Currency),
                salePrice.HasValue ? FormatPrice(salePrice.Value, p.Price.Currency) : "",
                $"{BaseUrl}/product/{p.Id}",
                imageLink,
                Brand
            };

            sb.AppendLine(string.Join(",", row.Select(Csv)));
        }

        // Return as a downloadable file so Meta (and a browser) treat it as a feed.
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "products.csv");
    }

    // Meta price format: "<amount> <ISO currency>", e.g. "12.00 GBP".
    private static string FormatPrice(decimal amount, string currency)
        => $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {currency}";

    // image_link / link must be absolute. Relative upload paths get the site prefix.
    private string AbsoluteUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;
        return $"{BaseUrl}/{url.TrimStart('/')}";
    }

    // CSV escaping: wrap in quotes and double any embedded quotes.
    private static string Csv(string field)
    {
        field ??= "";
        if (field.Contains('"') || field.Contains(',') || field.Contains('\n') || field.Contains('\r'))
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        return field;
    }
}
