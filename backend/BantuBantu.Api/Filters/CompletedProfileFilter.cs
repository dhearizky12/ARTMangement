using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
namespace BantuBantu.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowIncompleteProfileAttribute : Attribute { }
public class CompletedProfileFilter(IAuthRepository users) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var principal = context.HttpContext.User;
        if (principal.Identity?.IsAuthenticated != true || !principal.IsInRole("User") ||
            endpoint?.Metadata.GetMetadata<AllowIncompleteProfileAttribute>() is not null ||
            endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        { await next(); return; }
        if (!Guid.TryParse(principal.FindFirst("sub")?.Value, out var userId)) { context.Result = new UnauthorizedResult(); return; }
        var user = await users.FindUserAsync(userId, context.HttpContext.RequestAborted);
        if (user is null) { context.Result = new UnauthorizedResult(); return; }
        if (!user.ProfileCompleted)
        {
            context.Result = new ObjectResult(new { title = "Lengkapi profil untuk melanjutkan.", status = 403, code = "PROFILE_INCOMPLETE", profileStep = user.ProfileStep }) { StatusCode = 403 };
            return;
        }
        await next();
    }
}
