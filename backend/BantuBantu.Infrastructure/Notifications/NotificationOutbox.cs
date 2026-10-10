using System.Text.Json;
using BantuBantu.Application;
using BantuBantu.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BantuBantu.Infrastructure.Notifications;

/// <summary>Scoped outbox sharing the request's DbContext: staged rows commit
/// in the caller's SaveAsync (same transaction), so a rolled-back state
/// change sends nothing.</summary>
public class NotificationOutbox(AppDbContext db, NotificationSettings settings, INotificationRenderer renderer, IEmailSender sender, ILogger<NotificationOutbox> logger) : INotificationOutbox
{
    private readonly List<EmailOutbox> staged = [];
    public async Task<EmailOutbox?> EnqueueAsync(string eventType, string to, Guid? userId, string dedupeKey, string templateKey, object payload, CancellationToken ct)
    {
        if (!settings.Enabled) return null;
        var address = to.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(address) || !address.Contains('@'))
        {
            logger.LogWarning("Notification skipped: recipient address missing for event {EventType} template {Template}.", eventType, templateKey);
            return null;
        }
        if (await db.EmailOutboxes.AnyAsync(x => x.DedupeKey == dedupeKey, ct)) return null;
        if (staged.Any(x => x.DedupeKey == dedupeKey)) return null;
        var hourAgo = DateTimeOffset.UtcNow.AddHours(-1);
        var sentThisHour = await db.EmailOutboxes.CountAsync(x => x.To == address && x.EventType == eventType && x.CreatedAt > hourAgo && x.Status != EmailOutboxStatus.Suppressed, ct)
            + staged.Count(x => x.To == address && x.EventType == eventType);
        var payloadJson = NotificationJson.Serialize(payload);
        var row = new EmailOutbox { EventType = eventType, To = address, UserId = userId, DedupeKey = dedupeKey, TemplateKey = templateKey, PayloadJson = payloadJson };
        if (sentThisHour >= settings.MaxPerRecipientPerHour)
        {
            row.Status = EmailOutboxStatus.Suppressed;
            db.EmailOutboxes.Add(row);
            logger.LogWarning("Notification suppressed by hourly cap ({Cap}/h): event {EventType} to {To}.", settings.MaxPerRecipientPerHour, eventType, address);
            return row;
        }
        db.EmailOutboxes.Add(row);
        staged.Add(row);
        return row;
    }
    /// <summary>One best-effort immediate send per staged row, right after
    /// commit. Failures are swallowed: the row stays Pending and the poller
    /// retries. Never throws.</summary>
    public async Task DispatchEnqueuedAsync(CancellationToken ct)
    {
        foreach (var row in staged.ToArray())
        {
            if (row.Status != EmailOutboxStatus.Pending) continue;
            try
            {
                var rendered = renderer.Render(row.TemplateKey, JsonDocument.Parse(row.PayloadJson).RootElement);
                await sender.SendAsync(new EmailMessage(row.To, rendered.Subject, rendered.Html, rendered.Text, row.DedupeKey), ct);
                row.Status = EmailOutboxStatus.Sent;
                row.SentAt = DateTimeOffset.UtcNow;
                staged.Remove(row);
            }
            catch (Exception ex)
            {
                row.Attempts++;
                row.NextAttemptAt = DateTimeOffset.UtcNow + EmailOutboxWorker.RetryDelay(row.Attempts);
                row.LastError = EmailOutboxWorker.SanitizeError(ex);
                logger.LogWarning(ex, "Opportunistic notification send failed, left Pending: event {EventType} to {To} attempt {Attempts}.", row.EventType, row.To, row.Attempts);
            }
        }
        if (staged.Count == 0) return;
        try { await db.SaveChangesAsync(ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to persist opportunistic notification outcomes; poller will retry."); }
    }
}
