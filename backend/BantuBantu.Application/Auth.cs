using System.ComponentModel.DataAnnotations;
using BantuBantu.Domain;
using Microsoft.Extensions.Logging;
namespace BantuBantu.Application;

public record GoogleRequest([Required, MaxLength(10000)] string Credential);
public record AdminRequest([Required, EmailAddress] string Email, [Required, MaxLength(256)] string Password);
public record ProviderLoginRequest([Required, EmailAddress] string Email, [Required, MaxLength(256)] string Password);
public record ProviderRegistrationRequest([Required, EmailAddress] string Email, [Required, StringLength(256, MinimumLength = 14)] string Password, [Required, StringLength(256, MinimumLength = 14)] string ConfirmPassword);
public record ChangePasswordRequest([Required, MaxLength(256)] string CurrentPassword, [Required, StringLength(256, MinimumLength = 14)] string NewPassword);
public record RefreshRequest([Required, MaxLength(256)] string RefreshToken);
public record GoogleIdentity(string Sub, string Email, string Name, string? Picture, string Json);
public record UserDto(Guid Id, string Email, string FullName, string? PictureUrl, string Role, bool ProfileCompleted, string ProfileStep, Guid? AgencyId = null, string? ApplicationStatus = null)
{
    public static UserDto From(User u) => new(u.Id, u.Email, u.FullName, u.PictureUrl, u.Role.ToString(), u.ProfileCompleted, u.ProfileCompleted ? "done" : u.ProfileStep, (u as AdminAccount)?.AgencyId);
    public static UserDto FromProvider(Provider p, string email) => new(p.Id, email, p.FullName, null, UserRole.Provider.ToString(), true, "done", null, p.ApplicationStatus.ToString());
}
public record AccessToken(string Value, DateTimeOffset ExpiresAt);
public record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User, string RefreshToken, DateTimeOffset RefreshExpiresAt);
public record AuthResult(AuthResponse Response);

/// <summary>Discriminated result of a login-style operation: either a full session or a 2FA challenge.</summary>
public abstract record AuthOutcome;
public sealed record SessionOutcome(AuthResponse Response) : AuthOutcome;
public sealed record ChallengeOutcome(TwoFactorLoginChallenge Challenge) : AuthOutcome;

