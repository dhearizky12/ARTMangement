using BantuBantu.Application;
using BantuBantu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace BantuBantu.Tests;

// End-to-end proof that the two global catalog reads are served from the read
// cache: a warm request performs zero database statements.
[Collection("diagnostics-db")]
public class ReadCacheIntegrationTests
{
    private static async Task Prepare(DiagnosticsFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    private static async Task<HttpResponseMessage> Warm(HttpClient client, string url)
    {
        var cold = await client.GetAsync(url);
        cold.EnsureSuccessStatusCode();
        Assert.InRange(ProviderBrowseContractTests.SqlCount(cold), 1, 3);
        var warm = await client.GetAsync(url);
        warm.EnsureSuccessStatusCode();
        return warm;
    }

    [Fact]
    public async Task ServiceCategories_SecondRequestHitsCache()
    {
        using var factory = new DiagnosticsFactory();
        await Prepare(factory);
        using var client = factory.CreateClient();
        var warm = await Warm(client, "/api/service-categories");
        Assert.Equal(0, ProviderBrowseContractTests.SqlCount(warm));
    }

    [Fact]
    public async Task Content_SecondRequestHitsCache()
    {
        using var factory = new DiagnosticsFactory();
        await Prepare(factory);
        using var client = factory.CreateClient();
        var warm = await Warm(client, "/api/content");
        Assert.Equal(0, ProviderBrowseContractTests.SqlCount(warm));
    }

    // The cached content snapshot must be a single reusable instance that is
    // never EF-tracked, so later requests cannot accidentally observe or flush
    // mutations to it through a DbContext.
    [Fact]
    public async Task Content_SnapshotIsReusedAndDetached()
    {
        using var factory = new DiagnosticsFactory();
        await Prepare(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.ContentBlocks.AnyAsync(c => c.Id == "cache-uji"))
        {
            db.ContentBlocks.Add(new BantuBantu.Domain.ContentBlock { Id = "cache-uji", Title = "Uji", Body = "Isi", SortOrder = 1, IsActive = true });
            await db.SaveChangesAsync();
        }

        var catalog = scope.ServiceProvider.GetRequiredService<CatalogService>();
        var first = await catalog.Content(CancellationToken.None);
        var second = await catalog.Content(CancellationToken.None);

        Assert.NotEmpty(first);
        Assert.Same(first, second);
        Assert.All(first, block => Assert.Equal(EntityState.Detached, db.Entry(block).State));
    }
}
