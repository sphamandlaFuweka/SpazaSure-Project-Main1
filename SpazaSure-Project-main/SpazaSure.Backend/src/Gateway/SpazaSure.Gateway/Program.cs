using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(opt => opt.AddPolicy("Portal", p =>
    p.AllowAnyOrigin()
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

