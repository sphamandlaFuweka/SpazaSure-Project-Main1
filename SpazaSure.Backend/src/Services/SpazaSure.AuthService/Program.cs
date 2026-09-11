using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SpazaSure.AuthService.Services;
using SpazaSure.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SpazaSure Auth Service",
        Version = "v1",
        Description = "Handles supplier and spaza shop authentication"
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

builder.Services.AddHttpClient("AfricasTalking");

builder.Services.AddCors(opt => opt.AddPolicy("Portal", p =>
    p.SetIsOriginAllowed(SpazaSure.Shared.Helpers.CorsHelper.BuildOriginPredicate(builder.Configuration))
     .AllowAnyHeader()
     .AllowAnyMethod()
     .AllowCredentials()));

// PostgreSQL via EF Core
builder.Services.AddDbContext<SpazaSureDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.MigrationsHistoryTable("__EFMigrationsHistory")).UseSnakeCaseNamingConvention());

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
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<ShopAuthService>();
builder.Services.AddScoped<CustomerAuthService>();

var app = builder.Build();

// Verify the deployment-owned schema is available and seed every required role.
// Do not use EnsureCreated in shared QA/production databases because multiple
// services can race and leave a partial schema.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SpazaSureDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (app.Environment.IsDevelopment())
    {
        await db.Database.EnsureCreatedAsync();
    }
    else if (!await db.Database.CanConnectAsync())
    {
        throw new InvalidOperationException("AuthService cannot connect to the configured database.");
    }

    var requiredRoleNames = new[] { "supplier", "spaza_owner", "admin" };
    var existingRoleNames = await db.Roles
        .Where(role => requiredRoleNames.Contains(role.Name) && role.IsActive)
        .Select(role => role.Name)
        .ToListAsync();
    var missingRoleNames = requiredRoleNames.Except(existingRoleNames).ToArray();
    if (missingRoleNames.Length > 0)
    {
        throw new InvalidOperationException(
            $"Auth database is missing required active roles: {string.Join(", ", missingRoleNames)}.");
    }

    logger.LogInformation("Auth database schema and required roles are ready.");
} 

app.UseCors("Portal");
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SpazaSure Auth Service v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();



