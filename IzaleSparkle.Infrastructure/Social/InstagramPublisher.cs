using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Infrastructure.Social;

/// <summary>
/// Publishes photo posts to Instagram through the Meta Graph API.
///
/// Publishing is a two-step flow: create a media container from a publicly
/// fetchable image URL, then publish that container. Containers are processed
/// asynchronously, so we poll the container status_code until it is FINISHED
/// before publishing — publishing too early returns a confusing generic error.
///
/// Free of charge (content publishing has no per-post fee), but it needs an
/// Instagram Business or Creator account linked to a Facebook Page, and
/// Instagram:IgUserId + Instagram:AccessToken in configuration.
/// </summary>
public class InstagramPublisher(
    HttpClient http,
    IConfiguration config,
    ILogger<InstagramPublisher> log)
    : IInstagramPublisher
{
    private string GraphVersion => Blank(config["Instagram:GraphVersion"]) ?? "v21.0";
    private string? IgUserId    => Configured(config["Instagram:IgUserId"]);
    private string? AccessToken => Configured(config["Instagram:AccessToken"]);

    // Instagram limits — worth failing on locally rather than round-tripping.
    public const int MaxCaptionLength = 2200;
    public const int MaxHashtags      = 30;

    public bool IsConfigured => IgUserId is not null && AccessToken is not null;

    public async Task<string?> GetAccountNameAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return null;

        try
        {
            var url = $"https://graph.facebook.com/{GraphVersion}/{IgUserId}" +
                      $"?fields=username&access_token={Uri.EscapeDataString(AccessToken!)}";

            using var resp = await http.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                log.LogWarning("Instagram account lookup failed ({Status}): {Body}", resp.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("username", out var u) ? u.GetString() : null;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Instagram account lookup threw.");
            return null;
        }
    }

    public async Task<InstagramPublishResult> PublishPhotoAsync(
        string publicImageUrl, string caption, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Instagram posting is not configured. Set Instagram:IgUserId and Instagram:AccessToken " +
                "(Meta app → Instagram Graph API) before posting.");

        if (string.IsNullOrWhiteSpace(publicImageUrl))
            throw new InvalidOperationException("No image URL to post.");

        if (!Uri.TryCreate(publicImageUrl, UriKind.Absolute, out var imageUri)
            || (imageUri.Scheme != Uri.UriSchemeHttp && imageUri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException(
                $"'{publicImageUrl}' is not an absolute http(s) URL. Instagram fetches the image itself, " +
                "so it must be publicly reachable.");

        if (caption.Length > MaxCaptionLength)
            throw new InvalidOperationException(
                $"Caption is {caption.Length} characters — Instagram allows {MaxCaptionLength}. " +
                "Trim the caption or drop some hashtags.");

        var hashtagCount = caption.Count(c => c == '#');
        if (hashtagCount > MaxHashtags)
            throw new InvalidOperationException(
                $"Caption has {hashtagCount} hashtags — Instagram allows {MaxHashtags} per post.");

        // ── 1. CREATE CONTAINER ──────────────────────────────────
        var containerId = await PostForIdAsync(
            $"{IgUserId}/media",
            new Dictionary<string, string>
            {
                ["image_url"] = publicImageUrl,
                ["caption"]   = caption,
            },
            "create the media container", ct);

        // ── 2. WAIT FOR IT TO FINISH PROCESSING ──────────────────
        await WaitForContainerAsync(containerId, ct);

        // ── 3. PUBLISH ───────────────────────────────────────────
        var mediaId = await PostForIdAsync(
            $"{IgUserId}/media_publish",
            new Dictionary<string, string> { ["creation_id"] = containerId },
            "publish the post", ct);

        log.LogInformation("Published Instagram post {MediaId} from {ImageUrl}.", mediaId, publicImageUrl);

        return new InstagramPublishResult(mediaId, await GetPermalinkAsync(mediaId, ct));
    }

    // ── GRAPH HELPERS ────────────────────────────────────────────
    private async Task<string> PostForIdAsync(
        string path, Dictionary<string, string> fields, string what, CancellationToken ct)
    {
        fields["access_token"] = AccessToken!;

        using var content = new FormUrlEncodedContent(fields);
        using var resp = await http.PostAsync(
            $"https://graph.facebook.com/{GraphVersion}/{path}", content, ct);

        var body = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
        {
            log.LogError("Instagram: failed to {What} ({Status}): {Body}", what, resp.StatusCode, body);
            throw new InvalidOperationException(
                $"Instagram could not {what} ({(int)resp.StatusCode}). {ExtractError(body)}".Trim());
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("id", out var id) || id.GetString() is not { } value)
            throw new InvalidOperationException($"Instagram did not return an id when asked to {what}.");

        return value;
    }

    /// <summary>
    /// Polls the container until Instagram has finished ingesting the image.
    /// Photos are usually ready on the first or second check; give up after ~30s.
    /// </summary>
    private async Task WaitForContainerAsync(string containerId, CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(3);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var url = $"https://graph.facebook.com/{GraphVersion}/{containerId}" +
                      $"?fields=status_code,status&access_token={Uri.EscapeDataString(AccessToken!)}";

            using var resp = await http.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                // A status read failing is not fatal on its own — let the publish call decide.
                log.LogWarning("Instagram container status read failed ({Status}): {Body}", resp.StatusCode, body);
                return;
            }

            using var doc = JsonDocument.Parse(body);
            var root   = doc.RootElement;
            var status = root.TryGetProperty("status_code", out var sc) ? sc.GetString() : null;

            if (status == "FINISHED")
                return;

            if (status is "ERROR" or "EXPIRED")
            {
                var detail = root.TryGetProperty("status", out var s) ? s.GetString() : null;
                log.LogError("Instagram container {Id} ended as {Status}: {Detail}", containerId, status, detail);
                throw new InvalidOperationException(
                    $"Instagram rejected the image ({status}). {detail} ".Trim() +
                    " Use a JPEG under 8 MB with an aspect ratio between 4:5 and 1.91:1.");
            }

            await Task.Delay(delay, ct);
        }

        throw new InvalidOperationException(
            "Instagram is still processing the image after 30 seconds. It may still publish — " +
            "check the account before trying again, so you do not post twice.");
    }

    private async Task<string?> GetPermalinkAsync(string mediaId, CancellationToken ct)
    {
        try
        {
            var url = $"https://graph.facebook.com/{GraphVersion}/{mediaId}" +
                      $"?fields=permalink&access_token={Uri.EscapeDataString(AccessToken!)}";

            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("permalink", out var p) ? p.GetString() : null;
        }
        catch (Exception ex)
        {
            // The post is already live at this point — a missing link is cosmetic.
            log.LogWarning(ex, "Could not read permalink for Instagram post {MediaId}.", mediaId);
            return null;
        }
    }

    private static string ExtractError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                var msg  = err.TryGetProperty("error_user_msg", out var u) ? u.GetString() : null;
                msg    ??= err.TryGetProperty("message",        out var m) ? m.GetString() : null;
                return msg ?? string.Empty;
            }
        }
        catch (JsonException) { }

        return string.Empty;
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    // Placeholder values shipped in appsettings.json count as "not configured".
    private static string? Configured(string? v)
        => Blank(v) is { } s && !s.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) ? s : null;
}
