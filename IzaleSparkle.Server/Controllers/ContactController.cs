using Microsoft.AspNetCore.Mvc;
using IzaleSparkle.Application.Common.Interfaces;
using IzaleSparkle.Contracts.Requests;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ContactController(IEmailService emailService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ContactResponse>), 200)]
    public async Task<IActionResult> Submit([FromBody] ContactRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(ApiResponse<ContactResponse>.Fail("Email and message are required."));

        _ = emailService.SendContactEmailAsync(
            req.Email, $"{req.FirstName} {req.LastName}".Trim(),
            req.EnquiryType, req.Message, CancellationToken.None);

        return Ok(ApiResponse<ContactResponse>.Ok(
            new ContactResponse(true, "Thank you! We'll be in touch within one business day.")));
    }
}

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class NewsletterController(IEmailService emailService) : ControllerBase
{
    [HttpPost("subscribe")]
    [ProducesResponseType(typeof(ApiResponse<NewsletterResponse>), 200)]
    public async Task<IActionResult> Subscribe([FromBody] NewsletterSubscribeRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(ApiResponse<NewsletterResponse>.Fail("A valid email is required."));

        _ = emailService.SendNewsletterWelcomeAsync(req.Email, CancellationToken.None);

        return Ok(ApiResponse<NewsletterResponse>.Ok(
            new NewsletterResponse(true, "✦ Welcome to the Sparkle Circle!")));
    }
}
