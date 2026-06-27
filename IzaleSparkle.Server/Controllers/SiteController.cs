using Microsoft.AspNetCore.Mvc;
using IzaleSparkle.Application.Common.Interfaces;

namespace IzaleSparkle.Server.Controllers;

/// <summary>Public, anonymous endpoints for the storefront (e.g. visit tracking).</summary>
[ApiController]
[Route("api/site")]
public class SiteController(ISiteAnalytics analytics) : ControllerBase
{
    /// <summary>Record a website visit. Called once per app load from the client.</summary>
    [HttpPost("track")]
    public async Task<IActionResult> Track([FromQuery] string? path, CancellationToken ct)
    {
        await analytics.RecordVisitAsync(path, ct);
        return Ok();
    }
}
