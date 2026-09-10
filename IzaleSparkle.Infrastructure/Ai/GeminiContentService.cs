using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Infrastructure.Ai;

/// <summary>
/// Product copywriter backed by the Google Gemini API.
/// Runs on the free tier: no card, no spend — the trade-off is per-minute and
/// per-day request caps, so this class caches results and backs off on 429.
///
/// Configuration (appsettings / user secrets / environment):
///   Gemini:ApiKey      — free key from https://aistudio.google.com/apikey  (required)
///   Gemini:Model       — e.g. gemini-flash-latest (optional; see /api/admin/ai/models)
///   Gemini:BrandVoice  — override the built-in Izale Sparkle brief (optional)
///   Gemini:CacheHours  — how long identical requests are reused (default 12)
///
/// NOTE: on the free tier Google may use prompts and responses to improve their
/// products. Product photos and marketing copy only — never send customer data
/// through this service.
/// </summary>
public class GeminiContentService(
    HttpClient http,
    IConfiguration config,
    IMemoryCache cache,
    ILogger<GeminiContentService> log)
    : IAiContentService
{
    private const string BaseUrl      = "https://generativelanguage.googleapis.com/v1beta";
    private const string DefaultModel = "gemini-flash-latest";
    private const int    MaxAttempts  = 3;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string? ApiKey => config["Gemini:ApiKey"] is { } k
                              && !string.IsNullOrWhiteSpace(k)
                              && !k.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase)
                              ? k : null;

    private string Model => string.IsNullOrWhiteSpace(config["Gemini:Model"])
        ? DefaultModel : config["Gemini:Model"]!;

    public bool IsConfigured => ApiKey is not null;

    // ── PUBLIC API ───────────────────────────────────────────────
    public async Task<GeneratedProductContent> GenerateProductContentAsync(
        GenerateProductContentRequest req, CancellationToken ct = default)
    {
        var key = ApiKey ?? throw new InvalidOperationException(
            "Gemini is not configured. Add a free API key from aistudio.google.com/apikey " +
            "as Gemini:ApiKey (use dotnet user-secrets locally, never commit it).");

        var model  = string.IsNullOrWhiteSpace(req.Model) ? Model : req.Model!;
        var images = await ResolveImagesAsync(req, ct);

        if (images.Count == 0)
            throw new InvalidOperationException(
                "No usable photo was supplied — the copy is written from what the model can see.");

        var cacheKey = BuildCacheKey(model, req, images);
        if (cache.TryGetValue<GeneratedProductContent>(cacheKey, out var hit) && hit is not null)
        {
            log.LogInformation("Gemini cache hit for {Name}.", req.Name ?? "(unnamed piece)");
            return hit with { FromCache = true };
        }

        var prompt = BuildPrompt(req);
        var parts  = new List<object> { new { text = prompt } };
        parts.AddRange(images.Select(i => (object)new
        {
            inline_data = new { mime_type = i.MimeType, data = i.Base64Data }
        }));

        var body = new
        {
            contents = new[] { new { role = "user", parts = parts.ToArray() } },
            generationConfig = new
            {
                temperature        = req.Temperature ?? 0.9,
                responseMimeType   = "application/json",
                responseSchema     = ResponseSchema
            }
        };

        var (json, tokens) = await PostWithRetryAsync(
            $"{BaseUrl}/models/{Uri.EscapeDataString(model)}:generateContent", key, body, ct);

        var parsed = JsonSerializer.Deserialize<GeminiCopy>(json)
                     ?? throw new InvalidOperationException("The model returned no usable content.");

        var result = new GeneratedProductContent(
            Observed:           parsed.Observed           ?? "",
            ProductTitle:       parsed.ProductTitle       ?? "",
            SeoMetaDescription: parsed.SeoMetaDescription ?? "",
            ShortDescription:   parsed.ShortDescription   ?? "",
            LongDescription:    parsed.LongDescription    ?? "",
            BulletPoints:       parsed.BulletPoints       ?? [],
            Material:           parsed.Material           ?? req.Material ?? "",
            CareInstructions:   parsed.CareInstructions   ?? "",
            AltText:            parsed.AltText            ?? "",
            InstagramCaptions:  parsed.InstagramCaptions?.Select(c => new AiCaption(c.Style ?? "", c.Text ?? "")).ToList() ?? [],
            Hashtags:           parsed.Hashtags           ?? [],
            ReelHook:           parsed.ReelHook           ?? "",
            ReelScript:         parsed.ReelScript         ?? "",
            WhatsAppBroadcast:  parsed.WhatsAppBroadcast  ?? "",
            StoryPollIdea:      parsed.StoryPollIdea      ?? "",
            ModelUsed:          model,
            TokensUsed:         tokens,
            FromCache:          false);

        var hours = int.TryParse(config["Gemini:CacheHours"], out var h) ? h : 12;
        cache.Set(cacheKey, result, TimeSpan.FromHours(hours <= 0 ? 12 : hours));

        log.LogInformation("Gemini wrote copy for {Name} using {Model} ({Tokens} tokens).",
            req.Name ?? "(unnamed piece)", model, tokens);

        return result;
    }

    public async Task<IReadOnlyList<AiModelInfo>> ListModelsAsync(CancellationToken ct = default)
    {
        var key = ApiKey ?? throw new InvalidOperationException("Gemini is not configured (Gemini:ApiKey).");

        using var msg = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models?pageSize=200");
        msg.Headers.Add("x-goog-api-key", key);

        using var resp = await http.SendAsync(msg, ct);
        var payload = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"Could not list Gemini models ({(int)resp.StatusCode}). {Describe(payload)}");

        using var doc = JsonDocument.Parse(payload);
        var models = new List<AiModelInfo>();

        if (doc.RootElement.TryGetProperty("models", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var m in arr.EnumerateArray())
            {
                var name = m.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name is null) continue;

                var supported = m.TryGetProperty("supportedGenerationMethods", out var sm) && sm.ValueKind == JsonValueKind.Array
                    && sm.EnumerateArray().Any(x => x.GetString() == "generateContent");
                if (!supported) continue;

                var id = name.StartsWith("models/") ? name[7..] : name;
                if (id.Contains("embedding") || id.Contains("imagen") || id.Contains("veo")
                    || id.Contains("tts") || id.Contains("live") || id.Contains("aqa")) continue;

                var display = m.TryGetProperty("displayName", out var d) ? d.GetString() ?? id : id;
                models.Add(new AiModelInfo(id, display));
            }
        }

        // Cheapest-first: flash-lite, then flash, then everything else.
        static int Rank(string id) =>
            id.Contains("flash-lite") ? 0 : id.Contains("flash") ? 1 : id.Contains("pro") ? 2 : 3;

        return models.OrderBy(m => Rank(m.Id)).ThenByDescending(m => m.Id).ToList();
    }

    // ── HTTP WITH BACKOFF ────────────────────────────────────────
    private async Task<(string Json, int Tokens)> PostWithRetryAsync(
        string url, string key, object body, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(body, JsonOpts);

        for (var attempt = 1; ; attempt++)
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            msg.Headers.Add("x-goog-api-key", key);

            using var resp = await http.SendAsync(msg, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);

            if (resp.IsSuccessStatusCode)
                return ExtractText(raw);

            var retryable = resp.StatusCode is HttpStatusCode.TooManyRequests
                                             or HttpStatusCode.ServiceUnavailable
                                             or HttpStatusCode.InternalServerError;

            if (!retryable || attempt >= MaxAttempts)
            {
                log.LogError("Gemini request failed ({Status}) after {Attempts} attempt(s): {Body}",
                    resp.StatusCode, attempt, raw);

                throw new InvalidOperationException(resp.StatusCode switch
                {
                    HttpStatusCode.TooManyRequests =>
                        "Gemini's free-tier limit is exhausted for now. Wait a minute and try again, " +
                        "or switch Gemini:Model to a flash-lite model, which has a larger free allowance. "
                        + Describe(raw),
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                        "Gemini rejected the API key. Check Gemini:ApiKey. " + Describe(raw),
                    HttpStatusCode.NotFound =>
                        $"Model '{Model}' is not available to this key. Call /api/admin/ai/models to see what is. " + Describe(raw),
                    _ => $"Gemini request failed ({(int)resp.StatusCode}). {Describe(raw)}"
                });
            }

            var wait = resp.Headers.RetryAfter?.Delta
                       ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));
            log.LogWarning("Gemini returned {Status}; retrying in {Seconds}s (attempt {Attempt}/{Max}).",
                resp.StatusCode, wait.TotalSeconds, attempt, MaxAttempts);

            await Task.Delay(wait, ct);
        }
    }

    private static (string Json, int Tokens) ExtractText(string raw)
    {
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;

        var text = new StringBuilder();
        if (root.TryGetProperty("candidates", out var cands) && cands.ValueKind == JsonValueKind.Array)
            foreach (var c in cands.EnumerateArray())
                if (c.TryGetProperty("content", out var content)
                    && content.TryGetProperty("parts", out var parts)
                    && parts.ValueKind == JsonValueKind.Array)
                    foreach (var p in parts.EnumerateArray())
                        if (p.TryGetProperty("text", out var t))
                            text.Append(t.GetString());

        if (text.Length == 0)
        {
            var reason = root.TryGetProperty("candidates", out var c2)
                         && c2.ValueKind == JsonValueKind.Array && c2.GetArrayLength() > 0
                         && c2[0].TryGetProperty("finishReason", out var fr)
                ? fr.GetString() : null;

            throw new InvalidOperationException(
                $"Gemini returned no text{(reason is null ? "" : $" (finish reason: {reason})")}. Try again, or lower the temperature.");
        }

        var tokens = root.TryGetProperty("usageMetadata", out var um)
                     && um.TryGetProperty("totalTokenCount", out var tc) ? tc.GetInt32() : 0;

        return (text.ToString(), tokens);
    }

    private static string Describe(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("error", out var e)
                   && e.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
        }
        catch { return body.Length > 300 ? body[..300] : body; }
    }

    // ── IMAGES ───────────────────────────────────────────────────
    private async Task<List<AiImageInput>> ResolveImagesAsync(
        GenerateProductContentRequest req, CancellationToken ct)
    {
        var images = req.Images?.Where(i => !string.IsNullOrWhiteSpace(i.Base64Data)).Take(4).ToList() ?? [];
        if (images.Count > 0 || string.IsNullOrWhiteSpace(req.ImageUrl)) return images;

        // Relative uploads path (/uploads/foo.jpg) → resolve against the site base URL.
        var url = req.ImageUrl!;
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var baseUrl = config["Site:BaseUrl"]?.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("Site:BaseUrl must be set to resolve a relative image URL.");
            url = $"{baseUrl}/{url.TrimStart('/')}";
        }

        try
        {
            using var resp = await http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
            var mime  = resp.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            images.Add(new AiImageInput(mime, Convert.ToBase64String(bytes)));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not read the product image at {url}: {ex.Message}", ex);
        }

        return images;
    }

    private static string BuildCacheKey(string model, GenerateProductContentRequest r, List<AiImageInput> images)
    {
        var sb = new StringBuilder()
            .Append(model).Append('|').Append(r.Name).Append('|').Append(r.Category).Append('|')
            .Append(r.Price).Append('|').Append(r.Material).Append('|').Append(r.Occasion).Append('|')
            .Append(r.Notes).Append('|').Append(r.Temperature);

        foreach (var i in images)
            sb.Append('|').Append(i.Base64Data.Length).Append(':')
              .Append(i.Base64Data.Length > 64 ? i.Base64Data[..64] : i.Base64Data);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        return "ai:product-content:" + Convert.ToHexString(hash);
    }

    // ── PROMPT ───────────────────────────────────────────────────
    private const string DefaultBrandVoice = """
        Izale Sparkle is a small UK-based Indian jewellery label.
        Signature: anti-tarnish pieces and Kerala-inspired designs (temple motifs, palakka, jhimki, kasu, mullamottu, coin and paisley work).
        Customers: South Asian diaspora women in the UK — weddings, Onam and festival season, everyday wear that survives British weather.
        Voice: warm, specific, quietly premium. Concrete details over hype. No "elevate your look", no "unleash", no emoji spam, no fake scarcity.
        Spelling: British English (jewellery, colour). Prices in GBP.
        Always be honest about materials — plated brass is plated brass, never "gold".
        """;

    private string BuildPrompt(GenerateProductContentRequest r)
    {
        var voice = string.IsNullOrWhiteSpace(config["Gemini:BrandVoice"])
            ? DefaultBrandVoice : config["Gemini:BrandVoice"]!;

        return $"""
            You are the in-house copywriter for Izale Sparkle.

            BRAND BRIEF
            {voice}

            THE PIECE
            - Type/category: {Or(r.Category, "(not stated — infer from the photo)")}
            - Name/SKU: {Or(r.Name, "(none given — propose one that fits the brand)")}
            - Price: {(r.Price.HasValue ? $"GBP {r.Price.Value:0.00}" : "(not given — never invent a price)")}
            - Materials/finish: {Or(r.Material, "(not stated — describe only what the photo supports)")}
            - Occasion/collection: {Or(r.Occasion, "(not stated)")}
            - Notes: {Or(r.Notes, "(none)")}

            TASK
            Study the photo(s) and write launch-ready content. Rules:
            - Describe only what you can see or what the brief states. Never invent gemstones, carats, hallmarks, weight, dimensions, certifications or a price.
            - observed: one honest sentence on what the photo shows (design, finish, drop/length if visible).
            - product_title: max 60 characters, plain and searchable.
            - seo_meta_description: max 155 characters.
            - short_description: 1-2 sentences for a listing card.
            - long_description: 90-140 words, no headings, easy to read aloud.
            - bullet_points: 4-6 short spec/benefit bullets.
            - material: a short material/composition line suitable for the product page.
            - care_instructions: 2 sentences, realistic for plated/anti-tarnish jewellery.
            - alt_text: one factual sentence for screen readers, under 125 characters.
            - instagram_captions: exactly 3, styles "Elegant", "Story", "Punchy". Each under 400 characters, at most 2 emoji, ending with a light call to action. No hashtags inside captions.
            - hashtags: exactly 24, lowercase, no "#" prefix — a mix of broad, niche and community/local tags. Nothing spammy or banned.
            - reel_hook: the first 3 seconds of spoken/on-screen text.
            - reel_script: a 15-second Reel, 3-4 beats, each beat as "0-3s: visual - on-screen text".
            - whatsapp_broadcast: under 300 characters for the customer broadcast list.
            - story_poll_idea: one two-option Instagram Story poll about this piece.

            Return JSON only, matching the schema.
            """;

        static string Or(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value!;
    }

    // ── STRUCTURED OUTPUT SCHEMA ─────────────────────────────────
    private static Dictionary<string, object> Str => new() { ["type"] = "string" };
    private static Dictionary<string, object> StrArray => new()
    {
        ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = "string" }
    };

    private static readonly Dictionary<string, object> ResponseSchema = new()
    {
        ["type"] = "object",
        ["properties"] = new Dictionary<string, object>
        {
            ["observed"]             = Str,
            ["product_title"]        = Str,
            ["seo_meta_description"] = Str,
            ["short_description"]    = Str,
            ["long_description"]     = Str,
            ["bullet_points"]        = StrArray,
            ["material"]             = Str,
            ["care_instructions"]    = Str,
            ["alt_text"]             = Str,
            ["instagram_captions"]   = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["items"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object> { ["style"] = Str, ["text"] = Str },
                    ["required"] = new[] { "style", "text" }
                }
            },
            ["hashtags"]           = StrArray,
            ["reel_hook"]          = Str,
            ["reel_script"]        = Str,
            ["whatsapp_broadcast"] = Str,
            ["story_poll_idea"]    = Str
        },
        ["required"] = new[]
        {
            "observed", "product_title", "seo_meta_description", "short_description",
            "long_description", "bullet_points", "material", "care_instructions", "alt_text",
            "instagram_captions", "hashtags", "reel_hook", "reel_script",
            "whatsapp_broadcast", "story_poll_idea"
        }
    };

}

