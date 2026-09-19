using Microsoft.Extensions.Configuration;
using Npgsql;

namespace BantuBantu.Infrastructure;

/// <summary>
/// Resolves the application's database setting and converts Neon postgres URIs
/// to the key/value format understood by Npgsql.
/// </summary>
public sealed class DatabaseConnectionStringResolver
{
    public string Resolve(IConfiguration configuration, bool preferUnpooled = false)
    {
        var keys = preferUnpooled
            ? new[] { "DATABASE_URL_UNPOOLED", "ConnectionStrings:Default", "DATABASE_URL" }
            : new[] { "ConnectionStrings:Default", "DATABASE_URL" };

        var raw = keys
            .Select(key => configuration[key])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("Missing database configuration. Set ConnectionStrings:Default or DATABASE_URL.");

        return Convert(raw.Trim());
    }

    public static string Convert(string value)
    {
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            var existing = new NpgsqlConnectionStringBuilder(value);
            if ((existing.Host ?? string.Empty).EndsWith(".neon.tech", StringComparison.OrdinalIgnoreCase) &&
                !value.Contains("sslmode", StringComparison.OrdinalIgnoreCase))
                existing.SslMode = SslMode.Require;
            return existing.ConnectionString;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            throw new InvalidOperationException("DATABASE_URL must be a valid postgres:// or postgresql:// URI.");

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            SslMode = SslMode.Require
        };

        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var userInfo = uri.UserInfo.Split(':', 2);
            builder.Username = Uri.UnescapeDataString(userInfo[0]);
            if (userInfo.Length == 2)
                builder.Password = Uri.UnescapeDataString(userInfo[1]);
        }

        foreach (var (key, queryValue) in ParseQuery(uri.Query))
        {
            switch (key.ToLowerInvariant())
            {
                case "sslmode":
                    if (Enum.TryParse<SslMode>(queryValue, true, out var sslMode))
                        builder.SslMode = sslMode;
                    break;
                case "channel_binding":
                    if (Enum.TryParse<ChannelBinding>(queryValue, true, out var channelBinding))
                        builder.ChannelBinding = channelBinding;
                    break;
                case "application_name":
                    builder.ApplicationName = queryValue;
                    break;
            }
        }

        return builder.ConnectionString;
    }

    private static IEnumerable<(string Key, string Value)> ParseQuery(string query)
    {
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = separator < 0 ? part : part[..separator];
            var value = separator < 0 ? string.Empty : part[(separator + 1)..];
            if (string.IsNullOrWhiteSpace(key))
                continue;
            yield return (Uri.UnescapeDataString(key.Replace('+', ' ')), Uri.UnescapeDataString(value.Replace('+', ' ')));
        }
    }
}
