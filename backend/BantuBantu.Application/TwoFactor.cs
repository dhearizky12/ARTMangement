using System.ComponentModel.DataAnnotations;

namespace BantuBantu.Application;

public sealed record TwoFactorSettings
{
    public bool Enabled { get; init; }
    public bool ProviderPasswordFallback { get; init; } = true;
    public int CodeTtlMinutes { get; init; } = 10;
    public int MaxAttempts { get; init; } = 5;
    public int ResendCooldownSeconds { get; init; } = 60;
    public int MaxResendsPerHour { get; init; } = 5;
    public int MaxSkipsPerHour { get; init; } = 3;
}

/// <summary>Step-1 login response when a session cannot be issued yet.</summary>
public record TwoFactorLoginChallenge(
    bool RequiresTwoFactor,
    Guid ChallengeId,
    string Method,
    string MaskedEmail,
    bool FallbackAllowed);

public record TwoFactorVerifyRequest([Required] Guid ChallengeId, [Required, StringLength(6, MinimumLength = 6)] string Code);
public record TwoFactorResendRequest([Required] Guid ChallengeId);
public record TwoFactorSkipRequest([Required] Guid ChallengeId);
public record TwoFactorStepUpStartRequest([Required, MaxLength(512)] string RefreshToken);
public record TwoFactorStepUpVerifyRequest([Required] Guid ChallengeId, [Required, StringLength(6, MinimumLength = 6)] string Code, [Required, MaxLength(512)] string RefreshToken);

/// <summary>Issues and verifies one-time email codes. Codes are never stored in the clear.</summary>
public interface IEmailOtpService
{
    string Generate();
    string Hash(string code);
    bool Verify(string code, string storedHashHex);
    string Mask(string email);
    Task SendAsync(string to, string code, int ttlMinutes, CancellationToken ct);
}