using BantuBantu.Application;
using Microsoft.Extensions.Configuration;

namespace BantuBantu.Infrastructure;

public static class TwoFactorSettingsFactory
{
    public static TwoFactorSettings FromConfiguration(IConfiguration configuration)
    {
        static bool Flag(IConfiguration c, string key, bool fallback)
        {
            var raw = c[key]?.Trim();
            return string.IsNullOrWhiteSpace(raw) ? fallback : bool.TryParse(raw, out var value) ? value
                : throw new InvalidOperationException($"Invalid configuration: {key} must be true or false.");
        }
        static int Int(IConfiguration c, string key, int fallback, int min, int max)
        {
            var raw = c[key]?.Trim();
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            if (!int.TryParse(raw, out var value) || value < min || value > max)
                throw new InvalidOperationException($"Invalid configuration: {key} must be an integer between {min} and {max}.");
            return value;
        }
        var codeTtlMinutes = Int(configuration, "TwoFactor:CodeTtlMinutes", 10, 2, 30);
        var resendCooldownSeconds = Int(configuration, "TwoFactor:ResendCooldownSeconds", 60, 30, 300);
        if (resendCooldownSeconds >= codeTtlMinutes * 60)
            throw new InvalidOperationException("Invalid configuration: TwoFactor:ResendCooldownSeconds must be shorter than TwoFactor:CodeTtlMinutes.");
        return new TwoFactorSettings
        {
            Enabled = Flag(configuration, "TwoFactor:Enabled", false),
            ProviderPasswordFallback = Flag(configuration, "TwoFactor:ProviderPasswordFallback", true),
            CodeTtlMinutes = codeTtlMinutes,
            MaxAttempts = Int(configuration, "TwoFactor:MaxAttempts", 5, 3, 10),
            ResendCooldownSeconds = resendCooldownSeconds,
            MaxResendsPerHour = Int(configuration, "TwoFactor:MaxResendsPerHour", 5, 1, 20),
            MaxSkipsPerHour = Int(configuration, "TwoFactor:MaxSkipsPerHour", 3, 1, 10)
        };
    }
}