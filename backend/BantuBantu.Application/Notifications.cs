using System.Text.Json;
using BantuBantu.Domain;
namespace BantuBantu.Application;

/// <summary>Non-secret notification tuning. See NotificationSettingsFactory for
/// parsing, defaults and validation.</summary>
public record NotificationSettings
{
    public bool Enabled { get; init; }
    public int OutboxPollSeconds { get; init; } = 30;
    public int MaxAttempts { get; init; } = 6;
    public int MaxPerRecipientPerHour { get; init; } = 10;
}
public record AppSettings
{
    // Base URL of the web frontend, used to build email links. Falls back to
    // Frontend:Origin when unset.
    public string FrontendBaseUrl { get; init; } = "";
}
public record EmailSettings
{
    // Optional reply-to / support address shown in the email footer. Empty by
    // default; when empty the support line is omitted from the footer.
    public string ReplyTo { get; init; } = "";
}

/// <summary>Machine names for business events. Only events with a real
/// transition site in the services may be emitted.</summary>
public static class NotificationEvents
{
    public const string OrderBooked = "order.booked";
    public const string OrderStatus = "order.status";
    public const string ReviewReceived = "review.received";
    public const string ApplicationSubmitted = "application.submitted";
    public const string ApplicationApproved = "application.approved";
    public const string ApplicationRejected = "application.rejected";
    public const string ApplicationNeedsChanges = "application.needs-changes";
    public const string ProviderSuspended = "provider.suspended";
    public const string ProviderReactivated = "provider.reactivated";
    public const string AgencySuspended = "agency.suspended";
    public const string AgencyReactivated = "agency.reactivated";
    public const string PasswordChanged = "password.changed";
}
/// <summary>Template keys. One key selects one subject + HTML + text body.
/// Auth (2FA) mails stay on the immediate path and reuse the shared layout.</summary>
public static class NotificationTemplates
{
    public const string OrderNewProvider = "order.new.provider";
    public const string OrderNewCustomer = "order.new.customer";
    public const string OrderNewAdmin = "order.new.admin";
    public const string OrderStatusProvider = "order.status.provider";
    public const string OrderStatusCustomer = "order.status.customer";
    public const string ReviewReceived = "review.received";
    public const string ApplicationSubmittedProvider = "application.submitted.provider";
    public const string ApplicationSubmittedAdmin = "application.submitted.admin";
    public const string ApplicationApproved = "application.approved";
    public const string ApplicationRejected = "application.rejected";
    public const string ApplicationNeedsChanges = "application.needs-changes";
    public const string ProviderSuspended = "provider.suspended";
    public const string ProviderReactivated = "provider.reactivated";
    public const string AgencySuspended = "agency.suspended";
    public const string AgencyReactivated = "agency.reactivated";
    public const string PasswordChanged = "password.changed";
    public const string AuthOtp = "auth.otp";
}
// Minimal payloads. Every value is HTML-escaped at render time. Never put
// full addresses, phones, documents or ID numbers in here.
public record OrderMailPayload(Guid OrderId, string CustomerFirstName, string ProviderFirstName, string[] Services, string ScheduledDate, string Village, string District, string Status, int? Rating = null);
public record ApplicationMailPayload(Guid ProviderId, string ProviderFirstName, string? Note = null);
public record AgencyMailPayload(Guid AgencyId, string AgencyName);
public record ReviewMailPayload(Guid OrderId, string ProviderFirstName, int Rating);
public record PasswordMailPayload(string Name);
public record OtpMailPayload(string Code, int TtlMinutes);
public record RenderedEmail(string Subject, string Html, string Text);

/// <summary>Shared JSON options for outbox payloads: camelCase so the
/// renderer and the preview samples read the same property names.</summary>
public static class NotificationJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string Serialize(object payload) => JsonSerializer.Serialize(payload, Options);
}

