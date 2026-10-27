using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SpazaSure.NotificationService.Consumers;
using SpazaSure.NotificationService.Data;
using SpazaSure.NotificationService.Email;
using SpazaSure.Shared.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// The main schema too: the email sequences read users and log what they send
builder.Services.AddDbContext<SpazaSure.Infrastructure.Data.SpazaSureDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.MigrationsHistoryTable("__EFMigrationsHistory")).UseSnakeCaseNamingConvention());

// JWT Authentication
// Must match the auth service's signing secret, or every token is rejected with 401.
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret is not configured for the notification service.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.FromMinutes(5),
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorization();

// Phone push notifications (Firebase Cloud Messaging)
builder.Services.AddSingleton<SpazaSure.NotificationService.Services.PushSender>();

// Automated emails: waitlist confirmations, welcome, feature tips and reminders
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.Configure<EmailSequenceOptions>(builder.Configuration.GetSection("EmailSequences"));
builder.Services.AddSingleton(new EmailLinks(jwtSecret,
    builder.Configuration["PublicApiUrl"] ?? "https://api.spazasure.co.za"));
builder.Services.AddHostedService<EmailSequenceWorker>();
builder.Services.AddScoped<IAdminAccessStore, SpazaSure.Infrastructure.Data.AdminAccessStore>();

// RabbitMQ Consumer (Background Service)
builder.Services.AddHostedService<RabbitMqConsumer>();

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SpazaSure Notification Service",
        Version = "v1",
        Description = "Notifications from RabbitMQ events: stored for the inbox and pushed to phones"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });
});

// CORS  allow supplier portal
builder.Services.AddCors(opt =>
    opt.AddPolicy("AllowPortal", p =>
        p.SetIsOriginAllowed(SpazaSure.Shared.Helpers.CorsHelper.BuildOriginPredicate(builder.Configuration))
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials()));

var app = builder.Build();

app.UseSpazaSureExceptionHandling();
app.UseCors("AllowPortal");
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.UseAdminAccess();

app.MapControllers();
SpazaSure.Shared.Helpers.HealthEndpoint.MapHealth(app, "notifications", (services, ct) =>
    Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
        .GetRequiredService<NotificationDbContext>(services).Database.CanConnectAsync(ct));

app.Run();
