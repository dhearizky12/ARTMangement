using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BantuBantu.Application;

namespace BantuBantu.Infrastructure.Notifications;

/// <summary>Sends every template with sample data to one address through a
/// real <see cref="IEmailSender"/>. Development-only, requires explicit
/// confirmation, and refuses invalid addresses. Used to eyeball templates in
/// a real mailbox (e.g. Gmail) via the Resend sandbox before enabling mail.</summary>
public class MailPreviewSender(INotificationRenderer renderer)
{
    public async Task<IReadOnlyList<string>> SendAllAsync(IEmailSender sender, string to, bool confirmed, bool isDevelopment, CancellationToken ct)
    {
        if (!isDevelopment) throw new InvalidOperationException("--send-preview is Development-only.");
        if (!confirmed) throw new InvalidOperationException("--send-preview requires --confirm with the recipient address.");
        var address = to.Trim().ToLowerInvariant();
        if (!new EmailAddressAttribute().IsValid(address)) throw new InvalidOperationException("--send-preview needs a valid email address.");
        var sent = new List<string>();
        foreach (var (templateKey, payloadJson) in renderer.PreviewSamples())
        {
            using var document = JsonDocument.Parse(payloadJson);
            var rendered = renderer.Render(templateKey, document.RootElement);
            await sender.SendAsync(new EmailMessage(address, rendered.Subject, rendered.Html, rendered.Text), ct);
            sent.Add($"{templateKey}: {rendered.Subject}");
        }
        return sent;
    }
}
