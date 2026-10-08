using BantuBantu.Application;
using Microsoft.Extensions.Logging;

namespace BantuBantu.Infrastructure;

/// <summary>
/// Development sender: logs instead of sending, so local work never needs a
/// Resend key and can never reach a real recipient.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        logger.LogWarning("Development email sender: message NOT delivered. To={To} Subject={Subject} HtmlLength={HtmlLength}", message.To, message.Subject, message.Html.Length);
        return Task.CompletedTask;
    }
}
