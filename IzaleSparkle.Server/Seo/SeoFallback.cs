using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using IzaleSparkle.Infrastructure.Persistence;

namespace IzaleSparkle.Server.Seo;

/// <summary>
/// The storefront is a WebAssembly SPA served from one static index.html, so every
/// route used to emit the same title, description and a canonical pointing at the
/// homepage — which asks Google to deindex all 98 URLs. This rewrites the head per
/// request so each URL is self-canonical and describes itself, and adds Product
/// JSON-LD plus a no-script summary for the crawl pass that runs before JavaScript.
/// </summary>
public static class SeoFallback
{
    private record PageMeta(
        string Title,
        string Description,
        string Path,
        string? ImageUrl = null,
        string OgType = "website",
        string? JsonLd = null,
        string? Heading = null,
        string? Body = null);

    public static void MapSeoFallback(this WebApplication app)
    {
        app.MapFallback(async context =>
        {
            var env    = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var cache  = context.RequestServices.GetRequiredService<IMemoryCache>();
            var config = context.RequestServices.GetRequiredService<IConfiguration>();

            var file = env.WebRootFileProvider.GetFileInfo("index.html");
            if (!file.Exists)
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            var template = await cache.GetOrCreateAsync($"seo:index:{file.LastModified.Ticks}", async _ =>
            {
                await using var stream = file.CreateReadStream();
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return await reader.ReadToEndAsync();
            }) ?? string.Empty;

            var baseUrl = (config["Site:BaseUrl"] ?? "https://izalesparkle.com").TrimEnd('/');
            var db      = context.RequestServices.GetRequiredService<AppDbContext>();
            var meta    = await ResolveAsync(context, db, context.RequestAborted);

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(Apply(template, meta, baseUrl), context.RequestAborted);
        });
    }

    // ── PER-ROUTE METADATA ───────────────────────────────────────
    private static async Task<PageMeta> ResolveAsync(HttpContext ctx, AppDbContext db, CancellationToken ct)
    {
        var path = ctx.Request.Path.Value?.TrimEnd('/') ?? "";
        if (path.Length == 0) path = "/";

        var productMatch = Regex.Match(path, @"^/product/(\d+)$", RegexOptions.IgnoreCase);
        if (productMatch.Success && int.TryParse(productMatch.Groups[1].Value, out var id))
        {
            var p = await db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (p is not null) return ForProduct(p, path);
        }

        if (path.Equals("/shop", StringComparison.OrdinalIgnoreCase))
        {
            var slug = ctx.Request.Query["cat"].ToString();
            if (!string.IsNullOrWhiteSpace(slug))
            {
                var category = await db.Categories.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Slug == slug, ct);
                var name = category?.Name ?? Humanise(slug);
                return new PageMeta(
                    $"{name} — Anti-Tarnish Indian Jewellery UK | Izale Sparkle",
                    $"Shop {name.ToLowerInvariant()} at Izale Sparkle — anti-tarnish Indian jewellery for Tamil, Malayali and South Asian families in the UK. Free UK delivery.",
                    $"/shop?cat={slug}",
                    Heading: name,
                    Body: $"Anti-tarnish {name.ToLowerInvariant()}, shipped across the UK.");
            }

            return new PageMeta(
                "Shop All Indian Jewellery UK — Anti-Tarnish Sets | Izale Sparkle",
                "Browse the full Izale Sparkle collection — anti-tarnish necklaces, jhumkas, bangles, bridal and temple jewellery sets, delivered free across the UK.",
                "/shop",
                Heading: "Shop all jewellery",
                Body: "Anti-tarnish Indian jewellery for weddings, festivals and everyday wear.");
        }

