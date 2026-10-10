using BantuBantu.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BantuBantu.Infrastructure;

/// <summary>
/// Development sender: logs subject + plain text instead of sending, so local
/// work never needs a Resend key and can never reach a real recipient. Also
/// writes the rendered HTML to App_Data/mail-preview for visual review.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger, IConfiguration? configuration = null) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogWarning("Development email sender: message NOT delivered. To={To} Subject={Subject} Text={Text} HtmlLength={HtmlLength}",
            message.To, message.Subject, message.Text, message.Html.Length);
        try
        {
            var dir = configuration?["Email:PreviewDir"];
            if (string.IsNullOrWhiteSpace(dir)) dir = Path.Combine("App_Data", "mail-preview");
            Directory.CreateDirectory(dir);
            var safe = new string(message.To.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
            if (safe.Length > 60) safe = safe[..60];
            var path = Path.Combine(dir, $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{safe}.html");
            File.WriteAllText(path, message.Html);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Development email sender: could not write HTML preview.");
        }
        return Task.CompletedTask;
    }
}
