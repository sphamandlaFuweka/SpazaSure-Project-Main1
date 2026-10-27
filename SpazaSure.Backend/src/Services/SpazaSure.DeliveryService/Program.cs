using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Helpers;
using SpazaSure.Shared.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Drivers' licence, PrDP and ID documents: local disk in development, the S3 bucket in production
if (string.Equals(builder.Configuration["Storage:Provider"], "s3", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.Configure<S3StorageOptions>(builder.Configuration.GetSection("Storage:S3"));
    builder.Services.AddSingleton<IFileStorageService, S3FileStorageService>();
}
else
{
    builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
}
builder.Services.AddEndpointsApiExplorer();

// RabbitMQ Event Publisher: shops hear when a driver is allocated to their order
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return new EventPublisher(
        host: config["RabbitMQ:Host"] ?? "localhost",
        username: config["RabbitMQ:Username"] ?? "spazasure",
        password: config["RabbitMQ:Password"] ?? "guest"
    );
});
// Admins get a notification when a request crashes
builder.Services.AddSingleton<SpazaSure.Shared.Helpers.IUnhandledErrorAlerts, SpazaSure.Infrastructure.Data.AdminErrorAlerts>();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SpazaSure Delivery Service",
        Version = "v1",
        Description = "Drivers, allocating them to orders, and live delivery tracking"
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

builder.Services.AddDbContext<SpazaSureDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.MigrationsHistoryTable("__EFMigrationsHistory")).UseSnakeCaseNamingConvention());

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

builder.Services.AddCors(opt =>
    opt.AddPolicy("SupplierPortal", p =>
        p.SetIsOriginAllowed(SpazaSure.Shared.Helpers.CorsHelper.BuildOriginPredicate(builder.Configuration))
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()));

// Dashboard staff roles: which admin areas each staff member may view or manage.
builder.Services.AddScoped<SpazaSure.Shared.Helpers.IAdminAccessStore, SpazaSure.Infrastructure.Data.AdminAccessStore>();

var app = builder.Build();
SpazaSure.Shared.Helpers.PrivateFiles.Configure(builder.Configuration["Jwt:Secret"]);
// Driver documents hold ID numbers, so they need a signed link (POPIA s19).
SpazaSure.Shared.Helpers.PrivateFiles.UsePrivateFiles(app);

// Local-storage files get their own "/delivery-uploads" prefix: "/uploads" is routed to UserService
// and "/compliance-uploads" to ComplianceService.
if (!string.Equals(builder.Configuration["Storage:Provider"], "s3", StringComparison.OrdinalIgnoreCase))
{
    var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
        RequestPath = "/delivery-uploads",
        ServeUnknownFileTypes = false,
    });
}

app.UseSpazaSureExceptionHandling();
app.UseCors("SupplierPortal");
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SpazaSure Delivery Service v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseAuthorization();
SpazaSure.Shared.Helpers.AdminAccessExtensions.UseAdminAccess(app);
app.MapControllers();
SpazaSure.Shared.Helpers.HealthEndpoint.MapHealth(app, "delivery", (services, ct) =>
    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
        .GetRequiredService<SpazaSure.Infrastructure.Data.SpazaSureDbContext>(services).Database.CanConnectAsync(ct));

app.Run();