        return path.ToLowerInvariant() switch
        {
            "/contact" => new PageMeta(
                "Contact Izale Sparkle — Indian Jewellery, Haywards Heath UK",
                "Get in touch with Izale Sparkle for orders, sizing and bridal enquiries. Indian jewellery specialists based in Haywards Heath, West Sussex.",
                "/contact", Heading: "Contact us"),

            "/terms" => new PageMeta(
                "Terms & Conditions | Izale Sparkle",
                "Terms and conditions for orders placed with Izale Sparkle, including delivery, returns and payment terms.",
                "/terms", Heading: "Terms and conditions"),

            "/wishlist" => new PageMeta(
                "Your Wishlist | Izale Sparkle",
                "Jewellery you have saved for later at Izale Sparkle.",
                "/wishlist", Heading: "Your wishlist"),

            _ => new PageMeta(
                "Izale Sparkle — Anti-Tarnish Indian Jewellery UK | Kerala, Tamil & Malayali",
                "Anti-tarnish Indian jewellery in the UK — necklaces, jhumkas, bangles, bridal and temple sets for Tamil, Malayali and South Asian families. Free UK delivery.",
                "/",
                Heading: "Anti-tarnish Indian jewellery, made for UK weather",
                Body: "Kerala-inspired necklaces, jhumkas, bangles and bridal sets for Tamil, Malayali and South Asian families across the UK.")
        };
    }

    private static PageMeta ForProduct(Domain.Entities.Product p, string path)
    {
        var name     = p.Name.Trim();
        var category = Humanise(p.Category);
        var title    = Truncate($"{name} — Anti-Tarnish {category} | Izale Sparkle UK", 65);

        var description = Truncate(
            string.IsNullOrWhiteSpace(p.Description)
                ? $"{name} — anti-tarnish {category.ToLowerInvariant()} from Izale Sparkle. £{p.Price.Amount:N2} with free UK delivery."
                : Collapse(p.Description),
            155);

        var jsonLd = BuildProductJsonLd(p, path);

        return new PageMeta(title, description, path, p.ImageUrl, "product", jsonLd,
            Heading: name,
            Body: $"{description} Price £{p.Price.Amount:N2}. " +
                  (p.StockLevel > 0 ? "In stock." : "Currently out of stock."));
    }

    private static string BuildProductJsonLd(Domain.Entities.Product p, string path) => $$"""
        {
          "@context": "https://schema.org",
          "@type": "Product",
          "name": {{JsonString(p.Name)}},
          "description": {{JsonString(Collapse(p.Description))}},
          "sku": {{JsonString(p.Id.ToString())}},
          "category": {{JsonString(Humanise(p.Category))}},
          "material": {{JsonString(p.Material)}},
          "brand": { "@type": "Brand", "name": "Izale Sparkle" },
          "offers": {
            "@type": "Offer",
            "price": "{{p.Price.Amount:0.00}}",
            "priceCurrency": {{JsonString(p.Price.Currency)}},
            "availability": "https://schema.org/{{(p.StockLevel > 0 ? "InStock" : "OutOfStock")}}",
            "url": "__CANONICAL__"
          }
        }
        """;

    // ── HEAD REWRITING ───────────────────────────────────────────
    private static string Apply(string html, PageMeta meta, string baseUrl)
    {
        var canonical = baseUrl + (meta.Path == "/" ? "/" : meta.Path);
        var image = string.IsNullOrWhiteSpace(meta.ImageUrl)
            ? $"{baseUrl}/icons/og-image.png"
            : (meta.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? meta.ImageUrl
                : $"{baseUrl}/{meta.ImageUrl.TrimStart('/')}");

        var title = Escape(meta.Title);
        var desc  = Escape(meta.Description);

        html = ReplaceFirst(html, @"<title>.*?</title>", $"<title>{title}</title>");
        html = ReplaceMeta(html, "name", "description", desc);
        html = ReplaceFirst(html, @"<link\s+rel=""canonical""[^>]*/?>",
            $@"<link rel=""canonical"" href=""{Escape(canonical)}"" />");

        html = ReplaceMeta(html, "property", "og:url", Escape(canonical));
        html = ReplaceMeta(html, "property", "og:title", title);
        html = ReplaceMeta(html, "property", "og:description", desc);
        html = ReplaceMeta(html, "property", "og:image", Escape(image));
        html = ReplaceMeta(html, "property", "og:type", meta.OgType);
        html = ReplaceMeta(html, "name", "twitter:title", title);
        html = ReplaceMeta(html, "name", "twitter:description", desc);
        html = ReplaceMeta(html, "name", "twitter:image", Escape(image));

        if (meta.JsonLd is { Length: > 0 } jsonLd)
            html = html.Replace("</head>",
                $"<script type=\"application/ld+json\">{jsonLd.Replace("__CANONICAL__", canonical)}</script>\n</head>");

        // The crawl pass that runs before JavaScript otherwise sees only an error notice.
        if (meta.Heading is { Length: > 0 } heading)
        {
            var noscript = new StringBuilder()
                .Append("<noscript><div class=\"seo-fallback\">")
                .Append($"<h1>{Escape(heading)}</h1>")
                .Append($"<p>{Escape(meta.Body ?? meta.Description)}</p>")
                .Append("<nav><a href=\"/\">Home</a> <a href=\"/shop\">Shop</a> ")
                .Append("<a href=\"/shop?cat=necklace\">Necklaces</a> <a href=\"/shop?cat=earrings\">Earrings</a> ")
                .Append("<a href=\"/shop?cat=bangles\">Bangles</a> <a href=\"/contact\">Contact</a></nav>")
                .Append("</div></noscript>")
                .ToString();

            html = ReplaceFirst(html, @"<body[^>]*>", m => m + noscript);
        }

        return html;
    }

    private static string ReplaceMeta(string html, string attr, string key, string value) =>
        ReplaceFirst(html, $@"<meta\s+{attr}=""{Regex.Escape(key)}""\s+content=""[^""]*""\s*/?>",
            $@"<meta {attr}=""{key}"" content=""{value}"" />");

    private static string ReplaceFirst(string input, string pattern, string replacement) =>
        Regex.Replace(input, pattern, replacement.Replace("$", "$$"),
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static string ReplaceFirst(string input, string pattern, Func<string, string> build) =>
        Regex.Replace(input, pattern, m => build(m.Value),
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

    // ── HELPERS ──────────────────────────────────────────────────
    private static string Escape(string value) => WebUtility.HtmlEncode(value ?? "");

    private static string JsonString(string? value) =>
        System.Text.Json.JsonSerializer.Serialize(value ?? "");

    private static string Collapse(string value) =>
        Regex.Replace(value ?? "", @"\s+", " ").Trim();

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..Math.Max(0, max - 1)].TrimEnd() + "…";

    private static string Humanise(string slug) =>
        string.IsNullOrWhiteSpace(slug)
            ? "Jewellery"
            : string.Join(' ', slug.Replace('-', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
