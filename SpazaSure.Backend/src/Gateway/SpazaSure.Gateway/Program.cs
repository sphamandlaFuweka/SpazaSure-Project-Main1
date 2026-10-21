using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using SpazaSure.Shared.Helpers;

var builder = WebApplication.CreateBuilder(args);

// This is the ONE CORS policy that actually matters for browser traffic —
// every service behind this Gateway only has a Docker-internal address in
// QA/Prod (see docker-compose.qa.yml / docker-compose.prod.yml), so the
// browser only ever talks to the Gateway directly. Previously this was
// AllowAnyOrigin() with no restriction at all; now it reads an explicit
// allow-list from Cors:AllowedOrigins in appsettings/environment config.
builder.Services.AddCors(opt => opt.AddPolicy("Portal", p =>
    p.SetIsOriginAllowed(CorsHelper.BuildOriginPredicate(builder.Configuration))
     .AllowAnyHeader()
     .AllowAnyMethod()));

// The gateway is published on 127.0.0.1 only (see docker-compose.prod.yml), so the
// host's nginx is the only thing that can reach it. Trust the client IP nginx appends
// to X-Forwarded-For (ForwardLimit = 1 takes just that last hop, so it can't be spoofed).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

// Rate limiting per client IP. Sign-in endpoints get strict limits against password
// guessing; password reset is stricter still against email bombing and token guessing.
static bool IsAuthPath(PathString p, params string[] suffixes) =>
    (p.StartsWithSegments("/api/supplier/auth") || p.StartsWithSegments("/api/shop/auth") || p.StartsWithSegments("/api/customer/auth"))
    && suffixes.Any(sfx => p.Value!.EndsWith(sfx, StringComparison.OrdinalIgnoreCase));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = ctx.Request.Path;

        if (IsAuthPath(path, "/forgot-password", "/reset-password"))
            return RateLimitPartition.GetFixedWindowLimiter("reset:" + ip, _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 });

        if (IsAuthPath(path, "/login", "/register", "/verify-otp", "/send-otp"))
            return RateLimitPartition.GetFixedWindowLimiter("auth:" + ip, _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 });

        return RateLimitPartition.GetTokenBucketLimiter("all:" + ip, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 300, TokensPerPeriod = 300, ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 0, AutoReplenishment = true,
        });
    });
    o.OnRejected = async (ctx, ct) =>
    {
        var wait = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retry) ? (int)Math.Ceiling(retry.TotalSeconds) : 60;
        ctx.HttpContext.Response.Headers.RetryAfter = wait.ToString();
        ctx.HttpContext.Response.ContentType = "application/json";
        var waitText = wait >= 120 ? $"{(int)Math.Ceiling(wait / 60.0)} minutes" : $"{wait} seconds";
        await ctx.HttpContext.Response.WriteAsJsonAsync(
            new { success = false, message = $"Too many attempts. Please wait {waitText} and try again." }, ct);
    };
});

builder.Services.AddHttpClient("maintenance", c => c.Timeout = TimeSpan.FromSeconds(3));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Unified Swagger aggregator
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SpazaSure API Gateway",
        Version = "v1",
        Description = "Unified API dashboard for all SpazaSure microservices"
    });
});

var app = builder.Build();

// Must run first so rate limits and logs see the real client IP
app.UseForwardedHeaders();

// When a downstream service is unreachable, times out, or a route doesn't
// match any endpoint, YARP/Kestrel return a bare status code with an EMPTY
// body — the client then has nothing to parse and surfaces a confusing
// "server returned an empty response" error. This guarantees every such
// response still comes back as JSON the mobile/web clients can read a real
// message from (mirrors SpazaSure.Shared's ExceptionHandlingMiddleware,
// which only covers unhandled exceptions *inside* each downstream service).
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (response.ContentLength is > 0) return;

    response.ContentType = "application/json";
    var message = response.StatusCode switch
    {
        502 or 503 => "The requested service is temporarily unavailable. Please try again shortly.",
        504 => "The request timed out. Please try again.",
        404 => "The requested resource was not found.",
        _ => "Something went wrong processing your request. Please try again."
    };
    await response.WriteAsync($"{{\"success\":false,\"message\":\"{message}\"}}");
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("Portal");
app.UseRateLimiter();
// Security headers on every API response, including proxied files. Documents may only be framed
// by SpazaSure's own sites (the admin console previews PDFs in a frame).
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Content-Security-Policy"] =
            "frame-ancestors 'self' https://portal.spazasure.co.za https://admin.spazasure.co.za https://app.spazasure.co.za";
        headers.Remove("X-Powered-By");
        return Task.CompletedTask;
    });
    await next();
});

// Admin-controlled maintenance mode (see MaintenanceModeMiddleware for what stays open)
app.UseMiddleware<SpazaSure.Gateway.MaintenanceModeMiddleware>();

// Single Swagger UI at /swagger  shows all services in one dropdown.
// Disabled in Production so the public API surface isn't browsable; enable
// explicitly with Swagger__Enabled=true if ever needed.
var swaggerEnabled = !app.Environment.IsProduction()
    || (bool.TryParse(builder.Configuration["Swagger:Enabled"], out var forceSwagger) && forceSwagger);
if (swaggerEnabled)
{
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.DocumentTitle = "SpazaSure API Dashboard";
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway");
    c.RoutePrefix = "swagger";
    c.DefaultModelsExpandDepth(-1);
    c.DisplayRequestDuration();
});
}

// GET /health: checks every service behind the gateway, for uptime monitoring
// (e.g. https://api.spazasure.co.za/health). 200 when all are healthy, 503 otherwise.
var healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
app.MapGet("/health", async (CancellationToken ct) =>
{
    var services = builder.Configuration.GetSection("ReverseProxy:Clusters").GetChildren()
        .SelectMany(cluster => cluster.GetSection("Destinations").GetChildren()
            .Select(d => (Name: cluster.Key.Replace("-cluster", ""), Address: d["Address"])))
        .Where(s => !string.IsNullOrEmpty(s.Address))
        .DistinctBy(s => s.Address)
        .ToList();
    var results = await Task.WhenAll(services.Select(async s =>
    {
        try
        {
            using var response = await healthClient.GetAsync($"{s.Address!.TrimEnd('/')}/health", ct);
            return (s.Name, Healthy: response.IsSuccessStatusCode);
        }
        catch (Exception)
        {
            return (s.Name, Healthy: false);
        }
    }));
    var healthy = results.All(r => r.Healthy);
    return Results.Json(
        new { status = healthy ? "ok" : "unhealthy", services = results.ToDictionary(r => r.Name, r => r.Healthy ? "ok" : "down") },
        statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

app.MapReverseProxy();

app.Run();

