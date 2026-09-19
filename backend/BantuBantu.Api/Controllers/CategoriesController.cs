using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BantuBantu.Api.Controllers;

[ApiController, Route("api/service-categories"), AllowAnonymous]
public class CategoriesController(ICategoryService categories) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ServiceCategoryDto>> List(CancellationToken ct) => categories.ListAsync(ct);
}
