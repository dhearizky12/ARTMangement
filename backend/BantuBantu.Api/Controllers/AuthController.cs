using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace BantuBantu.Api.Controllers;

[ApiController, Route("api/auth"), EnableRateLimiting("auth")]
public class AuthController(IAuthService auth, IConfiguration config) : ControllerBase
{
    private const string CookieName = "bantubantu_refresh";
    private CookieOptions Cookie(DateTimeOffset? expiry = null) => new() { HttpOnly = true, Secure = bool.Parse(config["Auth:CookieSecure"]!), SameSite = Enum.Parse<SameSiteMode>(config["Auth:CookieSameSite"]!, true), Path = "/api/auth", Expires = expiry, IsEssential = true };
    private ActionResult<AuthResponse> Respond(AuthResult result)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Cookies.Append(CookieName, result.RefreshToken, Cookie(result.RefreshExpiresAt));
        return Ok(result.Response);
    }
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google(GoogleRequest request, CancellationToken ct) => Respond(await auth.GoogleAsync(request.Credential, ct));
    [HttpPost("admin/login")]
    public async Task<ActionResult<AuthResponse>> Admin(AdminRequest request, CancellationToken ct) => Respond(await auth.AdminAsync(request.Email, request.Password, ct));
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(CancellationToken ct)
    {
        try { return Respond(await auth.RefreshAsync(Request.Cookies[CookieName] ?? throw new AuthenticationFailedException(), ct)); }
        catch (AuthenticationFailedException) { Response.Cookies.Delete(CookieName, Cookie()); throw; }
    }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (Request.Cookies.TryGetValue(CookieName, out var token)) await auth.LogoutAsync(token, ct);
        Response.Cookies.Delete(CookieName, Cookie()); return NoContent();
    }
    [Authorize, HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var id)) return Unauthorized();
        return Ok(await auth.MeAsync(id, ct));
    }
}