/// <summary>Thrown when a send fails transiently (429/5xx/network): the
/// outbox worker retries with backoff.</summary>
public class TransientEmailException(string message) : ProfileException(message, 502, "EMAIL_SEND_FAILED");
/// <summary>Thrown when a send can never succeed (invalid address, Resend
/// sandbox 403, bad key): the worker marks Failed without retrying.</summary>
public class PermanentEmailException(string message) : ProfileException(message, 502, "EMAIL_SEND_FAILED");

/// <summary>Scoped outbox bound to the request's DbContext: Enqueue only
/// stages rows, the caller's SaveAsync commits them in the same transaction.
/// DispatchEnqueuedAsync makes one best-effort immediate send after commit.</summary>
public interface INotificationOutbox
{
    Task<EmailOutbox?> EnqueueAsync(string eventType, string to, Guid? userId, string dedupeKey, string templateKey, object payload, CancellationToken ct);
    Task DispatchEnqueuedAsync(CancellationToken ct);
}
/// <summary>Scoped read-only projections used only to build notifications.
/// AsNoTracking, single round trips, no change tracking.</summary>
public interface INotificationData
{
    Task<OrderMailData?> OrderAsync(Guid orderId, CancellationToken ct);
    // Booking projection: the order row does not exist yet (same-transaction
    // enqueue), so everything is loaded from pre-existing rows by id. The
    // schedule comes from the request; the status is always Pending.
    Task<OrderMailData?> OrderBookingAsync(Guid orderId, Guid customerId, Guid providerId, string villageId, string scheduledDate, CancellationToken ct);
    Task<ProviderMailData?> ProviderAsync(Guid providerId, CancellationToken ct);
    Task<AgencyMailData?> AgencyAsync(Guid agencyId, CancellationToken ct);
    Task<List<string>> AgencyAdminEmailsAsync(Guid agencyId, CancellationToken ct);
    Task<List<string>> PlatformAdminEmailsAsync(CancellationToken ct);
}
public record OrderMailData(Guid OrderId, uint Version, string CustomerEmail, string CustomerFirstName, string ProviderFirstName, string ProviderEmail, bool ProviderEmailVerified, Guid? ProviderAgencyId, string[] Services, string ScheduledDate, string Village, string District);
public record ProviderMailData(Guid ProviderId, uint Version, string FirstName, string Email, bool EmailVerified, Guid? AgencyId, string? Note);
public record AgencyMailData(Guid AgencyId, string Name);
/// <summary>Renders one template key + JSON payload into subject/HTML/text.
/// Pure function of (template, payload, baseUrl, replyTo).</summary>
public interface INotificationRenderer
{
    RenderedEmail Render(string templateKey, JsonElement payload);
    IReadOnlyList<(string TemplateKey, string PayloadJson)> PreviewSamples();
}
/// <summary>High-level emit API used by the application services. Each method
/// enqueues (same transaction) and the caller dispatches after SaveAsync.</summary>
public interface INotificationService
{
    Task EmitOrderBookedAsync(Guid orderId, Guid customerId, Guid providerId, string villageId, string scheduledDate, CancellationToken ct);
    Task EmitOrderStatusAsync(Guid orderId, uint version, string oldStatus, string newStatus, CancellationToken ct);
    Task EmitReviewReceivedAsync(Guid orderId, uint version, int rating, CancellationToken ct);
    Task EmitApplicationSubmittedAsync(Guid providerId, uint version, CancellationToken ct);
    Task EmitApplicationDecisionAsync(Guid providerId, uint version, string decision, string? note, CancellationToken ct);
    Task EmitProviderSuspendedAsync(Guid providerId, uint version, string reason, CancellationToken ct);
    Task EmitProviderReactivatedAsync(Guid providerId, uint version, CancellationToken ct);
    Task EmitAgencyStatusAsync(Guid agencyId, string status, CancellationToken ct);
    Task EmitPasswordChangedAsync(Guid providerId, string email, string name, string changedAtTicks, bool emailVerified, CancellationToken ct);
    Task DispatchEnqueuedAsync(CancellationToken ct);
}
