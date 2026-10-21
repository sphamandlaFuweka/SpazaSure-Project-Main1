using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SpazaSure.Shared.Helpers;

/// <summary>
/// GET /health for each service: 200 when it is running and can reach its database, 503 otherwise.
/// The gateway's /health checks every service (GatewayHealth), for uptime monitoring.
/// </summary>
public static class HealthEndpoint
{
    public static IEndpointConventionBuilder MapHealth(
        this IEndpointRouteBuilder app, string service,
        Func<IServiceProvider, CancellationToken, Task<bool>>? databaseCheck = null) =>
        app.MapGet("/health", async (HttpContext context) =>
        {
            var database = "not used";
            if (databaseCheck is not null)
            {
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
                    timeout.CancelAfter(TimeSpan.FromSeconds(3));
                    database = await databaseCheck(context.RequestServices, timeout.Token) ? "ok" : "unreachable";
                }
                catch (Exception)
                {
                    database = "unreachable";
                }
            }
            var healthy = database != "unreachable";
            return Results.Json(new { service, status = healthy ? "ok" : "unhealthy", database },
                statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        }).AllowAnonymous();
}
