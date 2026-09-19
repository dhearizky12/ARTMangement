using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace BantuBantu.Api.Controllers;

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public class AuthController(IAuthService auth) : ControllerBase
{
    private ActionResult<AuthResponse> Respond(AuthResult result)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(result.Response);
    }
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(GoogleRequest request, CancellationToken ct) => Respond(await auth.GoogleAsync(request.Credential, ct));
    [HttpPost("admin/login")]
    public async Task<ActionResult<AuthResponse>> Admin(AdminRequest request, CancellationToken ct) => Respond(await auth.AdminAsync(request.Email, request.Password, ct));
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct) => Respond(await auth.RefreshAsync(request.RefreshToken, ct));
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct) { await auth.LogoutAsync(request.RefreshToken, ct); return NoContent(); }
    [Authorize, HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var id)) return Unauthorized();
        return Ok(await auth.MeAsync(id, ct));
    }
}
