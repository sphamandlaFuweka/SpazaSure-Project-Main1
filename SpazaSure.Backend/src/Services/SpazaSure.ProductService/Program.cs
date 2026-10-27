using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Helpers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
// RabbitMQ Event Publisher for notifications
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new EventPublisher(
        host: config["RabbitMQ:Host"] ?? "localhost",
        username: config["RabbitMQ:Username"] ?? "spazasure",
        password: config["RabbitMQ:Password"] ?? "guest");
});
// Admins get a notification when a request crashes
builder.Services.AddSingleton<SpazaSure.Shared.Helpers.IUnhandledErrorAlerts, SpazaSure.Infrastructure.Data.AdminErrorAlerts>();

builder.Services.AddHttpClient<SpazaSure.ProductService.Services.OpenFoodFactsService>(client =>
{
    client.BaseAddress = new Uri("https://world.openfoodfacts.org/api/v2/");
    client.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SpazaSure Product Service",
        Version = "v1",
        Description = "Handles product catalog management for suppliers"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token. Example: Bearer eyJhbGci..."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(opt => opt.AddPolicy("Portal", p =>
    p.SetIsOriginAllowed(SpazaSure.Shared.Helpers.CorsHelper.BuildOriginPredicate(builder.Configuration))
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// PostgreSQL via EF Core
builder.Services.AddDbContext<SpazaSureDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        o => o.MigrationsHistoryTable("__EFMigrationsHistory"))
    .UseSnakeCaseNamingConvention());

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Dashboard staff roles: which admin areas each staff member may view or manage.
builder.Services.AddScoped<SpazaSure.Shared.Helpers.IAdminAccessStore, SpazaSure.Infrastructure.Data.AdminAccessStore>();

var app = builder.Build();

app.UseSpazaSureExceptionHandling();
app.UseCors("Portal");
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SpazaSure Product Service v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();
SpazaSure.Shared.Helpers.AdminAccessExtensions.UseAdminAccess(app);
app.MapControllers();
SpazaSure.Shared.Helpers.HealthEndpoint.MapHealth(app, "products", (services, ct) =>
    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
        .GetRequiredService<SpazaSure.Infrastructure.Data.SpazaSureDbContext>(services).Database.CanConnectAsync(ct));

app.Run();
