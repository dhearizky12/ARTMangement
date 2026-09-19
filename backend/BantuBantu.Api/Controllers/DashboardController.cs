using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BantuBantu.Api.Controllers;

[ApiController, Route("api")]
public class DashboardController : ControllerBase
{
    [Authorize(Roles = "Customer"), HttpGet("user/dashboard")]
    public IActionResult UserDashboard() => Ok(new { message = "Login pengguna berhasil. Area pengguna terlindungi role User." });
    [Authorize(Roles = "PlatformAdmin,AgencyAdmin"), HttpGet("admin/dashboard")]
    public IActionResult AdminDashboard() => Ok(new { message = "Login administrator berhasil. Area admin terlindungi role Admin." });
}
