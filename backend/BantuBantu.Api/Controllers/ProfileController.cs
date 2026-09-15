using System.ComponentModel.DataAnnotations;
using BantuBantu.Api.Filters;
using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace BantuBantu.Api.Controllers;

public class DocumentForm
{
    [Required, RegularExpression("^(KTP|Passport)$")]
    public string DocumentType { get; set; } = "";
    [Required]
    public IFormFile File { get; set; } = null!;
}
[ApiController, Route("api/profile"), Authorize(Roles = "User"), AllowIncompleteProfile]
public class ProfileController(IProfileService profiles) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : throw new AuthenticationFailedException();
    [HttpGet("status")]
    public async Task<ProfileStatusDto> Status(CancellationToken ct) { Response.Headers.CacheControl = "no-store"; return await profiles.StatusAsync(UserId, ct); }
    [HttpPost("personal-info")]
    public Task<ProfileStatusDto> Personal(PersonalInfoRequest request, CancellationToken ct) => profiles.PersonalAsync(UserId, request, ct);
    [HttpPost("address")]
    public Task<ProfileStatusDto> Address(AddressRequest request, CancellationToken ct) => profiles.AddressAsync(UserId, request, ct);
    [HttpPost("documents"), EnableRateLimiting("upload"), RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 6 * 1024 * 1024)]
    public async Task<ProfileStatusDto> Documents([FromForm] DocumentForm request, CancellationToken ct)
    {
        await using var stream = request.File.OpenReadStream();
        return await profiles.DocumentsAsync(UserId, request.DocumentType, stream, request.File.Length, request.File.ContentType, ct);
    }
}
