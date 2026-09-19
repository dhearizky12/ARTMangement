using BantuBantu.Application;
using BantuBantu.Domain;
namespace BantuBantu.Api;

public class CurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public Actor Get()
    {
        var user = accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true || !Guid.TryParse(user.FindFirst("sub")?.Value, out var id) || !Enum.TryParse<UserRole>(user.FindFirst("role")?.Value, out var role)) throw new AuthenticationFailedException();
        return new(id, role, Guid.TryParse(user.FindFirst("agencyId")?.Value, out var agency) ? agency : null);
    }
}
