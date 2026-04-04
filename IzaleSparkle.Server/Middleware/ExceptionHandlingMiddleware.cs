using System.Net;
using System.Text.Json;
using IzaleSparkle.Contracts.Responses;
using IzaleSparkle.Domain.Exceptions;

namespace IzaleSparkle.Server.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleExceptionAsync(ctx, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext ctx, Exception ex)
    {
        var (status, message, errors) = ex switch
        {
            NotFoundException   e => (HttpStatusCode.NotFound,             e.Message, (IEnumerable<string>?)null),
            ValidationException e => (HttpStatusCode.UnprocessableEntity,  "Validation failed.", e.Message.Split("; ")),
            BusinessRuleException e=> (HttpStatusCode.BadRequest,          e.Message, null),
            _                    => (HttpStatusCode.InternalServerError,   "An unexpected error occurred.", null),
        };

        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode  = (int)status;

        var body = JsonSerializer.Serialize(
            ApiResponse<object>.Fail(message, errors),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        return ctx.Response.WriteAsync(body);
    }
}
