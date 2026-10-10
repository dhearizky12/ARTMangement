using System.Text.Json;
using BantuBantu.Application;
using BantuBantu.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BantuBantu.Infrastructure.Notifications;

/// <summary>Drains the email outbox. Single instance per deployment: rows are
/// claimed by (Status=Pending, NextAttemptAt due), oldest first, in small
/// batches so Resend rate limits are never stressed.
/// Backoff: 15min * 2^(attempt-1), capped at 12h; with the default 6 attempts
/// the last try lands ~16h after the event ("six over a day").</summary>
public class EmailOutboxWorker(IServiceScopeFactory scopes, ILogger<EmailOutboxWorker> logger) : BackgroundService
{
    private const int BatchSize = 25;
    public static TimeSpan RetryDelay(int attemptAfterIncrement) =>
        TimeSpan.FromMinutes(Math.Min(15 * Math.Pow(2, attemptAfterIncrement - 1), 720));
    public static bool IsPermanent(Exception ex) =>
        ex is PermanentEmailException ||
        (ex is ProfileException profile && profile is not TransientEmailException &&
         profile.Code is "EMAIL_CONFIG_MISSING");
    public static string SanitizeError(Exception ex)
    {
        var message = ex is ProfileException profile ? $"{profile.Code}: {profile.Message}" : ex.GetType().Name + ": " + ex.Message;
        message = message.Replace('\r', ' ').Replace('\n', ' ');
        return message.Length > 300 ? message[..300] : message;
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            int pollSeconds = 30;
            try
            {
                using var scope = scopes.CreateScope();
                var settings = scope.ServiceProvider.GetRequiredService<NotificationSettings>();
                pollSeconds = settings.OutboxPollSeconds;
                if (settings.Enabled)
                    await ProcessBatchAsync(scope.ServiceProvider, settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Email outbox poll failed; retrying.");
            }
            try { await Task.Delay(TimeSpan.FromSeconds(pollSeconds), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
    public static async Task<int> ProcessBatchAsync(IServiceProvider services, NotificationSettings settings, CancellationToken ct)
    {
        if (!settings.Enabled) return 0;
        var db = services.GetRequiredService<AppDbContext>();
        var sender = services.GetRequiredService<IEmailSender>();
        var renderer = services.GetRequiredService<INotificationRenderer>();
        var log = services.GetRequiredService<ILogger<EmailOutboxWorker>>();
        var now = DateTimeOffset.UtcNow;
        var batch = await db.EmailOutboxes
            .Where(x => x.Status == EmailOutboxStatus.Pending && x.NextAttemptAt <= now)
            .OrderBy(x => x.CreatedAt).Take(BatchSize).ToListAsync(ct);
        foreach (var row in batch)
        {
            try
            {
                var rendered = renderer.Render(row.TemplateKey, JsonDocument.Parse(row.PayloadJson).RootElement);
                await sender.SendAsync(new EmailMessage(row.To, rendered.Subject, rendered.Html, rendered.Text, row.DedupeKey), ct);
                row.Status = EmailOutboxStatus.Sent;
                row.SentAt = DateTimeOffset.UtcNow;
                row.LastError = null;
            }
            catch (Exception ex) when (IsPermanent(ex))
            {
                row.Status = EmailOutboxStatus.Failed;
                row.LastError = SanitizeError(ex);
                log.LogWarning("Notification permanently failed, not retried: event {EventType} to {To} template {Template}: {Error}.", row.EventType, row.To, row.TemplateKey, row.LastError);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                row.Attempts++;
                if (row.Attempts >= settings.MaxAttempts)
                {
                    row.Status = EmailOutboxStatus.Failed;
                    row.LastError = SanitizeError(ex);
                    log.LogWarning("Notification exhausted {Attempts} attempts: event {EventType} to {To}: {Error}.", row.Attempts, row.EventType, row.To, row.LastError);
                }
                else
                {
                    row.NextAttemptAt = DateTimeOffset.UtcNow + RetryDelay(row.Attempts);
                    row.LastError = SanitizeError(ex);
                }
            }
        }
        if (batch.Count > 0) await db.SaveChangesAsync(ct);
        return batch.Count;
    }
}
