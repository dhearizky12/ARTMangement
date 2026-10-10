using System.Net;
using System.Net.Http.Json;
using BantuBantu.Api.Diagnostics;
using BantuBantu.Application;
using BantuBantu.Domain;
using BantuBantu.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BantuBantu.Tests;

public class DiagnosticsTests
{
    [Fact]
    public void Settings_AreOffByDefault_AndOptInWhenConfigured()
    {
        var off = DiagnosticsSettings.FromConfiguration(new ConfigurationBuilder().Build());
        Assert.False(off.ServerTiming);
        var on = DiagnosticsSettings.FromConfiguration(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Diagnostics:ServerTiming"] = "true" }).Build());
        Assert.True(on.ServerTiming);
    }

    [Fact]
    public void RequestMetrics_CountStatementsAndAccumulateTime()
    {
        var metrics = new RequestDbMetrics();
        Assert.Equal(0, metrics.SqlCount);
        Assert.Equal(0, metrics.DbMilliseconds);
        metrics.Record(TimeSpan.FromMilliseconds(4).Ticks);
        metrics.Record(TimeSpan.FromMilliseconds(6).Ticks);
        Assert.Equal(2, metrics.SqlCount);
        Assert.Equal(10, metrics.DbMilliseconds);
    }

    [Fact]
    public async Task ServerTiming_IsAbsentWhenDisabled_PresentWhenEnabled()
    {
        var disabled = new DefaultHttpContext();
        disabled.Response.Body = new MemoryStream();
        await new ServerTimingMiddleware(_ => Task.CompletedTask, new DiagnosticsSettings()).InvokeAsync(disabled, new RequestDbMetrics());
        await disabled.Response.WriteAsync("");
        Assert.False(disabled.Response.Headers.ContainsKey("Server-Timing"));

        var enabled = new DefaultHttpContext();
        enabled.Response.Body = new MemoryStream();
        var metrics = new RequestDbMetrics();
        metrics.Record(TimeSpan.FromMilliseconds(7).Ticks);
        await new ServerTimingMiddleware(_ => Task.CompletedTask, new DiagnosticsSettings { ServerTiming = true }).InvokeAsync(enabled, metrics);
        await enabled.Response.WriteAsync("");
        var header = enabled.Response.Headers["Server-Timing"].ToString();
        Assert.Contains("db;dur=7", header);
        Assert.Contains("sql;count=1", header);
        Assert.Contains("total;dur=", header);
    }

    [Fact]
    public async Task DbLatencyEndpoint_IsPlatformAdminOnly()
    {
        using var factory = new DiagnosticsFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            var admin = new AdminAccount { Email = "diag-admin@example.test", FullName = "Diag Admin", Role = UserRole.PlatformAdmin, ProfileCompleted = true };
            admin.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(admin, "Test-password-long-42!");
            db.AdminAccounts.Add(admin);
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/diagnostics/db-latency")).StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/admin/login", new { email = "diag-admin@example.test", password = "Test-password-long-42!" });
        login.EnsureSuccessStatusCode();
        var session = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        var response = await client.GetAsync("/api/admin/diagnostics/db-latency");
        response.EnsureSuccessStatusCode();
        var body = (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>());
        Assert.True(body.GetProperty("connected").GetBoolean());
        Assert.True(body.GetProperty("roundTripMs").GetInt64() >= 0);
        Assert.Contains("db;dur=", response.Headers.GetValues("Server-Timing").Single());
    }
}

public sealed class DiagnosticsFactory : AuthFactory
{
    private static readonly string Connection = Environment.GetEnvironmentVariable("BANTUBANTU_DIAGNOSTICS_TEST_DB")
        ?? Environment.GetEnvironmentVariable("BANTUBANTU_TEST_DB")
        ?? throw new InvalidOperationException("Set BANTUBANTU_DIAGNOSTICS_TEST_DB to a dedicated EMPTY PostgreSQL test database.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default", Connection);
        builder.UseSetting("Diagnostics:ServerTiming", "true");
    }
}
