using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BantuBantu.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BantuBantu.Infrastructure;

/// <summary>
/// Issues and verifies one-time email codes. The code key is derived with
/// HKDF-SHA256 from the JWT signing key material (salt "bantubantu-2fa-v1",
/// info "email-otp-code-hmac") so no extra secret has to be provisioned.
/// Codes are stored only as HMAC-SHA256 hex and are logged in the clear only
/// in Development when no Resend API key is configured.
/// </summary>
public class EmailOtpService : IEmailOtpService
{
    private static readonly byte[] Salt = Encoding.UTF8.GetBytes("bantubantu-2fa-v1");
    private static readonly byte[] Info = Encoding.UTF8.GetBytes("email-otp-code-hmac");
    private readonly byte[] key;
    private readonly IEmailSender sender;
    private readonly IConfiguration config;
    private readonly ILogger<EmailOtpService> logger;

    public EmailOtpService(RsaKeys keys, IEmailSender sender, IConfiguration config, ILogger<EmailOtpService> logger)
    {
        this.sender = sender;
        this.config = config;
        this.logger = logger;
        key = HKDF.DeriveKey(HashAlgorithmName.SHA256, keys.PrivateKeyMaterial, 32, Salt, Info);
    }

    public string Generate() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    public string Hash(string code) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(code)));
    public bool Verify(string code, string storedHashHex)
    {
        if (storedHashHex.Length != 64) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(storedHashHex), HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(code)));
    }
    public string Mask(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0) return "***";
        var local = email[..at];
        var domain = email[(at + 1)..];
        var prefix = local.Length > 0 ? local[..1] : "*";
        return $"{prefix}***@{domain}";
    }
    public async Task SendAsync(string to, string code, int ttlMinutes, CancellationToken ct)
    {
        var environment = config["ASPNETCORE_ENVIRONMENT"];
        var apiKey = config["Resend:ApiKey"];
        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("2FA kode untuk {To} (hanya pengembangan): {Code}", to, code);
            return;
        }
        var html =
            "<p>Halo,</p>" +
            "<p>Gunakan kode berikut untuk masuk ke akun Bantu-Bantu:</p>" +
            $"<p style=\"font-size:2rem;letter-spacing:.5rem;font-weight:700;\">{code}</p>" +
            $"<p>Berlaku {ttlMinutes} menit.</p>" +
            "<p>Jangan bagikan kode ini kepada siapa pun.</p>" +
            "<p>Abaikan email ini jika bukan Anda yang memintanya.</p>";
        await sender.SendAsync(new EmailMessage(to, "Kode masuk Bantu-Bantu", html), ct);
    }
}