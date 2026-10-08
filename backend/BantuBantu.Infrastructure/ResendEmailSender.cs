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
            html = message.Html
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new ProfileException($"Resend menolak email (HTTP {(int)response.StatusCode}) untuk {message.To}: {(body.Length > 300 ? body[..300] : body)}", 502, "EMAIL_SEND_FAILED");

        logger.LogInformation("Email accepted by Resend for recipient {Recipient} (status {Status}).", message.To, (int)response.StatusCode);
    }
}
