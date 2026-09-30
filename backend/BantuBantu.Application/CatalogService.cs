using BantuBantu.Domain;
namespace BantuBantu.Application;

public class CatalogService(IMarketplaceRepository repo, ICurrentActor current) : ApplicationService(current)
{
    public Task<List<ServiceCategory>> Categories(CancellationToken ct) { Platform(); return repo.CategoriesAsync(ct); }
    public async Task<ServiceCategory> Category(Guid? id, CategoryRequest r, CancellationToken ct) { var actor = Platform(); var c = id.HasValue ? await repo.CategoryAsync(id.Value, ct) ?? throw new ProfileException("Kategori tidak ditemukan.", 404) : new ServiceCategory { Id = Guid.NewGuid() }; c.Slug = r.Slug; c.Name = r.Name.Trim(); c.Description = r.Description.Trim(); c.IconKey = r.IconKey; c.SortOrder = r.SortOrder; c.IsActive = r.IsActive; c.IsFeatured = r.IsFeatured; if (!id.HasValue) repo.AddCategory(c); repo.Audit(actor, null, "category.save", c.Id.ToString()); await repo.SaveAsync(ct); return c; }
    public async Task DeleteCategory(Guid id, CancellationToken ct) { Platform(); var c = await repo.CategoryAsync(id, ct) ?? throw new ProfileException("Kategori tidak ditemukan.", 404); if (await repo.CategoryInUseAsync(id, ct)) throw new ProfileException("Kategori digunakan provider. Nonaktifkan kategori tersebut.", 409); repo.RemoveCategory(c); await repo.SaveAsync(ct); }
    public Task<List<ContentBlock>> Content(CancellationToken ct) => repo.ContentAsync(ct);
    public Task<List<ContentBlock>> AdminContent(CancellationToken ct) { Platform(); return repo.AdminContentAsync(ct); }
    public async Task<ContentBlock> SaveTrust(string? id, TrustSectionRequest r, CancellationToken ct)
    {
        var actor = Platform();
        var key = id ?? r.Id;
        if (key != r.Id || key.Length > 80 || !System.Text.RegularExpressions.Regex.IsMatch(key, "^[a-z0-9-]+$")) throw new ProfileException("Kode konten tidak valid.");
        var c = (await repo.AdminContentAsync(ct)).SingleOrDefault(x => x.Id == key);
        if (c is null) { c = new ContentBlock { Id = key }; repo.AddContent(c); }
        c.Title = r.Title.Trim(); c.Body = r.Body.Trim(); c.IconName = r.IconName?.Trim(); c.SortOrder = r.SortOrder; c.IsActive = r.IsActive;
        repo.Audit(actor, "trust-section.save", "ContentBlock", null, detail: key);
        await repo.SaveAsync(ct);
        return c;
    }
    public async Task DeleteTrust(string id, CancellationToken ct)
    {
        var actor = Platform();
        var c = (await repo.AdminContentAsync(ct)).SingleOrDefault(x => x.Id == id) ?? throw new ProfileException("Konten tidak ditemukan.", 404);
        repo.RemoveContent(c);
        repo.Audit(actor, "trust-section.delete", "ContentBlock", null, detail: id);
        await repo.SaveAsync(ct);
    }
    public async Task<ContentBlock> Content(string id, ContentRequest r, CancellationToken ct)
    {
        var actor = Platform();
        if (id.Length > 80 || !System.Text.RegularExpressions.Regex.IsMatch(id, "^[a-z0-9-]+$")) throw new ProfileException("Kode konten tidak valid.");
        var c = (await repo.AdminContentAsync(ct)).SingleOrDefault(x => x.Id == id);
        if (c is null) { c = new() { Id = id }; repo.AddContent(c); }
        c.Title = r.Title.Trim(); c.Body = r.Body.Trim(); c.SortOrder = r.SortOrder; c.IsActive = true;
        repo.Audit(actor, "content.save", "ContentBlock", null, detail: id);
        await repo.SaveAsync(ct); return c;
    }
    public Task<List<AuditLogEntry>> Audit(CancellationToken ct) { Platform(); return repo.AuditAsync(ct); }
}
