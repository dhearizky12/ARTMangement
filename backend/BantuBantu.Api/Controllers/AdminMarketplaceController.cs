using BantuBantu.Application;
using BantuBantu.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace BantuBantu.Api.Controllers;

[ApiController, Route("api/admin"), Authorize(Roles = "PlatformAdmin,AgencyAdmin")]
public class AdminMarketplaceController(ProviderService providers, AgencyService agencies, CatalogService catalog, OrderService orders) : ControllerBase
{
    [HttpGet("providers")] public Task<ProviderAdminDto[]> Providers(CancellationToken ct) => providers.Roster(ct);
    [HttpPost("providers")] public Task<ProviderAdminDto> Draft(DraftRequest request, CancellationToken ct) => providers.Draft(request, ct);
    [HttpGet("providers/{id:guid}")] public Task<ProviderAdminDto> Provider(Guid id, CancellationToken ct) => providers.AdminDetail(id, ct);
    [HttpPost("providers/{id:guid}/personal-info")] public Task<ProviderAdminDto> Personal(Guid id, ProviderPersonalRequest request, CancellationToken ct) => providers.Personal(id, request, ct);
    [HttpPost("providers/{id:guid}/address")] public Task<ProviderAdminDto> Address(Guid id, AddressRequest request, CancellationToken ct) => providers.Address(id, request, ct);
    [HttpPost("providers/{id:guid}/profile")] public Task<ProviderAdminDto> Profile(Guid id, ProviderProfileRequest request, CancellationToken ct) => providers.Profile(id, request, ct);
    [HttpPost("providers/{id:guid}/verify")] public Task<ProviderAdminDto> Verify(Guid id, VerifyRequest request, CancellationToken ct) => providers.Verify(id, request, ct);
    [HttpPost("providers/{id:guid}/documents"), EnableRateLimiting("upload"), RequestSizeLimit(6 * 1024 * 1024)]
    public Task<ProviderAdminDto> Document(Guid id, [FromForm] string documentType, [FromForm] IFormFile file, CancellationToken ct) => Upload(id, documentType, file, ct);
    private async Task<ProviderAdminDto> Upload(Guid id, string type, IFormFile file, CancellationToken ct) { await using var stream = file.OpenReadStream(); return await providers.Document(id, type, stream, file.Length, file.ContentType, ct); }
    [HttpGet("providers/{id:guid}/documents/{documentId:guid}")] public async Task<IActionResult> Document(Guid id, Guid documentId, CancellationToken ct) { var file = await providers.DocumentDownload(id, documentId, ct); Response.Headers.CacheControl = "no-store"; return File(file.Content, "image/jpeg", file.Name); }
    [HttpGet("orders")] public Task<OrderDto[]> Orders(CancellationToken ct) => orders.Orders(ct);
    [HttpPost("orders/{id:guid}/status")] public Task<OrderDto> OrderStatus(Guid id, OrderStatusRequest request, CancellationToken ct) => orders.ChangeOrder(id, request, ct);
    [Authorize(Roles = "PlatformAdmin"), HttpGet("agencies")] public Task<List<Agency>> Agencies(CancellationToken ct) => agencies.Agencies(ct);
    [Authorize(Roles = "PlatformAdmin"), HttpPost("agencies")] public Task<Agency> Agency(AgencyRequest request, CancellationToken ct) => agencies.CreateAgency(request, ct);
    [Authorize(Roles = "PlatformAdmin"), HttpPost("agencies/{id:guid}/status")] public Task<Agency> AgencyStatus(Guid id, AgencyStatusRequest request, CancellationToken ct) => agencies.SetAgencyStatus(id, request, ct);
    [Authorize(Roles = "PlatformAdmin"), HttpPost("accounts")] public Task<UserDto> Account(AdminAccountRequest request, CancellationToken ct) => agencies.CreateAdmin(request, ct);
    [Authorize(Roles = "PlatformAdmin"), HttpGet("categories")] public Task<List<ServiceCategory>> Categories(CancellationToken ct) => catalog.Categories(ct);
    [Authorize(Roles = "PlatformAdmin"), HttpPost("categories")] public Task<ServiceCategory> Category(CategoryRequest request, CancellationToken ct) => catalog.Category(null, request, ct);
    [Authorize(Roles = "PlatformAdmin"), HttpPut("categories/{id:guid}")] public Task<ServiceCategory> Category(Guid id, CategoryRequest request, CancellationToken ct) => catalog.Category(id, request, ct);
    [Authorize(Roles = "PlatformAdmin"), HttpDelete("categories/{id:guid}")] public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct) { await catalog.DeleteCategory(id, ct); return NoContent(); }
    [Authorize(Roles = "PlatformAdmin"), HttpPut("content/{id}")] public Task<ContentBlock> Content(string id, ContentRequest request, CancellationToken ct) => catalog.Content(id, request, ct);
    [Authorize(Roles = "PlatformAdmin"), HttpGet("audit")] public Task<List<AuditEntry>> Audit(CancellationToken ct) => catalog.Audit(ct);
}
