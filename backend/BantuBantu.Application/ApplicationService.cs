using BantuBantu.Domain;
namespace BantuBantu.Application;

public abstract class ApplicationService(ICurrentActor current)
{
    protected Actor Caller() => current.Get();
    protected Actor Admin() { var a = current.Get(); if (a.Role is not (UserRole.PlatformAdmin or UserRole.AgencyAdmin)) throw new ProfileException("Akses admin diperlukan.", 403); return a; }
    protected Actor Platform() { var a = Admin(); if (a.Role != UserRole.PlatformAdmin) throw new ProfileException("Hanya Platform Admin.", 403); return a; }
    protected Actor Customer() { var a = current.Get(); if (a.Role != UserRole.Customer) throw new ProfileException("Masuk sebagai Customer untuk memesan.", 403); return a; }
}
