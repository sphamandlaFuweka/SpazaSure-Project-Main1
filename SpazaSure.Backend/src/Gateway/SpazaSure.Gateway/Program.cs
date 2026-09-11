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

