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

// Single Swagger UI at /swagger  shows all services in one dropdown
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.DocumentTitle = "SpazaSure API Dashboard";
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway");
    c.RoutePrefix = "swagger";
    c.DefaultModelsExpandDepth(-1);
    c.DisplayRequestDuration();
});

app.MapReverseProxy();

app.Run();

