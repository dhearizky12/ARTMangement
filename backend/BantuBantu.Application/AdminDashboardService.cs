namespace BantuBantu.Application;

// Dashboard counters for the admin area. Scoping is delegated to the repository
// so AgencyAdmin sees only its own agency's totals while PlatformAdmin sees the
// whole platform.
public class AdminDashboardService(IMarketplaceRepository repo, ICurrentActor current) : ApplicationService(current)
{
    public Task<AdminDashboardStats> Stats(CancellationToken ct) => repo.AdminDashboardStatsAsync(Admin(), ct);
}
