using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BantuBantu.Api.Controllers;

[ApiController, Route("api/provider"), Authorize(Roles = "Provider")]
public class ProviderController(ProviderService providers, OrderService orders) : ControllerBase
{
    [HttpGet("profile")]
    public Task<ProviderDto> Profile(CancellationToken ct) => providers.OwnProfile(ct);

    [HttpGet("application/status")]
    public Task<ProviderApplicationDto> ApplicationStatus(CancellationToken ct) => providers.OwnApplication(ct);

    [HttpPost("application/submit")]
    public Task<ProviderApplicationDto> SubmitApplication(CancellationToken ct) => providers.SubmitApplication(ct);

    [HttpPost("personal-info")]
    public Task<ProviderAdminDto> Personal(ProviderPersonalRequest request, CancellationToken ct) => providers.OwnPersonal(request, ct);

    [HttpPost("address")]
    public Task<ProviderAdminDto> Address(AddressRequest request, CancellationToken ct) => providers.OwnAddress(request, ct);

    [HttpPost("profile")]
    public Task<ProviderAdminDto> Profile(ProviderProfileRequest request, CancellationToken ct) => providers.OwnProfile(request, ct);

    [HttpPost("documents"), EnableRateLimiting("upload"), RequestSizeLimit(6 * 1024 * 1024)]
    public Task<ProviderAdminDto> Document([FromForm] string documentType, [FromForm] IFormFile file, CancellationToken ct) => Upload(documentType, file, ct);

    private async Task<ProviderAdminDto> Upload(string type, IFormFile file, CancellationToken ct) { await using var stream = file.OpenReadStream(); return await providers.OwnDocument(type, stream, file.Length, file.ContentType, ct); }

    [HttpGet("documents/{documentId:guid}")]
    public async Task<IActionResult> Document(Guid documentId, CancellationToken ct) { var file = await providers.OwnDocumentDownload(documentId, ct); Response.Headers.CacheControl = "no-store"; return File(file.Content, "image/jpeg", file.Name); }

    [HttpPut("availability")]
    public Task<ProviderDto> Availability(AvailabilityUpdateRequest request, CancellationToken ct) => providers.UpdateOwnAvailability(request, ct);

    [HttpGet("orders")]
    public Task<OrderDto[]> Orders(CancellationToken ct) => orders.Orders(ct);
}
