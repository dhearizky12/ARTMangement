using System.ComponentModel.DataAnnotations;
using BantuBantu.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace BantuBantu.Api.Controllers;

[ApiController, Route("api/admin/email"), Authorize(Roles = "PlatformAdmin"), RequireMfa]
public class EmailAdminController(IEmailSender sender) : ControllerBase
{
    public record EmailTestRequest([Required, EmailAddress, MaxLength(256)] string To, [Required, StringLength(200, MinimumLength = 1)] string Subject);

    // Sends one real message so a newly provisioned Resend key and the verified
    // from-domain can be confirmed end to end (rate limited like auth endpoints).
    [HttpPost("test"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Test(EmailTestRequest request, CancellationToken ct)
    {
        await sender.SendAsync(new EmailMessage(request.To, request.Subject, "<p>Email tes konfigurasi pengiriman Bantu-Bantu.</p>"), ct);
        return Accepted(new { status = "sent", to = request.To });
    }
}
