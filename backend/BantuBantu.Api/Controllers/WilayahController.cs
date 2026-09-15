using BantuBantu.Application;
using BantuBantu.Api.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
namespace BantuBantu.Api.Controllers;
[ApiController, Route("api/wilayah"), Authorize, AllowIncompleteProfile]
public class WilayahController(IWilayahRepository repository) : ControllerBase
{
    [HttpGet("search")]
    public Task<IReadOnlyList<VillageResult>> Search([FromQuery, Required, StringLength(100, MinimumLength = 2)] string q, CancellationToken ct, [FromQuery] int limit = 10)
        => repository.SearchAsync(q, limit, ct);
}
