using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SpazaSure.Shared.Models;

namespace SpazaSure.Shared.Helpers;

/// <summary>
/// Without this, an unhandled exception anywhere in a request pipeline
/// results in a bare 500 response with an EMPTY body (ASP.NET Core's
/// default behaviour when no exception handler is registered) — which the
/// mobile/web clients then surface confusingly as "server returned an
/// empty response". This middleware guarantees every unhandled exception
/// still comes back as a proper ApiResponse JSON payload the client can
/// parse and show a real error message for.
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var body = ApiResponse.Fail("Something went wrong processing your request. Please try again.");
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }));
        }
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseSpazaSureExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
