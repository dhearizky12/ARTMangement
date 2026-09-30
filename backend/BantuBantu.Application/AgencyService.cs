using BantuBantu.Domain;
namespace BantuBantu.Application;

public class AgencyService(IMarketplaceRepository repo, ICurrentActor current, IAuthRepository auth, IPasswordService passwords) : ApplicationService(current)
{
    public Task<List<Agency>> Agencies(CancellationToken ct) { Platform(); return repo.AgenciesAsync(ct); }
    public async Task<Agency> CreateAgency(AgencyRequest r, CancellationToken ct)
    {
        var actor = Platform();
        var agency = new Agency { Name = r.Name.Trim(), ContactInfo = r.ContactInfo.Trim(), Status = AgencyStatus.Approved };
        repo.AddAgency(agency);
        repo.Audit(actor, "agency.create", "Agency", agency.Id, detail: agency.Name);
        await repo.SaveAsync(ct);
        return agency;
    }
    public async Task<Agency> SetAgencyStatus(Guid id, AgencyStatusRequest r, CancellationToken ct)
    {
        var actor = Platform();
        if (r.Status is not (AgencyStatus.Approved or AgencyStatus.Suspended)) throw new ProfileException("Status agency tidak valid.");
        var agency = await repo.AgencyAsync(id, ct) ?? throw new ProfileException("Agency tidak ditemukan.", 404);
        agency.Status = r.Status;
        repo.Audit(actor, r.Status == AgencyStatus.Suspended ? "agency.suspend" : "agency.reactivate", "Agency", id, detail: agency.Name);
        await repo.SaveAsync(ct);
        return agency;
    }
    public Task<Agency> Suspend(Guid id, CancellationToken ct) => SetAgencyStatus(id, new AgencyStatusRequest(AgencyStatus.Suspended), ct);
    public Task<Agency> Reactivate(Guid id, CancellationToken ct) => SetAgencyStatus(id, new AgencyStatusRequest(AgencyStatus.Approved), ct);
    public async Task<UserDto> CreateAdmin(AdminAccountRequest r, CancellationToken ct)
    {
        var actor = Platform();
        if ((await repo.AgencyAsync(r.AgencyId, ct))?.Status != BantuBantu.Domain.AgencyStatus.Approved) throw new ProfileException("Agency belum disetujui.");
        var email = r.Email.Trim().ToLowerInvariant();
        if (await auth.EmailExistsAsync(email, ct)) throw new ProfileException("Email sudah terdaftar.", 409);
        var admin = new AdminAccount { Email = email, FullName = r.FullName.Trim(), Role = UserRole.AgencyAdmin, AgencyId = r.AgencyId };
        admin.PasswordHash = passwords.Hash(admin, r.Password);
        auth.AddUser(admin);
        repo.Audit(actor, "agency-admin.create", "AdminAccount", admin.Id, detail: $"{admin.Email}; agency={r.AgencyId}");
        await auth.SaveAsync(ct);
        return UserDto.From(admin);
    }
}
