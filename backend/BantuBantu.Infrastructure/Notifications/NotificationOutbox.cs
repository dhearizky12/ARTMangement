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
    /// commit. Recipients are attempted concurrently under a short total
    /// budget (<c>Notifications:OpportunisticTimeoutSeconds</c>) so a slow
    /// mail provider cannot hold the request open. Anything unsent — timeout
    /// or error — stays Pending for the poller; a timeout never fails a row
    /// and never consumes an attempt. Never throws.</summary>
    public async Task DispatchEnqueuedAsync(CancellationToken ct)
    {
        var pending = staged.Where(row => row.Status == EmailOutboxStatus.Pending).ToArray();
        if (pending.Length == 0) return;
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(settings.OpportunisticTimeoutSeconds));
        var outcomes = await Task.WhenAll(pending.Select(row => TryDispatchAsync(row, budget.Token)));
        var sent = 0;
        var left = 0;
        foreach (var (row, outcome) in pending.Zip(outcomes))
        {
            if (outcome.Sent)
            {
                row.Status = EmailOutboxStatus.Sent;
                row.SentAt = DateTimeOffset.UtcNow;
                staged.Remove(row);
                sent++;
                continue;
            }
            left++;
            if (outcome.TimedOut) row.NextAttemptAt = DateTimeOffset.UtcNow;
            else
            {
                row.Attempts++;
                row.NextAttemptAt = DateTimeOffset.UtcNow + EmailOutboxWorker.RetryDelay(row.Attempts);
                row.LastError = outcome.Error;
            }
        }
        if (left > 0)
            logger.LogInformation("Opportunistic notification dispatch sent {Sent} immediately, {Left} left Pending for the poller.", sent, left);
        if (staged.Count == 0) return;
        try { await db.SaveChangesAsync(ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to persist opportunistic notification outcomes; poller will retry."); }
    }
    private async Task<(bool Sent, bool TimedOut, string? Error)> TryDispatchAsync(EmailOutbox row, CancellationToken ct)
    {
        RenderedEmail rendered;
        try
        {
            using var document = JsonDocument.Parse(row.PayloadJson);
            rendered = renderer.Render(row.TemplateKey, document.RootElement);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Opportunistic notification render failed, left Pending: event {EventType} to {To}.", row.EventType, row.To);
            return (false, false, EmailOutboxWorker.SanitizeError(ex));
        }
        try
        {
            await sender.SendAsync(new EmailMessage(row.To, rendered.Subject, rendered.Html, rendered.Text, row.DedupeKey), ct);
            return (true, false, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return (false, true, null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Opportunistic notification send failed, left Pending: event {EventType} to {To}.", row.EventType, row.To);
            return (false, false, EmailOutboxWorker.SanitizeError(ex));
        }
    }
}
