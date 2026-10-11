using System.Net;
using System.Net.Http.Headers;
using BantuBantu.Application;
using BantuBantu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace BantuBantu.Tests;

[Collection("diagnostics-db")]
public class PublicCacheTests
{
    private static async Task<DiagnosticsFactory> ReadyAsync()
    {
        var factory = new DiagnosticsFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await ProviderBrowseContractTests.SeedPublishedProvider(db);
        return factory;
    }

    [Fact]
    public async Task PublicCatalogRead_SendsSharedCacheHeadersAndETag()
    {
        using var factory = await ReadyAsync();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/service-categories");
        response.EnsureSuccessStatusCode();

        var cacheControl = response.Headers.CacheControl;
        Assert.NotNull(cacheControl);
        Assert.True(cacheControl!.Public);
        Assert.Equal(TimeSpan.FromSeconds(60), cacheControl.MaxAge);
        Assert.Contains("stale-while-revalidate=300", string.Join(",", response.Headers.GetValues("Cache-Control")));
        Assert.Contains("Origin", response.Headers.Vary);
        var etag = response.Headers.ETag;
        Assert.NotNull(etag);

        var revalidated = new HttpRequestMessage(HttpMethod.Get, "/api/service-categories");
        revalidated.Headers.TryAddWithoutValidation("If-None-Match", etag!.ToString());
        var second = await client.SendAsync(revalidated);
        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task NonAllowlistedPath_IsNotCached()
    {
        using var factory = await ReadyAsync();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        Assert.Null(response.Headers.CacheControl);
    }

    [Fact]
    public async Task AuthenticatedRead_IsNeverMarkedPublicCacheable()
    {
        using var factory = await ReadyAsync();
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/providers?pageSize=50");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token");
        var response = await client.SendAsync(request);

        Assert.Null(response.Headers.CacheControl);
        Assert.Null(response.Headers.ETag);
    }

    [Fact]
    public async Task PublicCatalogRead_IsCompressedWhenAccepted()
    {
        using var factory = await ReadyAsync();
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/providers?pageSize=50");
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Contains("gzip", response.Content.Headers.ContentEncoding);
    }
}
