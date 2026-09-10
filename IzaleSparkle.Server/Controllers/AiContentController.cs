using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IzaleSparkle.Application.Ai.Commands;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Server.Controllers;

/// <summary>
/// AI copywriting endpoints — admin only. Backed by the Gemini free tier,
/// so responses are cached and rate-limit failures are surfaced as 400s
/// with a readable message rather than a 500.
/// </summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/ai")]
[Produces("application/json")]
public class AiContentController(IMediator mediator, IAiContentService ai) : ControllerBase
{
    /// <summary>Generate product copy, captions, hashtags and a Reel script from product photos.</summary>
    [HttpPost("product-content")]
    [ProducesResponseType(typeof(ApiResponse<GeneratedProductContent>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> GenerateProductContent(
        [FromBody] GenerateProductContentRequest req, CancellationToken ct)
    {
        if (!ai.IsConfigured)
            return BadRequest(ApiResponse<GeneratedProductContent>.Fail(
                "AI copywriting is not set up yet. Add a free Gemini API key (aistudio.google.com/apikey) " +
                "as Gemini:ApiKey in configuration."));

        try
        {
            var result = await mediator.Send(new GenerateProductContentCommand(req), ct);
            return Ok(ApiResponse<GeneratedProductContent>.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            // Free-tier limits, a bad key, or an unavailable model — all actionable by the admin.
            return BadRequest(ApiResponse<GeneratedProductContent>.Fail(ex.Message));
        }
    }

    /// <summary>Models this API key can actually call — cheapest (flash-lite) first.</summary>
    [HttpGet("models")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<AiModelInfo>>), 200)]
    public async Task<IActionResult> GetModels(CancellationToken ct)
    {
        if (!ai.IsConfigured)
            return BadRequest(ApiResponse<IEnumerable<AiModelInfo>>.Fail(
                "AI copywriting is not set up yet — no Gemini:ApiKey in configuration."));

        try
        {
            var models = await mediator.Send(new ListAiModelsQuery(), ct);
            return Ok(ApiResponse<IEnumerable<AiModelInfo>>.Ok(models));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<IEnumerable<AiModelInfo>>.Fail(ex.Message));
        }
    }

    /// <summary>Whether AI features should be shown in the admin UI at all.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    public IActionResult Status() => Ok(ApiResponse<bool>.Ok(ai.IsConfigured));
}
