using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace BantuBantu.Api.Controllers;

[ApiController, Route("api")]
public class DashboardController : ControllerBase
{
    [Authorize(Roles = "User"), HttpGet("user/dashboard")]
    public IActionResult UserDashboard() => Ok(new { message = "Login pengguna berhasil. Area pengguna terlindungi role User." });
    [Authorize(Roles = "Admin"), HttpGet("admin/dashboard")]
    public IActionResult AdminDashboard() => Ok(new { message = "Login administrator berhasil. Area admin terlindungi role Admin." });
}
