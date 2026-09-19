using BantuBantu.Application;
using BantuBantu.Domain;
using Microsoft.EntityFrameworkCore;
namespace BantuBantu.Infrastructure;
// Every admin provider/order lookup goes through this fail-closed scope, including mutation lookups.
public interface IProviderScope { IQueryable<Provider> Apply(IQueryable<Provider> query, Actor actor); }
public class ProviderScope : IProviderScope
{
    public IQueryable<Provider> Apply(IQueryable<Provider> query, Actor actor) => actor.Role switch
    {
        UserRole.PlatformAdmin => query,
        UserRole.AgencyAdmin when actor.AgencyId.HasValue => query.Where(p => p.AgencyId == actor.AgencyId),
        _ => query.Where(p => false)
    };
}
public class MarketplaceRepository(AppDbContext db, IProviderScope scope) : IMarketplaceRepository
{
    private IQueryable<Provider> Details(IQueryable<Provider> query) => query.AsSplitQuery().Include(p => p.Agency).Include(p => p.Village)!.ThenInclude(v => v!.District).ThenInclude(d => d.Regency).ThenInclude(r => r.Province).Include(p => p.Categories).ThenInclude(c => c.ServiceCategory).Include(p => p.Skills).Include(p => p.Languages).Include(p => p.Availability).Include(p => p.Documents).Include(p => p.Reviews).Include(p => p.Orders);
    private IQueryable<Provider> Published() => db.Providers.Where(p => p.VerificationStatus == VerificationStatus.Verified && (p.AgencyId == null || p.Agency!.Status == AgencyStatus.Approved) && p.Categories.Any(c => c.ServiceCategory.IsActive));
    public async Task<(int Total, List<Provider> Items)> BrowseAsync(string? q, Guid? category, string? villageId, int page, int pageSize, CancellationToken ct)
    {
        var query = Published().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q)) { var term = q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_"); query = query.Where(p => EF.Functions.ILike(p.FullName, "%" + term + "%", "\\") || p.Skills.Any(s => EF.Functions.ILike(s.SkillName, "%" + term + "%", "\\"))); }
        if (category.HasValue) query = query.Where(p => p.Categories.Any(c => c.ServiceCategoryId == category && c.ServiceCategory.IsActive));
        if (!string.IsNullOrEmpty(villageId)) query = query.Where(p => p.VillageId == villageId);
        var count = await query.CountAsync(ct);
        var items = await Details(query.OrderByDescending(p => p.Reviews.Select(r => (double?)r.Rating).Average() ?? 0).ThenBy(p => p.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return (count, items);
    }
    public Task<Provider?> PublicProviderAsync(Guid id, CancellationToken ct) => Details(Published()).SingleOrDefaultAsync(p => p.Id == id, ct);
    public Task<List<Provider>> AdminProvidersAsync(Actor actor, CancellationToken ct) => Details(scope.Apply(db.Providers, actor)).OrderByDescending(p => p.CreatedAt).Take(200).ToListAsync(ct);
    public Task<Provider?> AdminProviderAsync(Actor actor, Guid id, CancellationToken ct) => Details(scope.Apply(db.Providers, actor)).SingleOrDefaultAsync(p => p.Id == id, ct);
    public void AddDocument(ProviderDocument document) => db.Set<ProviderDocument>().Add(document);
    public void AddProvider(Provider p) => db.Providers.Add(p);
    public async Task<bool> CategoriesExistAsync(Guid[] ids, CancellationToken ct) => ids.Distinct().Count() == ids.Length && await db.ServiceCategories.CountAsync(c => ids.Contains(c.Id) && c.IsActive, ct) == ids.Length;
    public Task<List<ServiceCategory>> CategoriesAsync(CancellationToken ct) => db.ServiceCategories.OrderBy(c => c.SortOrder).ToListAsync(ct);
    public Task<ServiceCategory?> CategoryAsync(Guid id, CancellationToken ct) => db.ServiceCategories.SingleOrDefaultAsync(c => c.Id == id, ct);
    public void AddCategory(ServiceCategory c) => db.ServiceCategories.Add(c);
    public Task<bool> CategoryInUseAsync(Guid id, CancellationToken ct) => db.Set<ProviderCategory>().AnyAsync(c => c.ServiceCategoryId == id, ct);
    public void RemoveCategory(ServiceCategory c) => db.ServiceCategories.Remove(c);
    public Task<List<Agency>> AgenciesAsync(CancellationToken ct) => db.Agencies.OrderBy(a => a.Name).ToListAsync(ct);
    public Task<Agency?> AgencyAsync(Guid id, CancellationToken ct) => db.Agencies.SingleOrDefaultAsync(a => a.Id == id, ct);
    public void AddAgency(Agency a) => db.Agencies.Add(a);
    public Task<List<ContentBlock>> ContentAsync(CancellationToken ct) => db.ContentBlocks.OrderBy(c => c.SortOrder).ToListAsync(ct);
    public Task<ContentBlock?> ContentAsync(string id, CancellationToken ct) => db.ContentBlocks.SingleOrDefaultAsync(c => c.Id == id, ct);
    public void AddContent(ContentBlock c) => db.ContentBlocks.Add(c);
    private IQueryable<Order> ScopedOrders(Actor actor)
    {
        if (actor.Role == UserRole.Customer) return db.Orders.Where(o => o.CustomerId == actor.Id);
        var ids = scope.Apply(db.Providers, actor).Select(p => p.Id);
        return db.Orders.Where(o => ids.Contains(o.ProviderId));
    }
    public Task<List<Order>> OrdersAsync(Actor actor, CancellationToken ct) => ScopedOrders(actor).Include(o => o.Provider).Include(o => o.Review).OrderByDescending(o => o.CreatedAt).Take(200).ToListAsync(ct);
    public Task<Order?> OrderAsync(Actor actor, Guid id, CancellationToken ct) => ScopedOrders(actor).Include(o => o.Provider).Include(o => o.Review).SingleOrDefaultAsync(o => o.Id == id, ct);
    public void AddOrder(Order o) => db.Orders.Add(o);
    public void AddReview(Review r) => db.Reviews.Add(r);
    public void Audit(Actor actor, Guid? providerId, string action, string detail) => db.AuditEntries.Add(new() { ActorId = actor.Id, ProviderId = providerId, Action = action, Detail = detail });
    public Task<List<AuditEntry>> AuditAsync(CancellationToken ct) => db.AuditEntries.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(200).ToListAsync(ct);
    public async Task SaveAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);
}
