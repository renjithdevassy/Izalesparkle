using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IzaleSparkle.Application.Admin.Commands;
using IzaleSparkle.Contracts.Responses;

namespace IzaleSparkle.Server.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await mediator.Send(new GetCategoriesAdminQuery(), ct);
        return Ok(ApiResponse<IEnumerable<CategoryResponse>>.Ok(result));
    }
}
