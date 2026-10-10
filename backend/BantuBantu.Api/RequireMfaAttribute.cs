using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BantuBantu.Api;

/// <summary>
/// Gates an endpoint behind a verified two-step session. Only "limited"
/// sessions - the provider password fallback that skipped the email code - are
/// refused here (403 mfa_required, for step-up). Full sessions carry amr, and
/// sessions with no 2FA claims at all (<see cref="BantuBantu.Domain.SessionAccess"/>.
/// None) are already rejected as 401 by middleware while 2FA is enabled.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireMfaAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated == true &&
            context.HttpContext.User.HasClaim("limited", "true"))
        {
            context.Result = new ObjectResult(new
            {
                title = "Verifikasi dua langkah diperlukan.",
                status = 403,
                code = "mfa_required",
                traceId = context.HttpContext.TraceIdentifier
            })
            { StatusCode = 403 };
        }
    }
}