namespace BantuBantu.Api.Diagnostics;

/// <summary>
/// Diagnostics switches. Both are off by default: the Server-Timing header
/// exposes per-request database timing, so it must be opted into explicitly
/// (Development, or a deliberate staging probe) rather than shipped on.
/// </summary>
public sealed class DiagnosticsSettings
{
    public bool ServerTiming { get; init; }

    public static DiagnosticsSettings FromConfiguration(IConfiguration configuration) => new()
    {
        ServerTiming = string.Equals(configuration["Diagnostics:ServerTiming"], "true", StringComparison.OrdinalIgnoreCase)
    };
}