public class AuthenticationFailedException : Exception { }
public interface IGoogleIdentityVerifier { Task<GoogleIdentity> VerifyAsync(string credential); }
public interface ITokenService
{
    AccessToken Create(User user, SessionAccess? level = null);
    AccessToken Create(Provider provider, string email, SessionAccess? level = null);
    string NewRefreshToken();
    string Hash(string token);
    int RefreshDays { get; }
}
public interface IPasswordService
{
    string Hash(AdminAccount user, string password);
    bool Verify(AdminAccount user, string password);
    string HashProvider(string password);
    bool VerifyProvider(string passwordHash, string password);
}
public interface IAuthRepository
{
    Task<User?> FindGoogleAsync(string sub, CancellationToken ct);
    Task<User?> FindAdminAsync(string email, CancellationToken ct);
    Task<User?> FindUserAsync(Guid id, CancellationToken ct);
    Task<ProviderCredential?> FindProviderCredentialAsync(string email, CancellationToken ct);
    Task<ProviderCredential?> FindProviderCredentialByProviderAsync(Guid providerId, CancellationToken ct);
    Task<Provider?> FindProviderAsync(Guid id, CancellationToken ct);
    Task<bool> CanAuthenticateAsync(User user, CancellationToken ct);
    Task<bool> CanAuthenticateProviderAsync(Guid providerId, CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    void AddProvider(Provider provider);
    void AddUser(User user, ExternalLogin? login = null);
    void AddSession(RefreshSession session);
    void AddProviderSession(ProviderRefreshSession session);
    Task<RefreshSession?> ConsumeSessionAsync(string hash, CancellationToken ct);
    Task<ProviderRefreshSession?> ConsumeProviderSessionAsync(string hash, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);

    Task InvalidateTwoFactorChallengesAsync(Guid accountId, UserRole role, TwoFactorRequirement requirement, CancellationToken ct);
    void AddTwoFactorChallenge(TwoFactorChallenge challenge);
    Task<TwoFactorChallenge?> FindTwoFactorChallengeAsync(Guid id, CancellationToken ct);
    Task<TwoFactorAccountState?> FindTwoFactorAccountStateAsync(Guid accountId, CancellationToken ct);
    void AddTwoFactorAccountState(TwoFactorAccountState state);
}
public interface IAuthService
{
    Task<AuthOutcome> GoogleAsync(string credential, CancellationToken ct);
    Task<AuthOutcome> AdminAsync(string email, string password, CancellationToken ct);
    Task<AuthOutcome> RegisterProviderAsync(string email, string password, string confirmPassword, CancellationToken ct);
    Task<AuthOutcome> ProviderAsync(string email, string password, CancellationToken ct);
    Task ChangeProviderPasswordAsync(Guid providerId, string currentPassword, string newPassword, CancellationToken ct);
    Task<AuthOutcome> RefreshAsync(string token, CancellationToken ct);
    Task LogoutAsync(string token, CancellationToken ct);
    Task<UserDto> MeAsync(Guid id, CancellationToken ct);
    Task<AuthOutcome> VerifyTwoFactorCodeAsync(Guid challengeId, string code, CancellationToken ct);
    Task ResendTwoFactorCodeAsync(Guid challengeId, CancellationToken ct);
    Task<AuthOutcome> SkipTwoFactorAsync(Guid challengeId, CancellationToken ct);
    Task<TwoFactorLoginChallenge> StartProviderStepUpAsync(Guid providerId, string email, string jti, CancellationToken ct);
    Task<AuthOutcome> VerifyProviderStepUpAsync(Guid challengeId, string code, Guid providerId, string jti, string refreshToken, CancellationToken ct);
}
public class AuthService(IAuthRepository repository, IGoogleIdentityVerifier google, ITokenService tokens, IPasswordService passwords, IEmailOtpService otp, TwoFactorSettings twoFactor, INotificationService notifications, ILoggerFactory loggerFactory) : IAuthService
{
    private readonly ILogger security = loggerFactory.CreateLogger("BantuBantu.Security");

