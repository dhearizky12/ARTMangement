using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace BantuBantu.Api.Controllers;

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    private ActionResult<AuthResponse> Respond(AuthOutcome outcome)
    {
        Response.Headers.CacheControl = "no-store";
        return outcome switch
        {
            SessionOutcome s => Ok(s.Response),
            ChallengeOutcome c => Ok(c.Challenge),
            _ => throw new InvalidOperationException("Unknown auth outcome.")
        };
    }
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(GoogleRequest request, CancellationToken ct) => Respond(await auth.GoogleAsync(request.Credential, ct));
    [HttpPost("admin/login")]
    public async Task<ActionResult<AuthResponse>> Admin(AdminRequest request, CancellationToken ct) => Respond(await auth.AdminAsync(request.Email, request.Password, ct));
    [HttpPost("provider/register")]
    public async Task<ActionResult<AuthResponse>> RegisterProvider(ProviderRegistrationRequest request, CancellationToken ct) => Respond(await auth.RegisterProviderAsync(request.Email, request.Password, request.ConfirmPassword, ct));
    [HttpPost("provider/login")]
    public async Task<ActionResult<AuthResponse>> Provider(ProviderLoginRequest request, CancellationToken ct) => Respond(await auth.ProviderAsync(request.Email, request.Password, ct));
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct) => Respond(await auth.RefreshAsync(request.RefreshToken, ct));
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct) { await auth.LogoutAsync(request.RefreshToken, ct); return NoContent(); }
    [HttpPost("2fa/verify"), EnableRateLimiting("2fa")]
    public async Task<ActionResult<AuthResponse>> VerifyTwoFactor(TwoFactorVerifyRequest request, CancellationToken ct) => Respond(await auth.VerifyTwoFactorCodeAsync(request.ChallengeId, request.Code, ct));
    [HttpPost("2fa/resend"), EnableRateLimiting("2fa")]
    public async Task<IActionResult> ResendTwoFactor(TwoFactorResendRequest request, CancellationToken ct) { await auth.ResendTwoFactorCodeAsync(request.ChallengeId, ct); return NoContent(); }
    [HttpPost("2fa/skip"), EnableRateLimiting("2fa")]
    public async Task<ActionResult<AuthResponse>> SkipTwoFactor(TwoFactorSkipRequest request, CancellationToken ct) => Respond(await auth.SkipTwoFactorAsync(request.ChallengeId, ct));
    [Authorize(Roles = "Provider"), RequireMfa, HttpPost("provider/change-password")]
    public async Task<IActionResult> ChangeProviderPassword(ChangePasswordRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("providerId")?.Value, out var providerId)) return Unauthorized();
        await auth.ChangeProviderPasswordAsync(providerId, request.CurrentPassword, request.NewPassword, ct);
        return NoContent();
    }
    [Authorize, HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var id)) return Unauthorized();
        return Ok(await auth.MeAsync(id, ct));
    }
}