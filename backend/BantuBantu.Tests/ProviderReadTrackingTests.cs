using BantuBantu.Application;
using BantuBantu.Domain;
using BantuBantu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace BantuBantu.Tests;

// Guards the RUN 1 B1 read-path optimisations: read-only queries must not
// populate the change tracker, and the public list must collapse to a small,
// constant number of database round trips regardless of collection count.
[Collection("diagnostics-db")]
public class ProviderReadTrackingTests
{
    private static readonly Actor Platform = new(Guid.Parse("99999999-9999-9999-9999-999999999999"), UserRole.PlatformAdmin, null);
    private static readonly Actor Customer = new(ProviderBrowseContractTests.CustomerId, UserRole.Customer, null);

    [Fact]
    public async Task ReadPaths_LeaveTheChangeTrackerEmpty()
    {
        using var factory = new DiagnosticsFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await ProviderBrowseContractTests.SeedPublishedProvider(db);
        db.ChangeTracker.Clear();

        var repo = new MarketplaceRepository(db, new ProviderScope());
        var roster = await repo.AdminProvidersAsync(Platform, default);
        await repo.AdminVerificationQueueAsync(Platform, default);
        await repo.OrdersAsync(Customer, default);
        await repo.AdminReviewsAsync(Platform, default);
        var browse = await repo.BrowseAsync(null, null, null, 1, 50, default);

        Assert.Contains(roster, p => p.Id == ProviderBrowseContractTests.ProviderId);
        Assert.Contains(browse.Items, p => p.Id == ProviderBrowseContractTests.ProviderId);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), e => e.Entity is Provider or Order or Review);
    }

    [Fact]
    public async Task Browse_CollapsesToAFixedStatementCount()
    {
        using var factory = new DiagnosticsFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            await ProviderBrowseContractTests.SeedPublishedProvider(db);
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);

        var response = await client.GetAsync("/api/providers?pageSize=50");
        response.EnsureSuccessStatusCode();
        var count = ProviderBrowseContractTests.SqlCount(response);
        Assert.InRange(count, 1, 3);
    }
}
