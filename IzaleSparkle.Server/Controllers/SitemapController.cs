using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IzaleSparkle.Infrastructure.Persistence;

namespace IzaleSparkle.Server.Controllers;

[ApiController]
[Route("")]
public class SitemapController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly string BaseUrl;

    public SitemapController(AppDbContext db, IConfiguration config)
    {
        _db = db;
        BaseUrl = (config["Site:BaseUrl"] ?? "https://izalesparkle.com").TrimEnd('/');
    }

    [HttpGet("sitemap.xml")]
    [Produces("application/xml")]
    public async Task<IActionResult> Sitemap()
    {
        var urls = new List<SitemapUrl>
        {
            new() { Loc = BaseUrl + "/", Changefreq = "weekly", Priority = "1.0", Lastmod = DateTime.UtcNow.ToString("yyyy-MM-dd") },
            new() { Loc = BaseUrl + "/shop", Changefreq = "daily", Priority = "0.9", Lastmod = DateTime.UtcNow.ToString("yyyy-MM-dd") },
            new() { Loc = BaseUrl + "/contact", Changefreq = "monthly", Priority = "0.6", Lastmod = DateTime.UtcNow.ToString("yyyy-MM-dd") },
            new() { Loc = BaseUrl + "/terms", Changefreq = "monthly", Priority = "0.4", Lastmod = DateTime.UtcNow.ToString("yyyy-MM-dd") },
            new() { Loc = BaseUrl + "/wishlist", Changefreq = "weekly", Priority = "0.5", Lastmod = DateTime.UtcNow.ToString("yyyy-MM-dd") },
            new() { Loc = BaseUrl + "/login", Changefreq = "monthly", Priority = "0.4", Lastmod = DateTime.UtcNow.ToString("yyyy-MM-dd") },
            new() { Loc = BaseUrl + "/register", Changefreq = "monthly", Priority = "0.4", Lastmod = DateTime.UtcNow.ToString("yyyy-MM-dd") },
        };

        try
        {
            var productList = await _db.Products
                .Where(p => p.IsActive && p.StockLevel > 0)
                .Select(p => new { p.Slug, p.UpdatedAt, p.CreatedAt })
                .ToListAsync();

            var products = productList.Select(p => new SitemapUrl
            {
                Loc = $"{BaseUrl}/product/{p.Slug}",
                Changefreq = "weekly",
                Priority = "0.8",
                Lastmod = p.UpdatedAt.HasValue ? p.UpdatedAt.Value.ToString("yyyy-MM-dd") : p.CreatedAt.ToString("yyyy-MM-dd")
            }).ToList();

            urls.AddRange(products);

            var categories = await _db.Categories
                .Where(c => c.IsActive)
                .Select(c => new SitemapUrl
                {
                    Loc = $"{BaseUrl}/shop?cat={c.Slug}",
                    Changefreq = "daily",
                    Priority = "0.7",
                    Lastmod = DateTime.UtcNow.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            urls.AddRange(categories);
        }
        catch
        {
            // Return static URLs if database is unavailable
        }

        var xml = GenerateSitemapXml(urls);
        return Content(xml, "application/xml");
    }

    private static string GenerateSitemapXml(List<SitemapUrl> urls)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");

        foreach (var url in urls)
        {
            sb.AppendLine("  <url>");
            sb.AppendLine($"    <loc>{url.Loc}</loc>");
            sb.AppendLine($"    <lastmod>{url.Lastmod}</lastmod>");
            sb.AppendLine($"    <changefreq>{url.Changefreq}</changefreq>");
            sb.AppendLine($"    <priority>{url.Priority}</priority>");
            sb.AppendLine("  </url>");
        }

        sb.AppendLine("</urlset>");
        return sb.ToString();
    }

    private class SitemapUrl
    {
        public string Loc { get; set; } = "";
        public string Lastmod { get; set; } = "";
        public string Changefreq { get; set; } = "weekly";
        public string Priority { get; set; } = "0.5";
    }
}
