using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
namespace BantuBantu.Api.Controllers;

[ApiController, Route("api")]
public class MarketplaceController(ProviderService providers, CatalogService catalog, OrderService orders) : ControllerBase
{
    [AllowAnonymous, HttpGet("providers")]
    public Task<ProviderPage> Browse(CancellationToken ct, [FromQuery, StringLength(100)] string? q = null, [FromQuery] Guid? category = null, [FromQuery, RegularExpression("^[0-9]{10}$")] string? villageId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 12) => providers.Browse(q, category, villageId, page, pageSize, ct);
    [AllowAnonymous, HttpGet("providers/{id:guid}")] public Task<ProviderDto> Detail(Guid id, CancellationToken ct) => providers.Detail(id, ct);
    [AllowAnonymous, HttpGet("content")] public Task<List<BantuBantu.Domain.ContentBlock>> Content(CancellationToken ct) => catalog.Content(ct);
    [Authorize(Roles = "Customer"), HttpGet("orders")] public Task<OrderDto[]> Orders(CancellationToken ct) => orders.Orders(ct);
    [Authorize(Roles = "Customer"), HttpPost("orders")] public Task<OrderDto> Book(BookingRequest request, CancellationToken ct) => orders.Book(request, ct);
    [Authorize(Roles = "Customer"), HttpPost("orders/{id:guid}/review")] public Task<ReviewDto> Review(Guid id, ReviewRequest request, CancellationToken ct) => orders.Review(id, request, ct);
}
