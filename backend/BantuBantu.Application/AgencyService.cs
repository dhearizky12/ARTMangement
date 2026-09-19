using BantuBantu.Domain;
namespace BantuBantu.Application;

public class AgencyService(IMarketplaceRepository repo, ICurrentActor current, IAuthRepository auth, IPasswordService passwords) : ApplicationService(current)
{
    public Task<List<Agency>> Agencies(CancellationToken ct) { Platform(); return repo.AgenciesAsync(ct); }
    public async Task<Agency> CreateAgency(AgencyRequest r, CancellationToken ct) { var a = Platform(); var agency = new Agency { Name = r.Name.Trim(), ContactInfo = r.ContactInfo.Trim() }; repo.AddAgency(agency); repo.Audit(a, null, "agency.create", agency.Id.ToString()); await repo.SaveAsync(ct); return agency; }
    public async Task<Agency> SetAgencyStatus(Guid id, AgencyStatusRequest r, CancellationToken ct) { var a = Platform(); var agency = await repo.AgencyAsync(id, ct) ?? throw new ProfileException("Agency tidak ditemukan.", 404); agency.Status = r.Status; repo.Audit(a, null, "agency.status", $"{id}: {r.Status}"); await repo.SaveAsync(ct); return agency; }
    public async Task<UserDto> CreateAdmin(AdminAccountRequest r, CancellationToken ct) { Platform(); if ((await repo.AgencyAsync(r.AgencyId, ct))?.Status != BantuBantu.Domain.AgencyStatus.Approved) throw new ProfileException("Agency belum disetujui."); var email = r.Email.Trim().ToLowerInvariant(); if (await auth.EmailExistsAsync(email, ct)) throw new ProfileException("Email sudah terdaftar.", 409); var admin = new AdminAccount { Email = email, FullName = r.FullName.Trim(), Role = UserRole.AgencyAdmin, AgencyId = r.AgencyId }; admin.PasswordHash = passwords.Hash(admin, r.Password); auth.AddUser(admin); await auth.SaveAsync(ct); return UserDto.From(admin); }
}
