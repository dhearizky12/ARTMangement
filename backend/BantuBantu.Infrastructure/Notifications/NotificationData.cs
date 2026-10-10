using BantuBantu.Application;
using Microsoft.EntityFrameworkCore;

namespace BantuBantu.Infrastructure.Notifications;

/// <summary>Read-only notification projections. AsNoTracking, one round trip
/// each, no change tracking. Minimal personal data only: first names, the
/// service area at village/district level. Never full addresses or phones.</summary>
public class NotificationData(AppDbContext db) : INotificationData
{
    private static string FirstName(string fullName)
    {
        var name = (fullName ?? "").Trim();
        if (name.Length == 0) return "Pelanggan";
        var space = name.IndexOf(' ');
        return space < 0 ? name : name[..space];
    }
    public async Task<OrderMailData?> OrderAsync(Guid orderId, CancellationToken ct)
    {
        var row = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new
            {
                o.Id,
                o.Version,
                CustomerEmail = o.Customer.Email,
                CustomerName = o.Customer.FullName,
                o.CustomerId,
                ProviderName = o.Provider.FullName,
                ProviderEmail = o.Provider.Credential != null ? o.Provider.Credential.Email : "",
                Verified = o.Provider.Credential != null && o.Provider.Credential.EmailVerifiedAt != null,
                o.Provider.AgencyId,
                Services = o.Provider.Categories.Select(c => c.ServiceCategory.Name).ToArray(),
                Scheduled = o.ScheduledDate,
                Village = o.Village.Name,
                District = o.Village.District.Name
            })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;
        return new OrderMailData(row.Id, row.Version, row.CustomerEmail, FirstName(row.CustomerName), FirstName(row.ProviderName),
            row.ProviderEmail, row.Verified, row.AgencyId, row.Services,
            row.Scheduled.ToString("d MMMM yyyy", new System.Globalization.CultureInfo("id-ID")), row.Village, row.District);
    }
    public async Task<OrderMailData?> OrderBookingAsync(Guid orderId, Guid customerId, Guid providerId, string villageId, string scheduledDate, CancellationToken ct)
    {
        var customer = await db.Users.AsNoTracking().Where(u => u.Id == customerId)
            .Select(u => new { u.Email, u.FullName }).SingleOrDefaultAsync(ct);
        if (customer is null) return null;
        var provider = await db.Providers.AsNoTracking().Where(p => p.Id == providerId)
            .Select(p => new
            {
                p.FullName,
                Email = p.Credential != null ? p.Credential.Email : "",
                Verified = p.Credential != null && p.Credential.EmailVerifiedAt != null,
                p.AgencyId,
                Services = p.Categories.Select(c => c.ServiceCategory.Name).ToArray()
            })
            .SingleOrDefaultAsync(ct);
        if (provider is null) return null;
        var area = await db.Villages.AsNoTracking().Where(v => v.Id == villageId)
            .Select(v => new { v.Name, District = v.District.Name }).SingleOrDefaultAsync(ct);
        if (area is null) return null;
        return new OrderMailData(orderId, 0, customer.Email, FirstName(customer.FullName), FirstName(provider.FullName),
            provider.Email, provider.Verified, provider.AgencyId, provider.Services, scheduledDate, area.Name, area.District);
    }
    public async Task<ProviderMailData?> ProviderAsync(Guid providerId, CancellationToken ct)
    {
        var row = await db.Providers.AsNoTracking()
            .Where(p => p.Id == providerId)
            .Select(p => new
            {
                p.Id,
                p.Version,
                p.FullName,
                Email = p.Credential != null ? p.Credential.Email : "",
                Verified = p.Credential != null && p.Credential.EmailVerifiedAt != null,
                p.AgencyId,
                Note = p.ModerationNote
            })
            .SingleOrDefaultAsync(ct);
        if (row is null) return null;
        return new ProviderMailData(row.Id, row.Version, FirstName(row.FullName), row.Email, row.Verified, row.AgencyId, row.Note);
    }
    public async Task<AgencyMailData?> AgencyAsync(Guid agencyId, CancellationToken ct)
    {
        var row = await db.Agencies.AsNoTracking().Where(a => a.Id == agencyId)
            .Select(a => new { a.Id, a.Name }).SingleOrDefaultAsync(ct);
        return row is null ? null : new AgencyMailData(row.Id, row.Name);
    }
    public Task<List<string>> AgencyAdminEmailsAsync(Guid agencyId, CancellationToken ct) =>
        db.AdminAccounts.AsNoTracking()
            .Where(a => a.AgencyId == agencyId && a.Role == Domain.UserRole.AgencyAdmin)
            .Select(a => a.Email).ToListAsync(ct);
    public Task<List<string>> PlatformAdminEmailsAsync(CancellationToken ct) =>
        db.AdminAccounts.AsNoTracking()
            .Where(a => a.AgencyId == null && a.Role == Domain.UserRole.PlatformAdmin)
            .Select(a => a.Email).ToListAsync(ct);
}
