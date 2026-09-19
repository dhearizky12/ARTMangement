using BantuBantu.Domain;
namespace BantuBantu.Application;

public class ProviderService(IMarketplaceRepository repo, ICurrentActor current, IWilayahRepository wilayah, IFileStorage files, IDocumentProcessor images) : ApplicationService(current)
{
    private async Task<Provider> Owned(Guid id, CancellationToken ct) => await repo.AdminProviderAsync(Admin(), id, ct) ?? throw new ProfileException("Penyedia tidak ditemukan.", 404);
    private static string Step(Provider p) => p.FullName.Length < 2 ? "personal" : p.VillageId is null ? "address" : !new[] { "KTP", "KK" }.All(t => p.Documents.Any(d => d.DocumentType == t)) ? "documents" : p.Categories.Count == 0 || p.Skills.Count == 0 || p.Languages.Count == 0 || p.Price <= 0 || p.Availability.Count != 7 ? "profile" : "verify";
    private static void Invalidate(Provider p) { p.VerificationStatus = VerificationStatus.Pending; p.IdentityVerified = p.BackgroundCheckPassed = p.ContractSigned = false; }
    private static ProviderDto Map(Provider p) => new(p.Id, p.AgencyId, p.Agency?.Name, p.FullName, p.Age, p.Bio, p.YearsOfExperience, p.Orders.Count(o => o.Status == OrderStatus.Completed), p.PricingType, p.Price, p.VerificationStatus, p.IdentityVerified, p.BackgroundCheckPassed, p.ContractSigned, p.Village is null ? null : $"{p.Village.Name}, {p.Village.District.Name}, {p.Village.District.Regency.Name}, {p.Village.District.Regency.Province.Name}", p.Categories.Where(c => c.ServiceCategory.IsActive).Select(c => new CategorySummary(c.ServiceCategoryId, c.ServiceCategory.Name)).ToArray(), p.Skills.Select(s => s.SkillName).ToArray(), p.Languages.Select(s => s.LanguageName).ToArray(), p.Availability.OrderBy(a => a.DayOfWeek).Select(a => new AvailabilityRequest(a.DayOfWeek, a.IsAvailable)).ToArray(), p.Reviews.Count == 0 ? null : p.Reviews.Average(r => (double)r.Rating), p.Reviews.Count, p.Reviews.OrderByDescending(r => r.CreatedAt).Take(50).Select(r => new ReviewDto(r.Rating, r.Comment, r.CreatedAt)).ToArray());
    private static ProviderAdminDto AdminMap(Provider p) => new(Map(p), Step(p), p.VillageId, p.AddressDetail, p.PostalCode, p.Documents.Select(d => new ProviderDocumentDto(d.Id, d.DocumentType, d.UploadedAt)).ToArray());
    public async Task<ProviderPage> Browse(string? q, Guid? category, string? villageId, int page, int size, CancellationToken ct) { page = Math.Clamp(page, 1, 10000); size = Math.Clamp(size, 1, 20); var result = await repo.BrowseAsync(q, category, villageId, page, size, ct); return new(result.Total, page, size, result.Items.Select(Map).ToArray()); }
    public async Task<ProviderDto> Detail(Guid id, CancellationToken ct) => Map(await repo.PublicProviderAsync(id, ct) ?? throw new ProfileException("Penyedia tidak ditemukan.", 404));
    public async Task<ProviderAdminDto[]> Roster(CancellationToken ct) => (await repo.AdminProvidersAsync(Admin(), ct)).Select(AdminMap).ToArray();
    public async Task<ProviderAdminDto> Draft(DraftRequest request, CancellationToken ct)
    {
        var actor = Admin(); var agencyId = request.AgencyId;
        if (actor.Role == UserRole.AgencyAdmin) { if (agencyId.HasValue && agencyId != actor.AgencyId) throw new ProfileException("Agency tidak sesuai akun.", 403); agencyId = actor.AgencyId ?? throw new ProfileException("Agency wajib.", 403); }
        if (agencyId.HasValue && (await repo.AgencyAsync(agencyId.Value, ct))?.Status != AgencyStatus.Approved) throw new ProfileException("Agency belum disetujui.");
        var p = new Provider { AgencyId = agencyId }; repo.AddProvider(p); repo.Audit(actor, p.Id, "provider.create", agencyId?.ToString() ?? "direct"); await repo.SaveAsync(ct); return AdminMap(p);
    }
    public async Task<ProviderAdminDto> AdminDetail(Guid id, CancellationToken ct) => AdminMap(await Owned(id, ct));
    private async Task<ProviderAdminDto> Saved(Provider p, string action, CancellationToken ct) { p.UpdatedAt = DateTimeOffset.UtcNow; repo.Audit(Admin(), p.Id, action, ""); await repo.SaveAsync(ct); return AdminMap(await Owned(p.Id, ct)); }
    public async Task<ProviderAdminDto> Personal(Guid id, ProviderPersonalRequest r, CancellationToken ct) { var p = await Owned(id, ct); if (r.YearsOfExperience > r.Age - 18 || r.FullName.Trim().Length < 2 || r.Bio.Trim().Length < 10) throw new ProfileException("Periksa nama, bio, dan pengalaman (mulai usia 18 tahun)."); p.FullName = r.FullName.Trim(); p.Age = r.Age; p.Bio = r.Bio.Trim(); p.YearsOfExperience = r.YearsOfExperience; Invalidate(p); return await Saved(p, "provider.personal", ct); }
    public async Task<ProviderAdminDto> Address(Guid id, AddressRequest r, CancellationToken ct) { var p = await Owned(id, ct); if (Step(p) == "personal") throw new ProfileException("Isi informasi personal terlebih dahulu.", 409); if (await wilayah.FindAsync(r.VillageId, ct) is null || r.AddressDetail.Trim().Length < 10) throw new ProfileException("Pilih wilayah valid dan lengkapi detail alamat."); p.VillageId = r.VillageId; p.AddressDetail = r.AddressDetail.Trim(); p.PostalCode = r.PostalCode; Invalidate(p); return await Saved(p, "provider.address", ct); }
    public async Task<ProviderAdminDto> Document(Guid id, string type, Stream stream, long length, string contentType, CancellationToken ct)
    {
        var p = await Owned(id, ct); if (Step(p) is "personal" or "address") throw new ProfileException("Lengkapi personal dan alamat terlebih dahulu.", 409);
        if (!images.Rules.DocumentTypes.Contains(type)) throw new ProfileException("Jenis dokumen harus KTP atau KK.");
        await using var normalized = await images.ValidateAndNormalizeAsync(stream, length, contentType, ct); var key = await files.StoreAsync(normalized, ct); var existing = p.Documents.SingleOrDefault(d => d.DocumentType == type); var old = existing?.StorageKey;
        if (existing is null) repo.AddDocument(new() { ProviderId = p.Id, Provider = p, DocumentType = type, StorageKey = key }); else { existing.StorageKey = key; existing.UploadedAt = DateTimeOffset.UtcNow; }
        Invalidate(p);
        p.UpdatedAt = DateTimeOffset.UtcNow;
        repo.Audit(Admin(), p.Id, "provider.document." + type, "");
        try { await repo.SaveAsync(ct); }
        catch { await files.DeleteAsync(key, CancellationToken.None); throw; }
        if (old is not null) await files.DeleteAsync(old, ct);
        return AdminMap(await Owned(id, ct));
    }
    public async Task<(Stream Content, string Name)> DocumentDownload(Guid id, Guid documentId, CancellationToken ct) { var p = await Owned(id, ct); var doc = p.Documents.SingleOrDefault(d => d.Id == documentId) ?? throw new ProfileException("Dokumen tidak ditemukan.", 404); return (await files.OpenAsync(doc.StorageKey, ct), doc.DocumentType + ".jpg"); }
    public async Task<ProviderAdminDto> Profile(Guid id, ProviderProfileRequest r, CancellationToken ct)
    {
        var p = await Owned(id, ct); if (Step(p) is "personal" or "address" or "documents") throw new ProfileException("Lengkapi personal, alamat, dan dokumen terlebih dahulu.", 409);
        if (!await repo.CategoriesExistAsync(r.CategoryIds, ct) || r.Availability.Any(a => a is null) || r.Availability.Select(a => a.DayOfWeek).Distinct().Count() != 7 || !r.Availability.Any(a => a.IsAvailable)) throw new ProfileException("Kategori aktif dan tujuh hari ketersediaan diperlukan.");
        if (decimal.Round(r.Price, 2) != r.Price) throw new ProfileException("Tarif maksimal dua angka desimal.");
        var skills = Tags(r.Skills); var languages = Tags(r.Languages);
        p.Categories.RemoveAll(c => !r.CategoryIds.Contains(c.ServiceCategoryId)); foreach (var c in r.CategoryIds.Except(p.Categories.Select(c => c.ServiceCategoryId)).ToArray()) p.Categories.Add(new() { ProviderId = id, ServiceCategoryId = c });
        p.Skills.RemoveAll(s => !skills.Contains(s.SkillName)); foreach (var s in skills.Except(p.Skills.Select(s => s.SkillName)).ToArray()) p.Skills.Add(new() { ProviderId = id, SkillName = s });
        p.Languages.RemoveAll(s => !languages.Contains(s.LanguageName)); foreach (var s in languages.Except(p.Languages.Select(s => s.LanguageName)).ToArray()) p.Languages.Add(new() { ProviderId = id, LanguageName = s });
        foreach (var a in r.Availability) { var day = p.Availability.SingleOrDefault(x => x.DayOfWeek == a.DayOfWeek); if (day is null) p.Availability.Add(new() { ProviderId = id, DayOfWeek = a.DayOfWeek, IsAvailable = a.IsAvailable }); else day.IsAvailable = a.IsAvailable; }
        p.Price = r.Price; p.PricingType = r.PricingType; Invalidate(p); return await Saved(p, "provider.profile", ct);
    }
    private static string[] Tags(string[] values) { if (values.Any(v => string.IsNullOrWhiteSpace(v) || v.Trim().Length > 80)) throw new ProfileException("Tag harus berisi 1–80 karakter."); return values.Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); }
    public async Task<ProviderAdminDto> Verify(Guid id, VerifyRequest r, CancellationToken ct)
    {
        var p = await Owned(id, ct); if (r.Status == VerificationStatus.Verified && (Step(p) != "verify" || !r.IdentityVerified || !r.BackgroundCheckPassed || !r.ContractSigned || !await repo.CategoriesExistAsync(p.Categories.Select(c => c.ServiceCategoryId).ToArray(), ct))) throw new ProfileException("Lengkapi semua tahap dan tiga pemeriksaan sebelum verifikasi.");
        p.IdentityVerified = r.IdentityVerified; p.BackgroundCheckPassed = r.BackgroundCheckPassed; p.ContractSigned = r.ContractSigned; p.VerificationStatus = r.Status; p.UpdatedAt = DateTimeOffset.UtcNow; repo.Audit(Admin(), id, "provider.verify", $"{r.Status}; identity={r.IdentityVerified}; background={r.BackgroundCheckPassed}; contract={r.ContractSigned}; {r.Note}"); await repo.SaveAsync(ct); return AdminMap(p);
    }
}
