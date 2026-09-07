using System.Net;
using System.Text.Json;

namespace SportsPredictor.Api.Middleware;

/// <summary>
/// Translates unhandled exceptions into a structured JSON problem response
/// and ensures every failure is logged with a correlation id.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}", traceId);

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var problem = new
            {
                type = "https://httpstatuses.com/500",
                title = "An unexpected error occurred.",
                status = 500,
                traceId
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
        }
    }
}
