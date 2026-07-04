using MediatR;
using Microsoft.AspNetCore.Mvc;
using IzaleSparkle.Application.Auth.Commands;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Server.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IMediator mediator) : ControllerBase
{
    /// <summary>Register a new customer account.</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new RegisterCommand(req), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Login with email and password. Returns JWT token.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new LoginCommand(req), ct);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Login for TV app: accepts JSON as raw text/plain to avoid CORS preflight on old Tizen browsers.</summary>
    [HttpPost("tv-login")]
    [Consumes("text/plain", "application/json")]
    [ProducesResponseType(typeof(AuthResponse), 200)]
    public async Task<IActionResult> TvLogin(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var req = System.Text.Json.JsonSerializer.Deserialize<LoginRequest>(body,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (req is null) return BadRequest();
        var result = await mediator.Send(new LoginCommand(req), ct);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Request a password reset link by email. Always returns 200 (never reveals account existence).</summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(AuthMessageResponse), 200)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ForgotPasswordCommand(req), ct);
        return Ok(result);
    }

    /// <summary>Set a new password using a valid reset token.</summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(AuthMessageResponse), 200)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req, CancellationToken ct)
    {
        var result = await mediator.Send(new ResetPasswordCommand(req), ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
