namespace BantuBantu.Domain;

/// <summary>
/// Session access level, decided by the server and stamped into the access
/// token (amr claim for "Full", "limited" claim for "Limited"). A null value
/// on a stored refresh session means the session was created before 2FA
/// rollout ("None"); while 2FA is enabled such sessions are rejected.
/// </summary>
public enum SessionAccess { Full, Limited }

public enum TwoFactorRequirement { Login, StepUp }

/// <summary>
/// A single email-code challenge. The code itself is never stored; only an
/// HMAC-SHA256 digest under a key derived from the JWT signing key material.
/// A new challenge invalidates any previous one for the same account, and a
/// successful resend replaces the code hash on the same row. Attempts survive
/// resends so an account cannot reset the 5-attempt budget by re-issuing.
/// </summary>
public class TwoFactorChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public UserRole Role { get; set; }
    public Guid AccountId { get; set; }
    public TwoFactorRequirement Requirement { get; set; }
    public string Email { get; set; } = "";
    public string MaskedEmail { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; }
    public DateTimeOffset? ResentAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public DateTimeOffset? InvalidatedAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string? SessionJti { get; set; }
}

/// <summary>
/// Rolling window counters per 2FA account. Keyed on account rather than IP so
/// the Cloudflare Worker proxy (a single origin address) cannot throttle or
/// bypass the limits.
/// </summary>
public class TwoFactorAccountState
{
    public Guid AccountId { get; set; }
    public UserRole Role { get; set; }
    public int ResendsInWindow { get; set; }
    public DateTimeOffset? ResendWindowStart { get; set; }
    public int SkipsInWindow { get; set; }
    public DateTimeOffset? SkipWindowStart { get; set; }
}