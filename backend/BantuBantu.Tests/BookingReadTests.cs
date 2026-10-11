using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BantuBantu.Application;
using BantuBantu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace BantuBantu.Tests;

[Collection("diagnostics-db")]
public class BookingReadTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static async Task<HttpClient> CustomerClientAsync(DiagnosticsFactory factory)
    {
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            await ProviderBrowseContractTests.SeedPublishedProvider(db);
        }
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);
        var google = await client.PostAsJsonAsync("/api/auth/google", new { credential = "verified-test-identity" });
        google.EnsureSuccessStatusCode();
        var session = (await google.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        return client;
    }

    private static DateOnly NextBookableDay()
    {
        var day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        return day.DayOfWeek == DayOfWeek.Sunday ? day.AddDays(1) : day;
    }

    [Fact]
    public async Task Booking_SucceedsAndReadsNarrow()
    {
        using var factory = new DiagnosticsFactory();
        using var client = await CustomerClientAsync(factory);
        var body = new BookingRequest(ProviderBrowseContractTests.ProviderId, NextBookableDay(), "3374011003", "Jalan Uji nomor 9");
        var response = await client.PostAsJsonAsync("/api/orders", body, Json);
        response.EnsureSuccessStatusCode();
        var order = (await response.Content.ReadFromJsonAsync<OrderDto>(Json))!;
        Assert.Equal("Penyedia Uji Browse", order.ProviderName);
        Assert.InRange(ProviderBrowseContractTests.SqlCount(response), 1, 4);
    }

    [Fact]
    public async Task Booking_InvalidDateFailsFast()
    {
        using var factory = new DiagnosticsFactory();
        using var client = await CustomerClientAsync(factory);
        var body = new BookingRequest(ProviderBrowseContractTests.ProviderId, DateOnly.FromDateTime(DateTime.UtcNow), "3374011003", "Jalan Uji nomor 9");
        var response = await client.PostAsJsonAsync("/api/orders", body, Json);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.InRange(ProviderBrowseContractTests.SqlCount(response), 1, 3);
    }
}
