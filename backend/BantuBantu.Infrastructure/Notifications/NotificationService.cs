using BantuBantu.Application;
using Microsoft.Extensions.Logging;

namespace BantuBantu.Infrastructure.Notifications;

/// <summary>Builds recipient lists and enqueues one outbox row per recipient.
/// Providers without a credential email are skipped with a log line, never a
/// failure. Provider addresses are gated on EmailVerifiedAt only when 2FA is
/// enabled (the only path that sets the field); customers and admins have no
/// verified flag to gate on.</summary>
public class NotificationService(INotificationOutbox outbox, INotificationData data, NotificationSettings settings, TwoFactorSettings twoFactor, ILogger<NotificationService> logger) : INotificationService
{
    private bool ProviderMailable(string email, bool verified, string what)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Notification skipped ({What}): provider has no credential email.", what);
            return false;
        }
        if (twoFactor.Enabled && !verified)
        {
            logger.LogWarning("Notification skipped ({What}): provider email {Email} is not verified.", what, email);
            return false;
        }
        return true;
    }
    private async Task<List<string>> AdminRecipientsAsync(Guid? agencyId, CancellationToken ct)
    {
        if (agencyId.HasValue)
        {
            var agency = await data.AgencyAdminEmailsAsync(agencyId.Value, ct);
            if (agency.Count > 0) return agency;
        }
        return await data.PlatformAdminEmailsAsync(ct);
    }
    public async Task EmitOrderBookedAsync(Guid orderId, Guid customerId, Guid providerId, string villageId, string scheduledDate, CancellationToken ct)
    {
        if (!settings.Enabled) return;
        var o = await data.OrderBookingAsync(orderId, customerId, providerId, villageId, scheduledDate, ct);
        if (o is null) return;
        var payload = new OrderMailPayload(o.OrderId, o.CustomerFirstName, o.ProviderFirstName, o.Services, o.ScheduledDate, o.Village, o.District, "Pending");
        if (ProviderMailable(o.ProviderEmail, o.ProviderEmailVerified, NotificationEvents.OrderBooked))
            await outbox.EnqueueAsync(NotificationEvents.OrderBooked, o.ProviderEmail, null, $"{NotificationEvents.OrderBooked}:{o.OrderId}:v{o.Version}:{o.ProviderEmail}", NotificationTemplates.OrderNewProvider, payload, ct);
        await outbox.EnqueueAsync(NotificationEvents.OrderBooked, o.CustomerEmail, null, $"{NotificationEvents.OrderBooked}:{o.OrderId}:v{o.Version}:{o.CustomerEmail}", NotificationTemplates.OrderNewCustomer, payload, ct);
        foreach (var admin in await AdminRecipientsAsync(o.ProviderAgencyId, ct))
            await outbox.EnqueueAsync(NotificationEvents.OrderBooked, admin, null, $"{NotificationEvents.OrderBooked}:{o.OrderId}:v{o.Version}:{admin}", NotificationTemplates.OrderNewAdmin, payload, ct);
    }
    public async Task EmitOrderStatusAsync(Guid orderId, uint version, string oldStatus, string newStatus, CancellationToken ct)
    {
        if (!settings.Enabled) return;
        if (string.Equals(oldStatus, newStatus, StringComparison.Ordinal)) return;
        var o = await data.OrderAsync(orderId, ct);
        if (o is null) return;
        var payload = new OrderMailPayload(o.OrderId, o.CustomerFirstName, o.ProviderFirstName, o.Services, o.ScheduledDate, o.Village, o.District, newStatus);
        if (ProviderMailable(o.ProviderEmail, o.ProviderEmailVerified, NotificationEvents.OrderStatus))
            await outbox.EnqueueAsync(NotificationEvents.OrderStatus, o.ProviderEmail, null, $"{NotificationEvents.OrderStatus}:{o.OrderId}:v{version}:{newStatus}:{o.ProviderEmail}", NotificationTemplates.OrderStatusProvider, payload, ct);
        await outbox.EnqueueAsync(NotificationEvents.OrderStatus, o.CustomerEmail, null, $"{NotificationEvents.OrderStatus}:{o.OrderId}:v{version}:{newStatus}:{o.CustomerEmail}", NotificationTemplates.OrderStatusCustomer, payload, ct);
    }
    public async Task EmitReviewReceivedAsync(Guid orderId, uint version, int rating, CancellationToken ct)
    {
        if (!settings.Enabled) return;
        var o = await data.OrderAsync(orderId, ct);
        if (o is null) return;
        if (!ProviderMailable(o.ProviderEmail, o.ProviderEmailVerified, NotificationEvents.ReviewReceived)) return;
        var payload = new ReviewMailPayload(o.OrderId, o.ProviderFirstName, rating);
        await outbox.EnqueueAsync(NotificationEvents.ReviewReceived, o.ProviderEmail, null, $"{NotificationEvents.ReviewReceived}:{o.OrderId}:v{version}:{o.ProviderEmail}", NotificationTemplates.ReviewReceived, payload, ct);
    }
    public async Task EmitApplicationSubmittedAsync(Guid providerId, uint version, CancellationToken ct)
    {
        if (!settings.Enabled) return;
        var p = await data.ProviderAsync(providerId, ct);
        if (p is null) return;
        var payload = new ApplicationMailPayload(p.ProviderId, p.FirstName);
        if (ProviderMailable(p.Email, p.EmailVerified, NotificationEvents.ApplicationSubmitted))
            await outbox.EnqueueAsync(NotificationEvents.ApplicationSubmitted, p.Email, null, $"{NotificationEvents.ApplicationSubmitted}:{p.ProviderId}:v{version}:{p.Email}", NotificationTemplates.ApplicationSubmittedProvider, payload, ct);
        foreach (var admin in await AdminRecipientsAsync(p.AgencyId, ct))
            await outbox.EnqueueAsync(NotificationEvents.ApplicationSubmitted, admin, null, $"{NotificationEvents.ApplicationSubmitted}:{p.ProviderId}:v{version}:{admin}", NotificationTemplates.ApplicationSubmittedAdmin, payload, ct);
    }
    public async Task EmitApplicationDecisionAsync(Guid providerId, uint version, string decision, string? note, CancellationToken ct)
    {
        if (!settings.Enabled) return;
        var template = decision switch
        {
            NotificationEvents.ApplicationApproved => NotificationTemplates.ApplicationApproved,
            NotificationEvents.ApplicationRejected => NotificationTemplates.ApplicationRejected,
            NotificationEvents.ApplicationNeedsChanges => NotificationTemplates.ApplicationNeedsChanges,
            _ => throw new ArgumentException($"Unknown application decision: {decision}", nameof(decision))
        };
        var p = await data.ProviderAsync(providerId, ct);
        if (p is null) return;
        if (!ProviderMailable(p.Email, p.EmailVerified, decision)) return;
        var payload = new ApplicationMailPayload(p.ProviderId, p.FirstName, note);
        await outbox.EnqueueAsync(decision, p.Email, null, $"{decision}:{p.ProviderId}:v{version}:{p.Email}", template, payload, ct);
    }
    public async Task EmitProviderSuspendedAsync(Guid providerId, uint version, string reason, CancellationToken ct)
    {
        if (!settings.Enabled) return;
        var p = await data.ProviderAsync(providerId, ct);
        if (p is null) return;
        if (!ProviderMailable(p.Email, p.EmailVerified, NotificationEvents.ProviderSuspended)) return;
        var payload = new ApplicationMailPayload(p.ProviderId, p.FirstName, reason);
        await outbox.EnqueueAsync(NotificationEvents.ProviderSuspended, p.Email, null, $"{NotificationEvents.ProviderSuspended}:{p.ProviderId}:v{version}:{p.Email}", NotificationTemplates.ProviderSuspended, payload, ct);
    }
    public async Task EmitProviderReactivatedAsync(Guid providerId, uint version, CancellationToken ct)
    {
        if (!settings.Enabled) return;
        var p = await data.ProviderAsync(providerId, ct);
        if (p is null) return;
        if (!ProviderMailable(p.Email, p.EmailVerified, NotificationEvents.ProviderReactivated)) return;
        var payload = new ApplicationMailPayload(p.ProviderId, p.FirstName);
        await outbox.EnqueueAsync(NotificationEvents.ProviderReactivated, p.Email, null, $"{NotificationEvents.ProviderReactivated}:{p.ProviderId}:v{version}:{p.Email}", NotificationTemplates.ProviderReactivated, payload, ct);
    }
    public async Task EmitAgencyStatusAsync(Guid agencyId, string status, CancellationToken ct)
    {
        if (!settings.Enabled) return;
        var (eventType, template) = status switch
        {
            "Suspended" => (NotificationEvents.AgencySuspended, NotificationTemplates.AgencySuspended),
            "Approved" => (NotificationEvents.AgencyReactivated, NotificationTemplates.AgencyReactivated),
            _ => throw new ArgumentException($"Unknown agency status: {status}", nameof(status))
        };
        var agency = await data.AgencyAsync(agencyId, ct);
        if (agency is null) return;
        var payload = new AgencyMailPayload(agency.AgencyId, agency.Name);
        foreach (var admin in await data.AgencyAdminEmailsAsync(agencyId, ct))
            await outbox.EnqueueAsync(eventType, admin, null, $"{eventType}:{agencyId}:{admin}", template, payload, ct);
    }
    public async Task EmitPasswordChangedAsync(Guid providerId, string email, string name, string changedAtTicks, bool emailVerified, CancellationToken ct)
    {
        if (!settings.Enabled) return;
        if (!ProviderMailable(email, emailVerified, NotificationEvents.PasswordChanged)) return;
        var space = name.IndexOf(' ');
        var first = space < 0 ? name : name[..space];
        var payload = new PasswordMailPayload(string.IsNullOrWhiteSpace(first) ? "Pengguna" : first);
        await outbox.EnqueueAsync(NotificationEvents.PasswordChanged, email, providerId, $"{NotificationEvents.PasswordChanged}:{providerId}:{changedAtTicks}:{email}", NotificationTemplates.PasswordChanged, payload, ct);
    }
    public Task DispatchEnqueuedAsync(CancellationToken ct) => outbox.DispatchEnqueuedAsync(ct);
}
