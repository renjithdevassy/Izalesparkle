using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Application.Social.Commands;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Server.Controllers;

/// <summary>
/// Social publishing — admin only. Posts the copy produced by the AI copywriter
/// straight to Instagram, so captions and hashtags do not have to be
/// copy-pasted by hand. Meta charges nothing for content publishing.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/social")]
[Produces("application/json")]
public class SocialController(
    IMediator mediator,
    IInstagramPublisher instagram,
    IConfiguration config,
    ILogger<SocialController> logger) : ControllerBase
{
    private string BaseUrl => (config["Site:BaseUrl"] ?? "https://izalesparkle.com").TrimEnd('/');

    /// <summary>Whether the admin UI should offer Instagram posting, and which account it posts to.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<SocialStatus>), 200)]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var configured = instagram.IsConfigured;
        var account    = configured ? await instagram.GetAccountNameAsync(ct) : null;
        return Ok(ApiResponse<SocialStatus>.Ok(new SocialStatus(configured, account)));
    }

    /// <summary>Publish one photo post to Instagram.</summary>
    [HttpPost("instagram")]
    [ProducesResponseType(typeof(ApiResponse<InstagramPublishResult>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> PublishToInstagram(
        [FromBody] PublishInstagramRequest req, CancellationToken ct)
    {
        if (!instagram.IsConfigured)
            return BadRequest(ApiResponse<InstagramPublishResult>.Fail(
                "Instagram posting is not set up yet. Add Instagram:IgUserId and Instagram:AccessToken " +
                "to configuration (see AI-COPYWRITER.md)."));

        var absoluteUrl = ToAbsolute(req.ImageUrl);

        // Meta fetches the image over the public internet, so a dev machine cannot
        // publish. Say so plainly instead of surfacing a vague Graph API error.
        if (IsLocal(absoluteUrl))
            return BadRequest(ApiResponse<InstagramPublishResult>.Fail(
                $"The photo would be served from {absoluteUrl}, which Instagram cannot reach. " +
                "Posting works from the deployed site, not from localhost."));

        try
        {
            var result = await mediator.Send(new PublishInstagramCommand(absoluteUrl, req.Caption), ct);
            return Ok(ApiResponse<InstagramPublishResult>.Ok(result, "Posted to Instagram."));
        }
        catch (InvalidOperationException ex)
        {
            // Expired token, rejected image, rate limit — all actionable by the admin.
            logger.LogWarning(ex, "Instagram publish failed.");
            return BadRequest(ApiResponse<InstagramPublishResult>.Fail(ex.Message));
        }
    }

    // "/uploads/abc.jpg" → "https://izalesparkle.com/uploads/abc.jpg"; absolute URLs pass through.
    private string ToAbsolute(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out _) ? url : $"{BaseUrl}/{url.TrimStart('/')}";

    private static bool IsLocal(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && (uri.IsLoopback || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase));
}
