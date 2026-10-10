namespace BantuBantu.Application;

public record EmailMessage(string To, string Subject, string Html, string Text = "", string? IdempotencyKey = null);

/// <summary>
/// Sends a single email. Implemented by <c>ResendEmailSender</c> outside
/// Development (key: <c>Resend:ApiKey</c>, sending-only) and by
/// <c>LoggingEmailSender</c> in Development, where nothing is delivered.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
