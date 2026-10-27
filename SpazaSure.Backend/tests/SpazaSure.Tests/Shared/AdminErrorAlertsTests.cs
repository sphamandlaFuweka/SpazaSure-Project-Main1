using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SpazaSure.Infrastructure.Data;
using SpazaSure.Shared.Helpers;
using Xunit;

namespace SpazaSure.Tests.Shared;

public class AdminErrorAlertsTests
{
    private static AdminErrorAlerts Create(Func<DateTime> now) =>
        new(new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new EventPublisher("__nonexistent__", "x", "x"), NullLogger<AdminErrorAlerts>.Instance) { Now = now };

    [Fact]
    public void ShouldSend_AlertsOnce_ThenStaysQuietUntilTheQuietPeriodPasses()
    {
        var now = new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc);
        var alerts = Create(() => now);

        alerts.ShouldSend("NullReferenceException|GET /api/shop/orders/{id}").Should().BeTrue();
        alerts.ShouldSend("NullReferenceException|GET /api/shop/orders/{id}").Should().BeFalse();

        now += AdminErrorAlerts.QuietPeriod - TimeSpan.FromSeconds(1);
        alerts.ShouldSend("NullReferenceException|GET /api/shop/orders/{id}").Should().BeFalse();

        now += TimeSpan.FromSeconds(1);
        alerts.ShouldSend("NullReferenceException|GET /api/shop/orders/{id}").Should().BeTrue();
    }

    [Fact]
    public void ShouldSend_TreatsDifferentErrorsSeparately()
    {
        var alerts = Create(() => DateTime.UtcNow);

        alerts.ShouldSend("A|GET /x").Should().BeTrue();
        alerts.ShouldSend("B|GET /x").Should().BeTrue();
        alerts.ShouldSend("A|POST /x").Should().BeTrue();
    }

    [Fact]
    public async Task Middleware_ReportsTheCrash_AndStillReturnsAJsonError()
    {
        var reported = new List<(string Method, string Path, Exception Error)>();
        var services = new ServiceCollection()
            .AddSingleton<IUnhandledErrorAlerts>(new RecordingAlerts(reported))
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = "POST";
        context.Request.Path = "/api/shop/orders";
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new InvalidOperationException("boom"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
        reported.Should().ContainSingle();
        reported[0].Method.Should().Be("POST");
        reported[0].Path.Should().Be("/api/shop/orders");
        reported[0].Error.Message.Should().Be("boom");
    }

    [Fact]
    public async Task Middleware_StillAnswers_WhenTheAlertItselfFails()
    {
        var services = new ServiceCollection()
            .AddSingleton<IUnhandledErrorAlerts>(new ThrowingAlerts())
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw new InvalidOperationException("boom"),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(500);
        context.Response.ContentType.Should().Be("application/json");
    }

    private sealed class RecordingAlerts(List<(string, string, Exception)> into) : IUnhandledErrorAlerts
    {
        public void Report(string method, string path, Exception exception) => into.Add((method, path, exception));
    }

    private sealed class ThrowingAlerts : IUnhandledErrorAlerts
    {
        public void Report(string method, string path, Exception exception) => throw new Exception("alerting down");
    }
}
