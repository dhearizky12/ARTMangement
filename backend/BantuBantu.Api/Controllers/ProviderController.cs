using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BantuBantu.Api.Controllers;

[ApiController, Route("api/provider"), Authorize(Roles = "Provider")]
public class ProviderController(ProviderService providers, OrderService orders) : ControllerBase
{
    [HttpGet("profile")]
    public Task<ProviderDto> Profile(CancellationToken ct) => providers.OwnProfile(ct);

    [HttpPut("availability")]
    public Task<ProviderDto> Availability(AvailabilityUpdateRequest request, CancellationToken ct) => providers.UpdateOwnAvailability(request, ct);

    [HttpGet("orders")]
    public Task<OrderDto[]> Orders(CancellationToken ct) => orders.Orders(ct);
}
