using BantuBantu.Application;
using Microsoft.Extensions.Configuration;

namespace BantuBantu.Infrastructure.Notifications;

public static class NotificationSettingsFactory
{
    public static (NotificationSettings Notifications, AppSettings App, EmailSettings Email) FromConfiguration(IConfiguration configuration)
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
        var notifications = new NotificationSettings
        {
            Enabled = Flag(configuration, "Notifications:Enabled", false),
            OutboxPollSeconds = Int(configuration, "Notifications:OutboxPollSeconds", 30, 5, 300),
            MaxAttempts = Int(configuration, "Notifications:MaxAttempts", 6, 1, 10),
            MaxPerRecipientPerHour = Int(configuration, "Notifications:MaxPerRecipientPerHour", 10, 1, 100),
            OpportunisticTimeoutSeconds = Int(configuration, "Notifications:OpportunisticTimeoutSeconds", 2, 1, 30)
        };
        var baseUrl = (configuration["App:FrontendBaseUrl"] ?? "").Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = (configuration["Frontend:Origin"] ?? "").Trim();
        if (!string.IsNullOrWhiteSpace(baseUrl) && (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw new InvalidOperationException("Invalid configuration: App:FrontendBaseUrl must be an absolute http(s) URL.");
        return (notifications, new AppSettings { FrontendBaseUrl = baseUrl.TrimEnd('/') }, new EmailSettings { ReplyTo = (configuration["Email:ReplyTo"] ?? "").Trim() });
    }
}
