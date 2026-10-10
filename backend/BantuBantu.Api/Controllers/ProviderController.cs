using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BantuBantu.Api.Controllers;

[ApiController, Route("api/provider"), Authorize(Roles = "Provider")]
public class ProviderController(ProviderService providers, OrderService orders, IAuthService auth) : ControllerBase
{
    [HttpGet("profile")]
    public Task<ProviderDto> Profile(CancellationToken ct) => providers.OwnProfile(ct);

    [HttpGet("application/status")]
    public Task<ProviderApplicationDto> ApplicationStatus(CancellationToken ct) => providers.OwnApplication(ct);

    [RequireMfa, HttpPost("application/submit")]
    public Task<ProviderApplicationDto> SubmitApplication(CancellationToken ct) => providers.SubmitApplication(ct);

    [RequireMfa, HttpPost("personal-info")]
    public Task<ProviderAdminDto> Personal(ProviderPersonalRequest request, CancellationToken ct) => providers.OwnPersonal(request, ct);

    [RequireMfa, HttpPost("address")]
    public Task<ProviderAdminDto> Address(AddressRequest request, CancellationToken ct) => providers.OwnAddress(request, ct);

    [RequireMfa, HttpPost("profile")]
    public Task<ProviderAdminDto> Profile(ProviderProfileRequest request, CancellationToken ct) => providers.OwnProfile(request, ct);

    [RequireMfa, HttpPost("documents"), EnableRateLimiting("upload"), RequestSizeLimit(6 * 1024 * 1024)]
    public Task<ProviderAdminDto> Document([FromForm] string documentType, [FromForm] IFormFile file, CancellationToken ct) => Upload(documentType, file, ct);

    private async Task<ProviderAdminDto> Upload(string type, IFormFile file, CancellationToken ct) { await using var stream = file.OpenReadStream(); return await providers.OwnDocument(type, stream, file.Length, file.ContentType, ct); }

    [RequireMfa, HttpGet("documents/{documentId:guid}")]
    public async Task<IActionResult> Document(Guid documentId, CancellationToken ct) { var file = await providers.OwnDocumentDownload(documentId, ct); Response.Headers.CacheControl = "no-store"; return File(file.Content, "image/jpeg", file.Name); }

    [HttpPut("availability")]
    public Task<ProviderDto> Availability(AvailabilityUpdateRequest request, CancellationToken ct) => providers.UpdateOwnAvailability(request, ct);

    [HttpGet("orders")]
    public Task<OrderDto[]> Orders(CancellationToken ct) => orders.Orders(ct);

    [HttpPost("step-up/start"), EnableRateLimiting("2fa")]
    public async Task<ActionResult<TwoFactorLoginChallenge>> StartStepUp(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("providerId")?.Value, out var providerId)) return Unauthorized();
        var email = User.FindFirst("email")?.Value ?? "";
        var jti = User.FindFirst("jti")?.Value ?? "";
        if (User.HasClaim("amr", "email_otp")) throw new ProfileException("Sesi sudah diverifikasi dua langkah.", 409, "2FA_ALREADY_VERIFIED");
        return Ok(await auth.StartProviderStepUpAsync(providerId, email, jti, ct));
    }
    [HttpPost("step-up/verify"), EnableRateLimiting("2fa")]
    public async Task<ActionResult<AuthResponse>> VerifyStepUp(TwoFactorStepUpVerifyRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("providerId")?.Value, out var providerId)) return Unauthorized();
        var jti = User.FindFirst("jti")?.Value ?? "";
        return Respond(await auth.VerifyProviderStepUpAsync(request.ChallengeId, request.Code, providerId, jti, request.RefreshToken, ct));
    }
    private ActionResult<AuthResponse> Respond(AuthOutcome outcome)
    {
        Response.Headers.CacheControl = "no-store";
        return outcome is SessionOutcome s ? Ok(s.Response) : throw new InvalidOperationException("Unknown auth outcome.");
    }
}