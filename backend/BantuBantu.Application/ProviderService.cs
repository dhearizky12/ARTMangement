using BantuBantu.Domain;
namespace BantuBantu.Application;

public class ProviderService(IMarketplaceRepository repo, ICurrentActor current, IWilayahRepository wilayah, IFileStorage files, IDocumentProcessor images, IPasswordService passwords, IAuthRepository auth) : ApplicationService(current)
{
    private async Task<Provider> Owned(Guid id, CancellationToken ct) => await repo.AdminProviderAsync(Admin(), id, ct) ?? throw new ProfileException("Penyedia tidak ditemukan.", 404);
    private async Task<Provider> Own(CancellationToken ct) { var actor = Provider(); return await repo.AdminProviderAsync(actor, actor.ProviderId!.Value, ct) ?? throw new ProfileException("Penyedia tidak ditemukan.", 404); }
    private static string Step(Provider p) => p.FullName.Length < 2 ? "personal" : p.VillageId is null ? "address" : !new[] { "KTP", "KK" }.All(t => p.Documents.Any(d => d.DocumentType == t)) ? "documents" : p.Categories.Count == 0 || p.Skills.Count == 0 || p.Languages.Count == 0 || p.Price <= 0 || p.Availability.Count != 7 ? "profile" : "verify";
    private const string Empty = "belum";
    private const string Partial = "sebagian";
    private const string Complete = "lengkapi";
    private static string Status(bool complete, bool started) => complete ? Complete : started ? Partial : Empty;
    private static ApplicationSectionDto[] Sections(Provider p) => new ApplicationSectionDto[]
    {
        new("personal", "Personal", Status(p.FullName.Length >= 2 && p.Age >= 18 && p.Bio.Length >= 10, p.FullName.Length >= 2 || p.Age >= 18 || p.Bio.Length >= 10)),
        new("address", "Alamat", Status(p.VillageId is not null && p.AddressDetail.Length >= 10 && p.PostalCode.Length == 5, p.VillageId is not null || p.AddressDetail.Length >= 10 || p.PostalCode.Length == 5)),
        new("documents", "Dokumen", Status(new[] { "KTP", "KK" }.All(t => p.Documents.Any(d => d.DocumentType == t)), p.Documents.Any())),
        new("profile", "Layanan", Status(p.Categories.Count > 0 && p.Skills.Count > 0 && p.Languages.Count > 0 && p.Price > 0 && p.Availability.Count == 7 && p.Availability.Any(a => a.IsAvailable), p.Categories.Count > 0 || p.Skills.Count > 0 || p.Languages.Count > 0 || p.Price > 0 || p.Availability.Count == 7)),
    };
    private static bool CanEdit(Provider p) => p.ApplicationStatus is ProviderApplicationStatus.Draft or ProviderApplicationStatus.NeedsChanges;
    private static void Invalidate(Provider p) { p.VerificationStatus = VerificationStatus.Pending; p.IdentityVerified = p.BackgroundCheckPassed = p.ContractSigned = false; }
    private static ProviderDto Map(Provider p)
    {
        var visibleReviews = p.Reviews.Where(r => !r.IsHidden).ToArray();
        return new(p.Id, p.AgencyId, p.Agency?.Name, p.FullName, p.Age, p.Bio, p.YearsOfExperience, p.Orders.Count(o => o.Status == OrderStatus.Completed), p.PricingType, p.Price, p.VerificationStatus, p.IdentityVerified, p.BackgroundCheckPassed, p.ContractSigned, p.Village is null ? null : $"{p.Village.Name}, {p.Village.District.Name}, {p.Village.District.Regency.Name}, {p.Village.District.Regency.Province.Name}", p.Categories.Select(c => new CategorySummary(c.ServiceCategoryId, c.ServiceCategory.Name)).ToArray(), p.Skills.Select(s => s.SkillName).ToArray(), p.Languages.Select(s => s.LanguageName).ToArray(), p.Availability.OrderBy(a => a.DayOfWeek).Select(a => new AvailabilityRequest(a.DayOfWeek, a.IsAvailable)).ToArray(), visibleReviews.Length == 0 ? null : visibleReviews.Average(r => (double)r.Rating), visibleReviews.Length, visibleReviews.OrderByDescending(r => r.CreatedAt).Take(50).Select(r => new ReviewDto(r.Rating, r.Comment, r.CreatedAt)).ToArray(), p.ApplicationStatus, p.ModerationNote);
    }
    private static ProviderAdminDto AdminMap(Provider p) => new(Map(p), Step(p), p.VillageId, p.AddressDetail, p.PostalCode, p.Documents.Select(d => new ProviderDocumentDto(d.Id, d.DocumentType, d.UploadedAt)).ToArray());
    public async Task<ProviderPage> Browse(string? q, Guid? category, string? villageId, int page, int size, CancellationToken ct) { page = Math.Clamp(page, 1, 10000); size = Math.Clamp(size, 1, 20); var result = await repo.BrowseAsync(q, category, villageId, page, size, ct); return new(result.Total, page, size, result.Items.Select(Map).ToArray()); }
    public async Task<ProviderDto> Detail(Guid id, CancellationToken ct) => Map(await repo.PublicProviderAsync(id, ct) ?? throw new ProfileException("Penyedia tidak ditemukan.", 404));
    public async Task<ProviderAdminDto[]> Roster(CancellationToken ct) => (await repo.AdminProvidersAsync(Admin(), ct)).Select(AdminMap).ToArray();
    public async Task<ProviderAdminDto[]> Applications(ProviderApplicationStatus? status, CancellationToken ct)
    {
        var providers = await repo.AdminProvidersAsync(Admin(), ct);
        return providers.Where(p => !status.HasValue || p.ApplicationStatus == status.Value).Select(AdminMap).ToArray();
    }
    public async Task<ProviderAdminDto[]> VerificationQueue(CancellationToken ct)
    {
        return (await repo.AdminVerificationQueueAsync(Admin(), ct)).Select(AdminMap).ToArray();
    }
    public async Task<ProviderAdminDto> Draft(DraftRequest request, CancellationToken ct)
    {
        var actor = Admin(); var agencyId = request.AgencyId;
        if (actor.Role == UserRole.AgencyAdmin) { if (agencyId.HasValue && agencyId != actor.AgencyId) throw new ProfileException("Agency tidak sesuai akun.", 403); agencyId = actor.AgencyId ?? throw new ProfileException("Agency wajib.", 403); }
        if (agencyId.HasValue && (await repo.AgencyAsync(agencyId.Value, ct))?.Status != AgencyStatus.Approved) throw new ProfileException("Agency belum disetujui.");
        var email = request.Email.Trim().ToLowerInvariant();
        if (await auth.FindProviderCredentialAsync(email, ct) is not null) throw new ProfileException("Email Provider sudah terdaftar.", 409);
        var p = new Provider { AgencyId = agencyId };
        p.Credential = new ProviderCredential { ProviderId = p.Id, Provider = p, Email = email, PasswordHash = passwords.HashProvider(request.Password) };
        repo.AddProvider(p); repo.Audit(actor, p.Id, "provider.create", agencyId?.ToString() ?? "direct"); await repo.SaveAsync(ct); return AdminMap(p);
    }
    public async Task<ProviderDto> OwnProfile(CancellationToken ct) => Map(await Own(ct));
    public async Task<ProviderApplicationDto> OwnApplication(CancellationToken ct)
    {
        var p = await Own(ct);
        return new(AdminMap(p), p.ApplicationStatus, Step(p), p.ModerationNote, p.SubmittedAt, CanEdit(p), Sections(p));
    }
    public async Task<ProviderApplicationDto> SubmitApplication(CancellationToken ct)
    {
        var actor = Provider(); var p = await Own(ct);
        if (!CanEdit(p)) throw new ProfileException("Aplikasi sedang ditinjau atau sudah diproses.", 409);
        if (Step(p) != "verify") throw new ProfileException("Lengkapi seluruh profil dan dokumen sebelum mengirim aplikasi.", 409);
        p.ApplicationStatus = ProviderApplicationStatus.Submitted;
        p.SubmittedAt = DateTimeOffset.UtcNow;
        p.ModerationNote = null;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        repo.Audit(actor, p.Id, "provider.application.submit", "");
        await repo.SaveAsync(ct);
        return await OwnApplication(ct);
    }
    public async Task<ProviderDto> UpdateOwnAvailability(AvailabilityUpdateRequest request, CancellationToken ct)
    {
        var actor = Provider();
        if (request.Availability is null || request.Availability.Any(a => a is null) || request.Availability.Select(a => a.DayOfWeek).Distinct().Count() != 7 || !request.Availability.Any(a => a.IsAvailable)) throw new ProfileException("Isi ketersediaan untuk tujuh hari dan pilih minimal satu hari.");
        var p = await repo.AdminProviderAsync(actor, actor.ProviderId!.Value, ct) ?? throw new ProfileException("Penyedia tidak ditemukan.", 404);
        foreach (var a in request.Availability)
        {
            var day = p.Availability.SingleOrDefault(x => x.DayOfWeek == a.DayOfWeek);
            if (day is null) p.Availability.Add(new() { ProviderId = p.Id, DayOfWeek = a.DayOfWeek, IsAvailable = a.IsAvailable });
            else day.IsAvailable = a.IsAvailable;
        }
        p.UpdatedAt = DateTimeOffset.UtcNow;
        repo.Audit(actor, p.Id, "provider.availability", "self-service");
        await repo.SaveAsync(ct);
        return Map(await Own(ct));
    }
    public async Task<ProviderAdminDto> OwnPersonal(ProviderPersonalRequest request, CancellationToken ct)
    {
        var actor = Provider(); var p = await Own(ct); EnsureEditable(p);
        ApplyPersonal(p, request);
        return await Saved(p, actor, "provider.personal.self", ct);
    }
    public async Task<ProviderAdminDto> OwnAddress(AddressRequest request, CancellationToken ct)
    {
        var actor = Provider(); var p = await Own(ct); EnsureEditable(p);
        if (await wilayah.FindAsync(request.VillageId, ct) is null || request.AddressDetail.Trim().Length < 10) throw new ProfileException("Pilih wilayah valid dan lengkapi detail alamat.");
        p.VillageId = request.VillageId; p.AddressDetail = request.AddressDetail.Trim(); p.PostalCode = request.PostalCode; Invalidate(p);
        return await Saved(p, actor, "provider.address.self", ct);
    }
    public async Task<ProviderAdminDto> OwnDocument(string type, Stream stream, long length, string contentType, CancellationToken ct)
    {
        var actor = Provider(); var p = await Own(ct); EnsureEditable(p);
        return await SaveDocument(p, actor, type, stream, length, contentType, ct);
    }
    public async Task<ProviderAdminDto> OwnProfile(ProviderProfileRequest request, CancellationToken ct)
    {
        var actor = Provider(); var p = await Own(ct); EnsureEditable(p);
        return await ApplyProfileAndSave(p, actor, request, "provider.profile.self", ct);
    }
    public async Task<ProviderAdminDto> AdminDetail(Guid id, CancellationToken ct) => AdminMap(await Owned(id, ct));
    private static void EnsureEditable(Provider p) { if (!CanEdit(p)) throw new ProfileException("Aplikasi sedang ditinjau atau sudah diproses.", 409); }
    private static void ApplyPersonal(Provider p, ProviderPersonalRequest r) { if (r.YearsOfExperience > r.Age - 18 || r.FullName.Trim().Length < 2 || r.Bio.Trim().Length < 10) throw new ProfileException("Periksa nama, bio, dan pengalaman (mulai usia 18 tahun)."); p.FullName = r.FullName.Trim(); p.Age = r.Age; p.Bio = r.Bio.Trim(); p.YearsOfExperience = r.YearsOfExperience; Invalidate(p); }
    private async Task<ProviderAdminDto> Saved(Provider p, Actor actor, string action, CancellationToken ct) { p.UpdatedAt = DateTimeOffset.UtcNow; repo.Audit(actor, p.Id, action, ""); await repo.SaveAsync(ct); return AdminMap(await repo.AdminProviderAsync(actor, p.Id, ct) ?? throw new ProfileException("Penyedia tidak ditemukan.", 404)); }
    private async Task<ProviderAdminDto> Saved(Provider p, string action, CancellationToken ct) => await Saved(p, Admin(), action, ct);
    public async Task<ProviderAdminDto> Personal(Guid id, ProviderPersonalRequest r, CancellationToken ct) { var p = await Owned(id, ct); ApplyPersonal(p, r); return await Saved(p, "provider.personal", ct); }
    public async Task<ProviderAdminDto> Address(Guid id, AddressRequest r, CancellationToken ct) { var p = await Owned(id, ct); if (await wilayah.FindAsync(r.VillageId, ct) is null || r.AddressDetail.Trim().Length < 10) throw new ProfileException("Pilih wilayah valid dan lengkapi detail alamat."); p.VillageId = r.VillageId; p.AddressDetail = r.AddressDetail.Trim(); p.PostalCode = r.PostalCode; Invalidate(p); return await Saved(p, "provider.address", ct); }
    public async Task<ProviderAdminDto> Document(Guid id, string type, Stream stream, long length, string contentType, CancellationToken ct)
    {
        var p = await Owned(id, ct);
        return await SaveDocument(p, Admin(), type, stream, length, contentType, ct);
    }
    private async Task<ProviderAdminDto> SaveDocument(Provider p, Actor actor, string type, Stream stream, long length, string contentType, CancellationToken ct)
    {
        if (!images.Rules.DocumentTypes.Contains(type)) throw new ProfileException("Jenis dokumen harus KTP atau KK.");
        await using var normalized = await images.ValidateAndNormalizeAsync(stream, length, contentType, ct); var key = await files.StoreAsync(normalized, ct); var existing = p.Documents.SingleOrDefault(d => d.DocumentType == type); var old = existing?.StorageKey;
        if (existing is null) repo.AddDocument(new() { ProviderId = p.Id, Provider = p, DocumentType = type, StorageKey = key }); else { existing.StorageKey = key; existing.UploadedAt = DateTimeOffset.UtcNow; }
        Invalidate(p);
        p.UpdatedAt = DateTimeOffset.UtcNow;
        repo.Audit(actor, p.Id, "provider.document." + type, "");
        try { await repo.SaveAsync(ct); }
        catch { await files.DeleteAsync(key, CancellationToken.None); throw; }
        if (old is not null) await files.DeleteAsync(old, ct);
        return AdminMap(await repo.AdminProviderAsync(actor, p.Id, ct) ?? throw new ProfileException("Penyedia tidak ditemukan.", 404));
    }
    public async Task<(Stream Content, string Name)> DocumentDownload(Guid id, Guid documentId, CancellationToken ct) { var p = await Owned(id, ct); var doc = p.Documents.SingleOrDefault(d => d.Id == documentId) ?? throw new ProfileException("Dokumen tidak ditemukan.", 404); return (await files.OpenAsync(doc.StorageKey, ct), doc.DocumentType + ".jpg"); }
    public async Task<(Stream Content, string Name)> OwnDocumentDownload(Guid documentId, CancellationToken ct) { var p = await Own(ct); var doc = p.Documents.SingleOrDefault(d => d.Id == documentId) ?? throw new ProfileException("Dokumen tidak ditemukan.", 404); return (await files.OpenAsync(doc.StorageKey, ct), doc.DocumentType + ".jpg"); }
    public async Task<ProviderAdminDto> Profile(Guid id, ProviderProfileRequest r, CancellationToken ct)
    {
        var p = await Owned(id, ct); return await ApplyProfileAndSave(p, Admin(), r, "provider.profile", ct);
    }
    private async Task<ProviderAdminDto> ApplyProfileAndSave(Provider p, Actor actor, ProviderProfileRequest r, string action, CancellationToken ct)
    {
        if (r.CategoryIds.Length == 0 || !await repo.CategoriesExistAsync(r.CategoryIds, ct)) throw new ProfileException("Pilih minimal satu kategori layanan yang aktif.");
        if (r.Availability is null || r.Availability.Any(a => a is null) || r.Availability.Select(a => a.DayOfWeek).Distinct().Count() != 7 || !r.Availability.Any(a => a.IsAvailable)) throw new ProfileException("Isi ketersediaan untuk tujuh hari dan pilih minimal satu hari aktif.");
        if (decimal.Round(r.Price, 2) != r.Price) throw new ProfileException("Tarif maksimal dua angka desimal.");
        var skills = Tags(r.Skills); var languages = Tags(r.Languages);
        p.Categories.RemoveAll(c => !r.CategoryIds.Contains(c.ServiceCategoryId)); foreach (var c in r.CategoryIds.Except(p.Categories.Select(c => c.ServiceCategoryId)).ToArray()) p.Categories.Add(new() { ProviderId = p.Id, ServiceCategoryId = c });
        p.Skills.RemoveAll(s => !skills.Contains(s.SkillName)); foreach (var s in skills.Except(p.Skills.Select(s => s.SkillName)).ToArray()) p.Skills.Add(new() { ProviderId = p.Id, SkillName = s });
        p.Languages.RemoveAll(s => !languages.Contains(s.LanguageName)); foreach (var s in languages.Except(p.Languages.Select(s => s.LanguageName)).ToArray()) p.Languages.Add(new() { ProviderId = p.Id, LanguageName = s });
        foreach (var a in r.Availability) { var day = p.Availability.SingleOrDefault(x => x.DayOfWeek == a.DayOfWeek); if (day is null) p.Availability.Add(new() { ProviderId = p.Id, DayOfWeek = a.DayOfWeek, IsAvailable = a.IsAvailable }); else day.IsAvailable = a.IsAvailable; }
        p.Price = r.Price; p.PricingType = r.PricingType; Invalidate(p); return await Saved(p, actor, action, ct);
    }
    private static string[] Tags(string[] values) { if (values.Any(v => string.IsNullOrWhiteSpace(v) || v.Trim().Length > 80)) throw new ProfileException("Tag harus berisi 1–80 karakter."); return values.Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); }
    public async Task<ProviderAdminDto> Verify(Guid id, VerifyRequest r, CancellationToken ct)
    {
        var p = await Owned(id, ct); if (r.Status == VerificationStatus.Verified && (Step(p) != "verify" || !r.IdentityVerified || !r.BackgroundCheckPassed || !r.ContractSigned || !await repo.CategoriesExistAsync(p.Categories.Select(c => c.ServiceCategoryId).ToArray(), ct))) throw new ProfileException("Lengkapi semua tahap dan tiga pemeriksaan sebelum verifikasi.");
        if (r.Status == VerificationStatus.Rejected && string.IsNullOrWhiteSpace(r.Note)) throw new ProfileException("Alasan penolakan wajib diisi.");
        p.IdentityVerified = r.IdentityVerified; p.BackgroundCheckPassed = r.BackgroundCheckPassed; p.ContractSigned = r.ContractSigned; p.VerificationStatus = r.Status; p.ApplicationStatus = r.Status == VerificationStatus.Verified ? ProviderApplicationStatus.Approved : r.Status == VerificationStatus.Rejected ? ProviderApplicationStatus.Rejected : p.ApplicationStatus; p.ModerationNote = r.Note; p.ReviewedAt = DateTimeOffset.UtcNow; p.ReviewedBy = Admin().Id; p.UpdatedAt = DateTimeOffset.UtcNow;
        var action = r.Status == VerificationStatus.Rejected ? "provider.reject" : "provider.verify";
        repo.Audit(Admin(), action, "Provider", id, r.Note, $"{r.Status}; identity={r.IdentityVerified}; background={r.BackgroundCheckPassed}; contract={r.ContractSigned}");
        await repo.SaveAsync(ct); return AdminMap(p);
    }
    public async Task<ProviderAdminDto> Moderate(Guid id, ProviderApplicationStatus status, ProviderModerationRequest request, CancellationToken ct)
    {
        var actor = Admin(); var p = await Owned(id, ct);
        if (status == ProviderApplicationStatus.Approved)
        {
            if (Step(p) != "verify") throw new ProfileException("Lengkapi seluruh profil dan dokumen sebelum menyetujui.", 409);
            p.IdentityVerified = p.BackgroundCheckPassed = p.ContractSigned = true;
            p.VerificationStatus = VerificationStatus.Verified;
        }
        else if (status == ProviderApplicationStatus.Rejected)
        {
            if (string.IsNullOrWhiteSpace(request.Note)) throw new ProfileException("Alasan penolakan wajib diisi.");
            p.VerificationStatus = VerificationStatus.Rejected;
        }
        else if (status == ProviderApplicationStatus.NeedsChanges)
        {
            if (string.IsNullOrWhiteSpace(request.Note)) throw new ProfileException("Catatan perbaikan wajib diisi.");
            p.VerificationStatus = VerificationStatus.Pending;
        }
        else if (status == ProviderApplicationStatus.Suspended)
        {
            if (p.ApplicationStatus != ProviderApplicationStatus.Approved || p.VerificationStatus != VerificationStatus.Verified) throw new ProfileException("Hanya provider terverifikasi yang dapat ditangguhkan.", 409);
            if (string.IsNullOrWhiteSpace(request.Note)) throw new ProfileException("Alasan penangguhan wajib diisi.");
        }
        else throw new ProfileException("Status moderasi tidak valid.");
        p.ApplicationStatus = status; p.ModerationNote = request.Note?.Trim(); p.ReviewedAt = DateTimeOffset.UtcNow; p.ReviewedBy = actor.Id; p.UpdatedAt = DateTimeOffset.UtcNow;
        var action = status switch
        {
            ProviderApplicationStatus.Suspended => "provider.suspend",
            ProviderApplicationStatus.Approved => "provider.verify",
            ProviderApplicationStatus.Rejected => "provider.reject",
            _ => "provider.application." + status
        };
        repo.Audit(actor, action, "Provider", id, p.ModerationNote, p.ModerationNote ?? ""); await repo.SaveAsync(ct); return AdminMap(p);
    }
    public async Task<ProviderAdminDto> Reactivate(Guid id, CancellationToken ct)
    {
        var actor = Admin();
        var p = await Owned(id, ct);
        if (p.ApplicationStatus != ProviderApplicationStatus.Suspended || p.VerificationStatus != VerificationStatus.Verified) throw new ProfileException("Provider tidak sedang ditangguhkan.", 409);
        p.ApplicationStatus = ProviderApplicationStatus.Approved;
        p.ModerationNote = null;
        p.ReviewedAt = DateTimeOffset.UtcNow;
        p.ReviewedBy = actor.Id;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        repo.Audit(actor, "provider.reactivate", "Provider", id);
        await repo.SaveAsync(ct);
        return AdminMap(p);
    }
}