// ── RAW MODEL OUTPUT ─────────────────────────────────────────
internal sealed record GeminiCopy
{
    [JsonPropertyName("observed")]             public string? Observed { get; init; }
    [JsonPropertyName("product_title")]        public string? ProductTitle { get; init; }
    [JsonPropertyName("seo_meta_description")] public string? SeoMetaDescription { get; init; }
    [JsonPropertyName("short_description")]    public string? ShortDescription { get; init; }
    [JsonPropertyName("long_description")]     public string? LongDescription { get; init; }
    [JsonPropertyName("bullet_points")]        public List<string>? BulletPoints { get; init; }
    [JsonPropertyName("material")]             public string? Material { get; init; }
    [JsonPropertyName("care_instructions")]    public string? CareInstructions { get; init; }
    [JsonPropertyName("alt_text")]             public string? AltText { get; init; }
    [JsonPropertyName("instagram_captions")]   public List<RawCaption>? InstagramCaptions { get; init; }
    [JsonPropertyName("hashtags")]             public List<string>? Hashtags { get; init; }
    [JsonPropertyName("reel_hook")]            public string? ReelHook { get; init; }
    [JsonPropertyName("reel_script")]          public string? ReelScript { get; init; }
    [JsonPropertyName("whatsapp_broadcast")]   public string? WhatsAppBroadcast { get; init; }
    [JsonPropertyName("story_poll_idea")]      public string? StoryPollIdea { get; init; }
}

internal sealed record RawCaption
{
    [JsonPropertyName("style")] public string? Style { get; init; }
    [JsonPropertyName("text")]  public string? Text { get; init; }
}
