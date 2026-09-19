using Microsoft.Extensions.Configuration;

namespace BantuBantu.Api;

public sealed class AllowedOriginPolicy
{
    private readonly HashSet<string> exact;
    private readonly string[] wildcardSuffixes;

    private AllowedOriginPolicy(IEnumerable<string> exactOrigins, IEnumerable<string> wildcardSuffixes)
    {
        exact = new HashSet<string>(exactOrigins, StringComparer.OrdinalIgnoreCase);
        this.wildcardSuffixes = wildcardSuffixes.ToArray();
        PrimaryOrigin = exact.FirstOrDefault() ?? throw new InvalidOperationException("At least one frontend origin is required.");
    }

    public string PrimaryOrigin { get; }

    public static AllowedOriginPolicy FromConfiguration(IConfiguration configuration)
    {
        var exact = Split(configuration["Frontend:Origins"])
            .Concat(Split(configuration["Frontend:Origin"]))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (var origin in exact)
            ValidateOrigin(origin);

        var suffixes = Split(configuration["Frontend:OriginPatterns"])
            .Select(ParseWildcardSuffix)
            .ToArray();
        if (exact.Length == 0 && suffixes.Length == 0)
            throw new InvalidOperationException("Missing configuration: Frontend:Origin or Frontend:Origins");
        return new AllowedOriginPolicy(exact, suffixes);
    }

    public bool IsAllowed(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return false;
        if (exact.Contains(origin)) return true;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) return false;
        return wildcardSuffixes.Any(suffix => uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && !uri.Host.Equals(suffix.TrimStart('.'), StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> Split(string? value) => (value ?? string.Empty)
        .Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string ParseWildcardSuffix(string pattern)
    {
        if (!pattern.StartsWith("https://*.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Frontend:OriginPatterns only supports https://*.domain patterns: {pattern}");
        var suffix = pattern["https://*".Length..].TrimEnd('/').ToLowerInvariant();
        if (suffix.Length < 3 || suffix.Contains('/')) throw new InvalidOperationException($"Invalid frontend origin pattern: {pattern}");
        return suffix;
    }

    private static void ValidateOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.PathAndQuery) && uri.PathAndQuery != "/" ||
            !string.IsNullOrEmpty(uri.Fragment) || origin.EndsWith('/'))
            throw new InvalidOperationException("Frontend origins must be exact http(s) origins without a trailing slash.");
    }
}
