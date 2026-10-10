using System.Net.Mail;
using BantuBantu.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BantuBantu.Infrastructure;

/// <summary>
/// Sends mail through a plain SMTP relay (Mailpit in local development).
/// Selected when <c>Email:Smtp:Host</c> is configured; otherwise the
/// Development logger or the Resend sender is used. No auth/TLS: local
/// simulators only, never production.
/// </summary>
public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var host = configuration["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host))
            throw new ProfileException("Konfigurasi email belum lengkap: Email:Smtp:Host kosong.", 500, "EMAIL_CONFIG_MISSING");
        var port = int.TryParse(configuration["Email:Smtp:Port"], out var parsed) ? parsed : 1025;
        var from = configuration["Email:Smtp:From"];
        if (string.IsNullOrWhiteSpace(from)) from = configuration["Resend:From"];
        if (string.IsNullOrWhiteSpace(from))
            throw new ProfileException("Konfigurasi email belum lengkap: Email:Smtp:From kosong (dan Resend:From juga kosong).", 500, "EMAIL_CONFIG_MISSING");

        MailAddress fromAddress;
        try
        {
            fromAddress = new MailAddress(from);
        }
        catch (FormatException ex)
        {
            throw new PermanentEmailException($"Alamat pengirim tidak valid: {from}. ({ex.Message})");
        }
        MailAddress toAddress;
        try
        {
            toAddress = new MailAddress(message.To);
        }
        catch (FormatException ex)
        {
            throw new PermanentEmailException($"Alamat tujuan tidak valid: {message.To}. ({ex.Message})");
        }

        using var mail = new MailMessage(fromAddress, toAddress)
        {
            Subject = message.Subject,
            Body = message.Html,
            IsBodyHtml = true,
        };
        if (!string.IsNullOrWhiteSpace(message.Text))
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.Text, null, "text/plain"));

        using var client = new SmtpClient(host, port) { EnableSsl = false, DeliveryMethod = SmtpDeliveryMethod.Network };
        try
        {
            await client.SendMailAsync(mail, ct);
        }
        catch (Exception ex) when (ex is SmtpException or HttpRequestException or TaskCanceledException or IOException)
        {
            throw new TransientEmailException($"SMTP {host}:{port} tidak terjangkau untuk {message.To}: {ex.GetType().Name}.");
        }
        logger.LogInformation("Email accepted by SMTP {Host}:{Port} for recipient {Recipient}.", host, port, message.To);
    }
}
