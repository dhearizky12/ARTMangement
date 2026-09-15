using BantuBantu.Domain;
namespace BantuBantu.Application;

public class ProfileService(IProfileRepository repository, IFileStorage storage, IDocumentProcessor documents, IWilayahRepository wilayah) : IProfileService
{
    public async Task<ProfileStatusDto> StatusAsync(Guid userId, CancellationToken ct) => Status(await repository.GetAsync(userId, ct));
    private ProfileStatusDto Status(ProfileState state)
    {
        var personal = state.Personal;
        var address = state.Address;
        var document = state.Document;
        var step = state.User.ProfileCompleted ? "done" : personal is null ? "personal" : address is null ? "address" : "documents";
        return new(step, state.User.ProfileCompleted, personal is not null, address is not null, document is not null,
            personal is null ? null : new(personal.FullName, personal.BirthDate, personal.PhoneNumber, personal.Gender ?? "undisclosed"),
            address is null ? null : new(address.Province, address.City, address.District, address.AddressLine, address.PostalCode, address.VillageId, address.Village?.Name),
            document is null ? null : new(document.DocumentType, document.VerificationStatus, document.UploadedAt), documents.Rules);
    }
    private void RequireStep(ProfileState state, string expected)
    {
        var actual = Status(state).ProfileStep;
        if (actual != expected) throw new ProfileException("Tahap profil telah berubah. Lanjutkan dari tahap yang tersimpan.", 409, "PROFILE_STEP_REQUIRED", actual);
    }
    public async Task<ProfileStatusDto> PersonalAsync(Guid userId, PersonalInfoRequest request, CancellationToken ct)
    {
        await using var transaction = await repository.LockAsync(userId, ct);
        var state = await repository.GetAsync(userId, ct);
        RequireStep(state, "personal");
        repository.Add(new UserProfile { UserId = userId, FullName = request.FullName.Trim(), BirthDate = request.BirthDate, PhoneNumber = request.PhoneNumber, Gender = request.Gender });
        state.User.FullName = request.FullName.Trim();
        state.User.ProfileStep = "address";
        await repository.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return await StatusAsync(userId, ct);
    }
    public async Task<ProfileStatusDto> AddressAsync(Guid userId, AddressRequest request, CancellationToken ct)
    {
        await using var transaction = await repository.LockAsync(userId, ct);
        var state = await repository.GetAsync(userId, ct);
        RequireStep(state, "address");
        if (request.AddressDetail.Trim().Length < 10) throw new ProfileException("Detail alamat minimal 10 karakter.");
        var village = await wilayah.FindAsync(request.VillageId, ct) ?? throw new ProfileException("Kelurahan/desa tidak ditemukan. Pilih hasil pencarian yang valid.");
        repository.Add(new UserAddress { UserId = userId, VillageId = village.VillageId, Province = village.ProvinceName, City = village.RegencyName, District = village.DistrictName, AddressLine = request.AddressDetail.Trim(), PostalCode = request.PostalCode });
        state.User.ProfileStep = "documents";
        await repository.SaveAsync(ct);
        await transaction.CommitAsync(ct);
        return await StatusAsync(userId, ct);
    }
    public async Task<ProfileStatusDto> DocumentsAsync(Guid userId, string type, Stream file, long length, string contentType, CancellationToken ct)
    {
        if (!documents.Rules.DocumentTypes.Contains(type)) throw new ProfileException("Jenis dokumen tidak didukung.");
        await using var transaction = await repository.LockAsync(userId, ct);
        var state = await repository.GetAsync(userId, ct);
        RequireStep(state, "documents");
        await using var normalized = await documents.ValidateAndNormalizeAsync(file, length, contentType, ct);
        var key = await storage.StoreAsync(normalized, ct);
        try
        {
            repository.Add(new UserDocument { UserId = userId, DocumentType = type, StorageKey = key, VerificationStatus = "Pending" });
            state.User.ProfileCompleted = true;
            state.User.ProfileStep = "done";
            await repository.SaveAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(key, CancellationToken.None);
            throw;
        }
        return await StatusAsync(userId, ct);
    }
}
