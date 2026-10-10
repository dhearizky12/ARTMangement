using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BantuBantu.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BantuBantu.Infrastructure;

/// <summary>
/// Sends mail through the Resend HTTP API using a sending-only key
/// (permission=sending_access, minted by scripts/provision-resend-key.sh).
/// The key is read from configuration, never from .env directly and never logged.
/// Transactional only: no tracking headers are set, so Resend's open/click
/// tracking stays off for these messages.
/// </summary>
public class ResendEmailSender(HttpClient http, IConfiguration configuration, ILogger<ResendEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var apiKey = configuration["Resend:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ProfileException("Konfigurasi email belum lengkap: Resend:ApiKey kosong. Provision sending-only key dengan scripts/provision-resend-key.sh.", 500, "EMAIL_CONFIG_MISSING");

        var from = configuration["Resend:From"];
        if (string.IsNullOrWhiteSpace(from))
            throw new ProfileException("Konfigurasi email belum lengkap: Resend:From kosong (alamat pengirim dari domain yang sudah diverifikasi).", 500, "EMAIL_CONFIG_MISSING");

        var payload = JsonSerializer.Serialize(new
        {
            from,
            to = new[] { message.To },
            subject = message.Subject,
            html = message.Html,
            text = message.Text
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (!string.IsNullOrWhiteSpace(message.IdempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", message.IdempotencyKey);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new TransientEmailException($"Resend tidak terjangkau untuk {message.To}: {ex.GetType().Name}.");
        }
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("Email accepted by Resend for recipient {Recipient} (status {Status}).", message.To, (int)response.StatusCode);
                return;
            }
            var status = (int)response.StatusCode;
            var detail = body.Length > 300 ? body[..300] : body;
            // 429/5xx: retry later. Anything else (invalid address, the
            // resend.dev sandbox 403, bad key) can never succeed: fail fast.
            if (status == 429 || status >= 500)
                throw new TransientEmailException($"Resend menolak sementara email (HTTP {status}) untuk {message.To}: {detail}");
            throw new PermanentEmailException($"Resend menolak email (HTTP {status}) untuk {message.To}: {detail}");
        }
    }
}
