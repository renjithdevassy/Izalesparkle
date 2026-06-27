using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IzaleSparkle.Application.Common.Interfaces;

namespace IzaleSparkle.Infrastructure.Notifications;

/// <summary>
/// Reads products from the Meta (WhatsApp / Facebook) Commerce catalog via the Graph API.
/// Catalog reads are free — there is no per-request charge (only outbound messaging is billed).
/// Requires WhatsApp:CatalogId and WhatsApp:AccessToken in configuration.
/// </summary>
public class MetaWhatsAppCatalogService(
    HttpClient http,
    IConfiguration config,
    ILogger<MetaWhatsAppCatalogService> log)
    : IWhatsAppCatalogService
{
    private const string GraphVersion = "v21.0";

    public async Task<IReadOnlyList<WhatsAppCatalogItem>> FetchProductsAsync(CancellationToken ct = default)
    {
        var catalogId   = config["WhatsApp:CatalogId"];
        var accessToken = config["WhatsApp:AccessToken"];

        if (NotConfigured(catalogId) || NotConfigured(accessToken))
            throw new InvalidOperationException(
                "WhatsApp catalog is not configured. Set WhatsApp:CatalogId and WhatsApp:AccessToken " +
                "(find these in Meta Commerce Manager) before syncing.");

        const string fields = "retailer_id,name,description,price,currency,image_url,availability";
        var url = $"https://graph.facebook.com/{GraphVersion}/{catalogId}/products" +
                  $"?fields={fields}&limit=100&access_token={Uri.EscapeDataString(accessToken!)}";

        var items = new List<WhatsAppCatalogItem>();
        var pageGuard = 0;

        while (!string.IsNullOrEmpty(url) && pageGuard++ < 50)
        {
            using var resp = await http.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                log.LogError("Meta catalog fetch failed ({Status}): {Body}", resp.StatusCode, body);
                throw new InvalidOperationException(
                    $"Meta catalog request failed ({(int)resp.StatusCode}). " +
                    $"Check the catalog ID and access token. {ExtractError(body)}".Trim());
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                foreach (var el in data.EnumerateArray())
                    items.Add(MapItem(el));

            // Follow cursor-based pagination until there is no "next" page.
            url = root.TryGetProperty("paging", out var paging)
                  && paging.TryGetProperty("next", out var next)
                  && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }

        log.LogInformation("Fetched {Count} products from Meta catalog {CatalogId}.", items.Count, catalogId);
        return items;
    }

    private static bool NotConfigured(string? value)
        => string.IsNullOrWhiteSpace(value) || value.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);

    private static WhatsAppCatalogItem MapItem(JsonElement el)
    {
        string Str(string name) =>
            el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? string.Empty
                : string.Empty;

        var availability = Str("availability");
        return new WhatsAppCatalogItem(
            RetailerId:  Str("retailer_id"),
            Name:        Str("name"),
            Description: Str("description"),
            Price:       ParsePrice(Str("price")),
            Currency:    Str("currency"),
            ImageUrl:    Str("image_url"),
            Available:   string.IsNullOrEmpty(availability)
                         || availability.Contains("in stock", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Meta returns price as a formatted display string (e.g. "£10.00", "10.00 GBP").
    /// Extract the numeric portion. Verify prices after the first sync in case the
    /// catalog uses an unexpected format.
    /// </summary>
    private static decimal ParsePrice(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0m;
        var match = Regex.Match(raw.Replace(",", string.Empty), @"\d+(\.\d+)?");
        return match.Success
            && decimal.TryParse(match.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)
            ? d : 0m;
    }

    private static string ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err)
                && err.TryGetProperty("message", out var msg)
                && msg.ValueKind == JsonValueKind.String)
                return msg.GetString() ?? string.Empty;
        }
        catch (JsonException) { }
        return string.Empty;
    }
}
