using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Storage;
using SpazaSure.UserService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// File storage: "local" (default) writes to disk. Set Storage:Provider = "s3"
// in production (see appsettings.Production.json / .env.prod) to use any
// S3-compatible bucket (AWS S3, Cloudflare R2, DO Spaces, MinIO) instead.
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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SpazaSure User Service", Version = "v1", Description = "Supplier and shop user profiles, documents and file uploads" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "Bearer", BearerFormat = "JWT", In = ParameterLocation.Header, Description = "Enter your JWT token." });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement {{ new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }});
});

builder.Services.AddDbContext<SpazaSureDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.MigrationsHistoryTable("__EFMigrationsHistory")).UseSnakeCaseNamingConvention());
builder.Services.AddHttpClient(nameof(OnboardingPayFastService), client =>
    client.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddScoped<OnboardingPayFastService>();
builder.Services.AddSingleton<StripePaymentService>();

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

var app = builder.Build();

app.UseCors("SupplierPortal");
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SpazaSure User Service v1");
    c.RoutePrefix = "swagger";
});

// Resolve uploads directory - check multiple candidate paths so it works
// whether launched from VS (bin/Debug), dotnet run (project dir), or published.
var candidatePaths = new[]
{
    Path.Combine(builder.Environment.ContentRootPath, "uploads"),
    Path.Combine(AppContext.BaseDirectory, "uploads"),
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "uploads"),
};

// Use the first path that already contains files, otherwise fall back to ContentRootPath/uploads
var uploadsPath = candidatePaths.FirstOrDefault(p =>
    Directory.Exists(p) && Directory.GetFileSystemEntries(p).Length > 0)
    ?? candidatePaths[0];

Directory.CreateDirectory(uploadsPath);
Console.WriteLine($"[UserService] Serving uploads from: {uploadsPath}");

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".pdf"] = "application/pdf";
contentTypeProvider.Mappings[".jpg"] = "image/jpeg";
contentTypeProvider.Mappings[".jpeg"] = "image/jpeg";
contentTypeProvider.Mappings[".png"] = "image/png";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(uploadsPath)),
    RequestPath = "/uploads",
    ContentTypeProvider = contentTypeProvider,
    ServeUnknownFileTypes = false,
    OnPrepareResponse = ctx =>
    {
        // Allow browser to display inline (PDF viewer, images)
        ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        ctx.Context.Response.Headers["Content-Disposition"] = "inline";
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