    public async Task<AuthOutcome> GoogleAsync(string credential, CancellationToken ct)
    {
        var identity = await google.VerifyAsync(credential);
        var user = await repository.FindGoogleAsync(identity.Sub, ct);
        if (user is null)
        {
            if (await repository.EmailExistsAsync(identity.Email, ct)) throw new AuthenticationFailedException();
            user = new User { Email = identity.Email, FullName = identity.Name, PictureUrl = identity.Picture };
            repository.AddUser(user, new ExternalLogin { UserId = user.Id, User = user, ProviderKey = identity.Sub, RawProfileData = identity.Json });
        }
        if (user.Role != UserRole.Customer) throw new AuthenticationFailedException();
        return await IssueAsync(user, null, ct);
    }
    public async Task<AuthOutcome> AdminAsync(string email, string password, CancellationToken ct)
    {
        var user = await repository.FindAdminAsync(email.Trim().ToLowerInvariant(), ct);
        if (!passwords.Verify(user as AdminAccount ?? new AdminAccount(), password) || user is null) throw new AuthenticationFailedException();
        if (user is not AdminAccount { Role: UserRole.PlatformAdmin or UserRole.AgencyAdmin } admin) throw new AuthenticationFailedException();
        return await WithTwoFactorAsync(admin.Id, admin.Email, admin.Role, ct, () => IssueAsync(admin, twoFactor.Enabled ? SessionAccess.Full : null, ct));
    }
    public async Task<AuthOutcome> RegisterProviderAsync(string email, string password, string confirmPassword, CancellationToken ct)
    {
        if (password != confirmPassword) throw new ProfileException("Konfirmasi kata sandi tidak sama.");
        var normalized = email.Trim().ToLowerInvariant();
        if (await repository.EmailExistsAsync(normalized, ct) || await repository.FindProviderCredentialAsync(normalized, ct) is not null)
            throw new ProfileException("Email sudah terdaftar.", 409);
        var provider = new Provider { ApplicationStatus = ProviderApplicationStatus.Draft };
        provider.Credential = new ProviderCredential { ProviderId = provider.Id, Provider = provider, Email = normalized, PasswordHash = passwords.HashProvider(password) };
        repository.AddProvider(provider);
        await repository.SaveAsync(ct);
        return await IssueProviderAsync(provider.Credential, twoFactor.Enabled ? SessionAccess.Limited : null, ct);
    }
    public async Task<AuthOutcome> ProviderAsync(string email, string password, CancellationToken ct)
    {
        var credential = await repository.FindProviderCredentialAsync(email.Trim().ToLowerInvariant(), ct);
        if (credential is null || !passwords.VerifyProvider(credential.PasswordHash, password)) throw new AuthenticationFailedException();
        return await WithTwoFactorAsync(credential.ProviderId, credential.Email, UserRole.Provider, ct, () => IssueProviderAsync(credential, twoFactor.Enabled ? SessionAccess.Full : null, ct));
    }
    public async Task ChangeProviderPasswordAsync(Guid providerId, string currentPassword, string newPassword, CancellationToken ct)
    {
        var credential = await repository.FindProviderCredentialByProviderAsync(providerId, ct);
        if (credential is null || !passwords.VerifyProvider(credential.PasswordHash, currentPassword)) throw new AuthenticationFailedException();
        credential.PasswordHash = passwords.HashProvider(newPassword);
        credential.PasswordChangedAt = DateTimeOffset.UtcNow;
        await notifications.EmitPasswordChangedAsync(credential.ProviderId, credential.Email, credential.Provider.FullName, credential.PasswordChangedAt.Ticks.ToString(), credential.EmailVerifiedAt is not null, ct);
        await repository.SaveAsync(ct);
        await notifications.DispatchEnqueuedAsync(ct);
    }
    public async Task<AuthOutcome> RefreshAsync(string token, CancellationToken ct)
    {
        var session = await repository.ConsumeSessionAsync(tokens.Hash(token), ct);
        if (session is not null)
        {
            if (twoFactor.Enabled && session.Level is null && session.User.Role != UserRole.Customer)
            {
                await repository.SaveAsync(ct);
                throw new AuthenticationFailedException();
            }
            var level = session.Level ?? (twoFactor.Enabled && session.User.Role != UserRole.Customer ? SessionAccess.Full : null);
            return await IssueAsync(session.User, level, ct);
        }
        var providerSession = await repository.ConsumeProviderSessionAsync(tokens.Hash(token), ct);
        if (providerSession?.Provider.Credential is { } credential)
        {
            if (twoFactor.Enabled && providerSession.Level is null)
            {
                await repository.SaveAsync(ct);
                throw new AuthenticationFailedException();
            }
            return await IssueProviderAsync(credential, providerSession.Level ?? SessionAccess.Full, ct);
        }
        throw new AuthenticationFailedException();
    }
    public async Task LogoutAsync(string token, CancellationToken ct) { if (await repository.ConsumeSessionAsync(tokens.Hash(token), ct) is null) await repository.ConsumeProviderSessionAsync(tokens.Hash(token), ct); }
    public async Task<UserDto> MeAsync(Guid id, CancellationToken ct)
    {
        var user = await repository.FindUserAsync(id, ct);
        if (user is not null) return UserDto.From(user);
        var credential = await repository.FindProviderCredentialByProviderAsync(id, ct);
        if (credential?.Provider is not null && await repository.CanAuthenticateProviderAsync(id, ct)) return UserDto.FromProvider(credential.Provider, credential.Email);
        throw new AuthenticationFailedException();
    }
    public async Task<AuthOutcome> VerifyTwoFactorCodeAsync(Guid challengeId, string code, CancellationToken ct)
    {
        var challenge = await repository.FindTwoFactorChallengeAsync(challengeId, ct)
            ?? throw new ProfileException("Kode tidak valid.", 400, "2FA_INVALID");
        if (challenge.Requirement != TwoFactorRequirement.Login) throw new ProfileException("Kode tidak valid.", 400, "2FA_INVALID");
        await EnsureNotLockedAsync(challenge, ct);
        return await CompleteChallengeAsync(challenge, code, ct);
    }
    public async Task ResendTwoFactorCodeAsync(Guid challengeId, CancellationToken ct)
    {
        var challenge = await repository.FindTwoFactorChallengeAsync(challengeId, ct)
            ?? throw new ProfileException("Kode tidak valid.", 400, "2FA_INVALID");
        if (challenge.Requirement != TwoFactorRequirement.Login) throw new ProfileException("Kode tidak valid.", 400, "2FA_INVALID");
        await EnsureNotLockedAsync(challenge, ct);
        await EnforceResendLimitsAsync(challenge, ct);
        var code = otp.Generate();
        challenge.CodeHash = otp.Hash(code);
        challenge.ResentAt = DateTimeOffset.UtcNow;
        await repository.SaveAsync(ct);
        await otp.SendAsync(challenge.Email, code, twoFactor.CodeTtlMinutes, ct);
        security.LogInformation("2fa challenge_resent account={AccountId} role={Role} challenge={ChallengeId}", challenge.AccountId, challenge.Role, challenge.Id);
    }
    public async Task<AuthOutcome> SkipTwoFactorAsync(Guid challengeId, CancellationToken ct)
    {
        var challenge = await repository.FindTwoFactorChallengeAsync(challengeId, ct)
            ?? throw new ProfileException("Kode tidak valid.", 400, "2FA_INVALID");
        if (challenge.Requirement != TwoFactorRequirement.Login || challenge.VerifiedAt is not null
            || challenge.InvalidatedAt is not null || challenge.Role != UserRole.Provider || !twoFactor.ProviderPasswordFallback)
            throw new ProfileException("Opsi tidak tersedia.", 403, "2FA_SKIP_FORBIDDEN");
        if (challenge.LockedAt is not null) throw new ProfileException("Kode telah dikunci karena terlalu banyak percobaan.", 403, "2FA_LOCKED");
        var providerSession = await repository.FindProviderCredentialByProviderAsync(challenge.AccountId, ct);
        if (providerSession is null || !await repository.CanAuthenticateProviderAsync(challenge.AccountId, ct)) throw new AuthenticationFailedException();
        var state = await UpsertAccountStateAsync(challenge.AccountId, ct);
        var now = DateTimeOffset.UtcNow;
        if (state.SkipWindowStart is null || now - state.SkipWindowStart.Value >= TimeSpan.FromHours(1))
        {
            state.SkipWindowStart = now;
            state.SkipsInWindow = 0;
        }
        if (state.SkipsInWindow >= twoFactor.MaxSkipsPerHour)
            throw new ProfileException("Terlalu banyak sesi terbatas. Tunggu sebentar.", 429, "2FA_SKIP_LIMIT");
        state.SkipsInWindow++;
        challenge.InvalidatedAt = DateTimeOffset.UtcNow;
        await repository.SaveAsync(ct);
        security.LogInformation("2fa fallback_used account={AccountId} challenge={ChallengeId}", challenge.AccountId, challenge.Id);
        return await IssueProviderAsync(providerSession, SessionAccess.Limited, ct);
    }
    public async Task<TwoFactorLoginChallenge> StartProviderStepUpAsync(Guid providerId, string email, string jti, CancellationToken ct)
    {
        if (!twoFactor.Enabled) throw new ProfileException("Verifikasi dua langkah tidak aktif.", 409, "2FA_NOT_ENABLED");
        return await CreateAndSendAsync(providerId, email, UserRole.Provider, TwoFactorRequirement.StepUp, jti, ct);
    }
    public async Task<AuthOutcome> VerifyProviderStepUpAsync(Guid challengeId, string code, Guid providerId, string jti, string refreshToken, CancellationToken ct)
    {
        var challenge = await repository.FindTwoFactorChallengeAsync(challengeId, ct)
            ?? throw new ProfileException("Kode tidak valid.", 400, "2FA_INVALID");
        if (challenge.Requirement != TwoFactorRequirement.StepUp || challenge.Role != UserRole.Provider
            || challenge.AccountId != providerId || !string.Equals(challenge.SessionJti, jti, StringComparison.Ordinal))
            throw new ProfileException("Kode tidak berlaku untuk sesi ini.", 400, "2FA_INVALID");
        await EnsureNotLockedAsync(challenge, ct);
        var outcome = await CompleteChallengeAsync(challenge, code, ct);
        await repository.ConsumeProviderSessionAsync(tokens.Hash(refreshToken), ct);
        await repository.SaveAsync(ct);
        security.LogInformation("2fa step-up verified account={AccountId} challenge={ChallengeId}", challenge.AccountId, challenge.Id);
        return outcome;
    }
    private async Task<AuthOutcome> CompleteChallengeAsync(TwoFactorChallenge challenge, string code, CancellationToken ct)
    {
        var providerCredential = await repository.FindProviderCredentialByProviderAsync(challenge.AccountId, ct);
        if (challenge.Role == UserRole.Provider && (providerCredential is null || !await repository.CanAuthenticateProviderAsync(challenge.AccountId, ct)))
            throw new AuthenticationFailedException();
        if (otp.Verify(code, challenge.CodeHash))
        {
            if (providerCredential is not null && challenge.Role == UserRole.Provider)
                providerCredential.EmailVerifiedAt = DateTimeOffset.UtcNow;
            challenge.VerifiedAt = DateTimeOffset.UtcNow;
            challenge.InvalidatedAt = DateTimeOffset.UtcNow;
            var session = await IssueForChallengeAsync(challenge, ct);
            await repository.SaveAsync(ct);
            security.LogInformation("2fa challenge_verified account={AccountId} role={Role} challenge={ChallengeId}", challenge.AccountId, challenge.Role, challenge.Id);
            return session;
        }
        challenge.Attempts++;
        if (challenge.Attempts >= challenge.MaxAttempts)
        {
            challenge.LockedAt = DateTimeOffset.UtcNow;
            await repository.SaveAsync(ct);
            security.LogWarning("2fa challenge_locked account={AccountId} role={Role} challenge={ChallengeId}", challenge.AccountId, challenge.Role, challenge.Id);
            throw new ProfileException("Terlalu banyak percobaan salah.", 429, "2FA_LOCKED");
        }
        await repository.SaveAsync(ct);
        security.LogInformation("2fa challenge_failed account={AccountId} role={Role} challenge={ChallengeId} attempts={Attempts}", challenge.AccountId, challenge.Role, challenge.Id, challenge.Attempts);
        throw new ProfileException("Kode tidak benar.", 400, "2FA_INVALID");
    }
    private async Task EnsureNotLockedAsync(TwoFactorChallenge challenge, CancellationToken ct)
    {
        if (challenge.LockedAt is not null) throw new ProfileException("Terlalu banyak percobaan salah.", 429, "2FA_LOCKED");
        if (challenge.VerifiedAt is not null || challenge.InvalidatedAt is not null) throw new ProfileException("Kode tidak valid.", 400, "2FA_INVALID");
        if (DateTimeOffset.UtcNow > challenge.ExpiresAt) throw new ProfileException("Kode telah kedaluwarsa.", 400, "2FA_EXPIRED");
    }
    private async Task<AuthOutcome> WithTwoFactorAsync(Guid accountId, string email, UserRole role, CancellationToken ct, Func<Task<AuthOutcome>> session)
    {
        if (!twoFactor.Enabled) return await session();
        var challenge = await CreateAndSendAsync(accountId, email, role, TwoFactorRequirement.Login, null, ct);
        return new ChallengeOutcome(challenge);
    }
    private async Task<TwoFactorLoginChallenge> CreateAndSendAsync(Guid accountId, string email, UserRole role, TwoFactorRequirement requirement, string? sessionJti, CancellationToken ct)
    {
        await repository.InvalidateTwoFactorChallengesAsync(accountId, role, requirement, ct);
        var code = otp.Generate();
        var challenge = new TwoFactorChallenge
        {
            AccountId = accountId,
            Role = role,
            Requirement = requirement,
            Email = email,
            MaskedEmail = otp.Mask(email),
            CodeHash = otp.Hash(code),
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(twoFactor.CodeTtlMinutes),
            MaxAttempts = twoFactor.MaxAttempts,
            SessionJti = sessionJti
        };
        repository.AddTwoFactorChallenge(challenge);
        await repository.SaveAsync(ct);
        await otp.SendAsync(email, code, twoFactor.CodeTtlMinutes, ct);
        security.LogInformation("2fa challenge_issued account={AccountId} role={Role} requirement={Requirement} challenge={ChallengeId}", challenge.AccountId, challenge.Role, challenge.Requirement, challenge.Id);
        return new(true, challenge.Id, "email", challenge.MaskedEmail, role == UserRole.Provider && twoFactor.ProviderPasswordFallback);
    }
    private async Task<TwoFactorAccountState> UpsertAccountStateAsync(Guid accountId, CancellationToken ct)
    {
        var state = await repository.FindTwoFactorAccountStateAsync(accountId, ct);
        if (state is not null) return state;
        state = new TwoFactorAccountState { AccountId = accountId };
        repository.AddTwoFactorAccountState(state);
        return state;
    }
    private static bool lastHour(TwoFactorAccountState state) => state.ResendWindowStart is not null && DateTimeOffset.UtcNow - state.ResendWindowStart.Value < TimeSpan.FromHours(1);
    private async Task EnforceResendLimitsAsync(TwoFactorChallenge challenge, CancellationToken ct)
    {
        var state = await UpsertAccountStateAsync(challenge.AccountId, ct);
        var now = DateTimeOffset.UtcNow;
        if (challenge.ResentAt is not null && now - challenge.ResentAt.Value < TimeSpan.FromSeconds(twoFactor.ResendCooldownSeconds))
            throw new ProfileException("Tunggu sebentar sebelum meminta kode baru.", 429, "2FA_RESEND_COOLDOWN");
        if (!lastHour(state)) { state.ResendWindowStart = now; state.ResendsInWindow = 0; }
        if (state.ResendsInWindow >= twoFactor.MaxResendsPerHour)
            throw new ProfileException("Terlalu banyak permintaan kode. Coba lagi nanti.", 429, "2FA_RESEND_LIMIT");
        state.ResendsInWindow++;
    }
    private async Task<AuthOutcome> IssueForChallengeAsync(TwoFactorChallenge challenge, CancellationToken ct)
    {
        if (challenge.Role == UserRole.Provider)
        {
            var credential = await repository.FindProviderCredentialByProviderAsync(challenge.AccountId, ct);
            if (credential is null || !await repository.CanAuthenticateProviderAsync(challenge.AccountId, ct)) throw new AuthenticationFailedException();
            return await IssueProviderAsync(credential, SessionAccess.Full, ct);
        }
        var user = await repository.FindUserAsync(challenge.AccountId, ct);
        if (user is null || user.Role != challenge.Role) throw new AuthenticationFailedException();
        return await IssueAsync(user, SessionAccess.Full, ct);
    }
    private async Task<AuthOutcome> IssueAsync(User user, SessionAccess? level, CancellationToken ct)
    {
        if (!await repository.CanAuthenticateAsync(user, ct)) throw new AuthenticationFailedException();
        var access = tokens.Create(user, level); var refresh = tokens.NewRefreshToken(); var expiry = DateTimeOffset.UtcNow.AddDays(tokens.RefreshDays);
        repository.AddSession(new RefreshSession { UserId = user.Id, TokenHash = tokens.Hash(refresh), ExpiresAt = expiry, Level = level });
        await repository.SaveAsync(ct);
        return new SessionOutcome(new(access.Value, access.ExpiresAt, UserDto.From(user), refresh, expiry));
    }
    private async Task<AuthOutcome> IssueProviderAsync(ProviderCredential credential, SessionAccess? level, CancellationToken ct)
    {
        if (!await repository.CanAuthenticateProviderAsync(credential.ProviderId, ct)) throw new AuthenticationFailedException();
        var access = tokens.Create(credential.Provider, credential.Email, level); var refresh = tokens.NewRefreshToken(); var expiry = DateTimeOffset.UtcNow.AddDays(tokens.RefreshDays);
        repository.AddProviderSession(new ProviderRefreshSession { ProviderId = credential.ProviderId, Provider = credential.Provider, TokenHash = tokens.Hash(refresh), ExpiresAt = expiry, Level = level });
        await repository.SaveAsync(ct);
        return new SessionOutcome(new(access.Value, access.ExpiresAt, UserDto.FromProvider(credential.Provider, credential.Email), refresh, expiry));
    }
}