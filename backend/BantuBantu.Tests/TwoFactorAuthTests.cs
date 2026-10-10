using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using BantuBantu.Application;
using BantuBantu.Domain;
using BantuBantu.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace BantuBantu.Tests;

/// <summary>2FA end-to-end tests. They use a real RSA key pair, the real
/// AuthService, the real RequireMfa gating, and a predictable in-memory OTP
/// captor so a wrong code is distinguishable from the right code.</summary>
public partial class AuthIntegrationTests
{
    [Fact]
    public async Task AdminLoginReturnsChallengeAndCompletes()
    {
        using var factory = new TwoFactorAuthFactory();
        await factory.MigrateAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);

        var login = await client.PostAsJsonAsync("/api/auth/admin/login", new { email = "admin@example.test", password = "Test-password-long-42!" });
        login.EnsureSuccessStatusCode();
        var challenge = (await login.Content.ReadFromJsonAsync<TwoFactorLoginChallenge>())!;
        Assert.True(challenge.RequiresTwoFactor);
        Assert.Equal("email", challenge.Method);
        Assert.Equal("a***@example.test", challenge.MaskedEmail);
        Assert.False(challenge.FallbackAllowed);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/2fa/resend", new { challengeId = challenge.ChallengeId })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/2fa/resend", new { challengeId = challenge.ChallengeId })).StatusCode);

        var session = await VerifyAsync(client, challenge.ChallengeId, factory.LastCode);
        Assert.True(session.HasAmrEmailOtp, "full session token must carry amr=email_otp");
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
    }

    [Fact]
    public async Task AllFiveWrongAttemptsLockChallengeEvenWithRightCode()
    {
        using var factory = new TwoFactorAuthFactory();
        await factory.MigrateAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);

        var challengeId = await LoginChallengeAsync(client);
        for (var i = 0; i < 4; i++)
        {
            var (status, code) = await VerifyErrorAsync(client, challengeId, "000000");
            Assert.Equal(HttpStatusCode.BadRequest, status);
            Assert.Equal("2FA_INVALID", code);
        }
        var locked = await VerifyErrorAsync(client, challengeId, "000000");
        Assert.Equal(HttpStatusCode.TooManyRequests, locked.status);
        Assert.Equal("2FA_LOCKED", locked.code);
        var rightCodeStillLocked = await VerifyErrorAsync(client, challengeId, factory.Code);
        Assert.Equal(HttpStatusCode.TooManyRequests, rightCodeStillLocked.status);
        Assert.Equal("2FA_LOCKED", rightCodeStillLocked.code);
    }

    [Fact]
    public async Task ExpiredChallengeIsRejected()
    {
        using var factory = new TwoFactorAuthFactory();
        await factory.MigrateAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);

        var challengeId = await LoginChallengeAsync(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.TwoFactorChallenges.Where(x => x.Id == challengeId).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }
        var expired = await VerifyErrorAsync(client, challengeId, factory.Code);
        Assert.Equal(HttpStatusCode.BadRequest, expired.status);
        Assert.Equal("2FA_EXPIRED", expired.code);
    }

    [Fact]
    public async Task ProviderFallbackSkipIssuesLimitedThenStepUpUpgrades()
    {
        using var factory = new TwoFactorAuthFactory();
        await factory.MigrateAsync();
        await factory.EnsureProviderAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);

        var login = await client.PostAsJsonAsync("/api/auth/provider/login", new { email = "provider@example.test", password = "Test-password-long-42!" });
        login.EnsureSuccessStatusCode();
        var challenge = (await login.Content.ReadFromJsonAsync<TwoFactorLoginChallenge>())!;
        Assert.True(challenge.RequiresTwoFactor);
        Assert.True(challenge.FallbackAllowed);

        var skip = await client.PostAsJsonAsync("/api/auth/2fa/skip", new { challengeId = challenge.ChallengeId });
        skip.EnsureSuccessStatusCode();
        var limited = (await skip.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.False(Session.From(limited).HasAmrEmailOtp);
        Assert.True(Session.From(limited).IsLimited, "fallback session must carry limited claim");

        client.DefaultRequestHeaders.Authorization = new("Bearer", limited.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/provider/profile")).StatusCode);
        var blocked = await client.PostAsJsonAsync("/api/provider/application/submit", new { });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var blockBody = (await blocked.Content.ReadFromJsonAsync<ApiError>())!;
        Assert.Equal("mfa_required", blockBody.code);

        var start = await client.PostAsJsonAsync("/api/provider/step-up/start", new { });
        start.EnsureSuccessStatusCode();
        var stepUp = (await start.Content.ReadFromJsonAsync<TwoFactorLoginChallenge>())!;
        Assert.Equal("p***@example.test", stepUp.MaskedEmail);
        var wrong = await client.PostAsJsonAsync("/api/provider/step-up/verify", new { challengeId = stepUp.ChallengeId, code = "000000", refreshToken = limited.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var verify = await client.PostAsJsonAsync("/api/provider/step-up/verify", new { challengeId = stepUp.ChallengeId, code = factory.LastCode, refreshToken = limited.RefreshToken });
        verify.EnsureSuccessStatusCode();
        var full = (await verify.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.True(Session.From(full).HasAmrEmailOtp);
        Assert.False(Session.From(full).IsLimited);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = limited.RefreshToken })).StatusCode);

        client.DefaultRequestHeaders.Authorization = new("Bearer", full.AccessToken);
        var after = await client.PostAsJsonAsync("/api/provider/personal-info", new { });
        Assert.NotEqual(HttpStatusCode.Forbidden, after.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, after.StatusCode);

        Assert.NotNull(await factory.EmailVerifiedAtAsync());
    }

    [Fact]
    public async Task ResendInvalidatesPreviousCodeAndIsCooldownLimited()
    {
        using var factory = new TwoFactorAuthFactory();
        await factory.MigrateAsync();
        await factory.EnsureProviderAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);

        var challengeId = await LoginChallengeAsync(client);
        var firstCode = factory.Code;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/2fa/resend", new { challengeId })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/2fa/resend", new { challengeId })).StatusCode);
        var stale = await VerifyErrorAsync(client, challengeId, firstCode);
        Assert.Equal(HttpStatusCode.BadRequest, stale.status);
        Assert.Equal("2FA_INVALID", stale.code);
        var session = await VerifyAsync(client, challengeId, factory.LastCode);
        Assert.True(session.HasAmrEmailOtp);
        Assert.Contains("provider@example.test", factory.Sent);
    }

    [Fact]
    public async Task ProviderRegistrationIssuesLimitedSession()
    {
        using var factory = new TwoFactorAuthFactory();
        await factory.MigrateAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);

        var register = await client.PostAsJsonAsync("/api/auth/provider/register", new { email = $"new-{Guid.NewGuid():N}@example.test", password = "Test-password-long-42!", confirmPassword = "Test-password-long-42!" });
        register.EnsureSuccessStatusCode();
        var session = (await register.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.True(Session.From(session).IsLimited);
        Assert.False(Session.From(session).HasAmrEmailOtp);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/provider/application/submit", new { })).StatusCode);
    }

    [Fact]
    public async Task NoneLevelAccessTokensAreRejectedWhileEnabled()
    {
        using var factory = new TwoFactorAuthFactory();
        await factory.MigrateAsync();
        await factory.EnsureProviderAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);

        var (adminToken, providerToken) = factory.FabricatePreRolloutTokens();
        client.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", providerToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/provider/profile")).StatusCode);
    }

    [Fact]
    public async Task StepUpChallengeIsBoundToTheIssuingAccessToken()
    {
        using var factory = new TwoFactorAuthFactory();
        await factory.MigrateAsync();
        await factory.EnsureProviderAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);

        var challenge = await LoginChallengeAsync(client);
        var limited = (await (await client.PostAsJsonAsync("/api/auth/2fa/skip", new { challengeId = challenge })).Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", limited.AccessToken);
        var started = await client.PostAsJsonAsync("/api/provider/step-up/start", new { });
        started.EnsureSuccessStatusCode();
        var stepUp = (await started.Content.ReadFromJsonAsync<TwoFactorLoginChallenge>())!;

        // Rotate the access token: the step-up challenge is bound to the old jti.
        var refreshedResponse = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = limited.RefreshToken });
        refreshedResponse.EnsureSuccessStatusCode();
        var current = (await refreshedResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", current.AccessToken);
        var rejected = await client.PostAsJsonAsync("/api/provider/step-up/verify", new { challengeId = stepUp.ChallengeId, code = factory.Code, refreshToken = current.RefreshToken });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        // A fresh challenge issued to the current token still works.
        var restarted = await client.PostAsJsonAsync("/api/provider/step-up/start", new { });
        restarted.EnsureSuccessStatusCode();
        var second = (await restarted.Content.ReadFromJsonAsync<TwoFactorLoginChallenge>())!;
        var verified = await client.PostAsJsonAsync("/api/provider/step-up/verify", new { challengeId = second.ChallengeId, code = factory.LastCode, refreshToken = current.RefreshToken });
        verified.EnsureSuccessStatusCode();
        Assert.True(Session.From((await verified.Content.ReadFromJsonAsync<AuthResponse>())!).HasAmrEmailOtp);
    }

    private static async Task<Guid> LoginChallengeAsync(HttpClient client)
    {
        var login = await client.PostAsJsonAsync("/api/auth/provider/login", new { email = "provider@example.test", password = "Test-password-long-42!" });
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<TwoFactorLoginChallenge>())!.ChallengeId;
    }
    private static async Task<Session> VerifyAsync(HttpClient client, Guid challengeId, string code)
    {
        var response = await client.PostAsJsonAsync("/api/auth/2fa/verify", new { challengeId, code });
        response.EnsureSuccessStatusCode();
        return Session.From((await response.Content.ReadFromJsonAsync<AuthResponse>())!);
    }
    private static async Task<(HttpStatusCode status, string? code)> VerifyErrorAsync(HttpClient client, Guid challengeId, string code)
    {
        var response = await client.PostAsJsonAsync("/api/auth/2fa/verify", new { challengeId, code });
        var body = await response.Content.ReadFromJsonAsync<ApiError>();
        return (response.StatusCode, body?.code);
    }
}

internal sealed record ApiError(string? title, int status, string? code);

internal sealed record Session(string Token, IReadOnlyList<string> ClaimTypes, IReadOnlyList<string> ClaimValues, string RefreshToken)
{
    public static Session From(AuthResponse a)
    {
        var claims = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(a.AccessToken).Claims.ToArray();
        return new(a.AccessToken, claims.Select(c => c.Type).ToArray(), claims.Select(c => c.Value).ToArray(), a.RefreshToken);
    }
    public bool HasAmrEmailOtp => ClaimValues.Contains("email_otp");
    public bool IsLimited => ClaimTypes.Contains("limited");
}

public sealed class TwoFactorAuthFactory : AuthFactory
{
    private static readonly string Connection2Fa = Environment.GetEnvironmentVariable("BANTUBANTU_2FA_TEST_DB")
        ?? Environment.GetEnvironmentVariable("BANTUBANTU_TEST_DB")
        ?? throw new InvalidOperationException("Set BANTUBANTU_2FA_TEST_DB to a dedicated EMPTY PostgreSQL test database.");
    private readonly CapturingOtp otp = new();
    public string Code => "481516";
    public string LastCode => otp.Codes[^1];
    public IReadOnlyList<string> Sent => otp.Sent;
    public TwoFactorAuthFactory() : base() { }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:Default", Connection2Fa);
        builder.UseSetting("TwoFactor:Enabled", "true");
        builder.UseSetting("TwoFactor:ProviderPasswordFallback", "true");
        builder.UseSetting("TwoFactor:CodeTtlMinutes", "10");
        builder.UseSetting("TwoFactor:MaxAttempts", "5");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailOtpService>();
            services.AddSingleton<IEmailOtpService>(otp);
        });
    }
    public async Task MigrateAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        var passwords = scope.ServiceProvider.GetRequiredService<IPasswordService>();
        if (!await db.AdminAccounts.AnyAsync(x => x.Email == "admin@example.test"))
        {
            var admin = new AdminAccount { Email = "admin@example.test", FullName = "2FA admin", Role = UserRole.PlatformAdmin, ProfileCompleted = true };
            admin.PasswordHash = passwords.Hash(admin, "Test-password-long-42!");
            db.AdminAccounts.Add(admin);
        }
        await db.SaveChangesAsync();
    }
    public async Task EnsureProviderAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (!await db.Providers.AnyAsync(x => x.Credential != null && x.Credential.Email == "provider@example.test"))
        {
            var provider = new Provider { FullName = "2FA provider", ApplicationStatus = ProviderApplicationStatus.Approved, VerificationStatus = VerificationStatus.Verified, IdentityVerified = true, BackgroundCheckPassed = true, ContractSigned = true };
            provider.Credential = new ProviderCredential { Email = "provider@example.test", PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().HashProvider("Test-password-long-42!") };
            db.Providers.Add(provider);
        }
        await db.SaveChangesAsync();
    }
    public async Task<DateTimeOffset?> EmailVerifiedAtAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ProviderCredentials.Where(x => x.Email == "provider@example.test").Select(x => x.EmailVerifiedAt).SingleOrDefaultAsync();
    }
    /// <summary>Pre-rollout tokens: real identities, real roles, but no 2FA
    /// session-level claims (Level was null before this feature shipped).</summary>
    public (string admin, string provider) FabricatePreRolloutTokens()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var keys = scope.ServiceProvider.GetRequiredService<RsaKeys>();
        var adminId = db.AdminAccounts.Where(x => x.Email == "admin@example.test").Select(x => x.Id).Single();
        var providerId = db.ProviderCredentials.Where(x => x.Email == "provider@example.test").Select(x => x.ProviderId).Single();
        string Make(string role, Guid sub, string email, string providerIdClaim)
        {
            var claims = new List<Claim>
            {
                new("sub", sub.ToString()), new("email", email), new("role", role),
                new("profileCompleted", "true"), new("jti", Guid.NewGuid().ToString())
            };
            if (!string.IsNullOrWhiteSpace(providerIdClaim)) claims.Add(new Claim("providerId", providerIdClaim));
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken("test-issuer", "test-audience", claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15),
                new Microsoft.IdentityModel.Tokens.SigningCredentials(keys.SigningKey, Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256));
            return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(jwt);
        }
        return (Make("PlatformAdmin", adminId, "admin@example.test", ""), Make("Provider", providerId, "provider@example.test", providerId.ToString()));
    }
}

/// <summary>Deterministic two-step code so integration tests can exercise wrong
/// vs right codes. Real HKDF/HMAC behaviour is covered by the unit tests.</summary>
public sealed class CapturingOtp : IEmailOtpService
{
    public readonly List<string> Sent = [];
    public readonly List<string> Codes = [];
    private string code = "481516";
    public string Generate()
    {
        var next = code;
        code = code == "481516" ? "612083" : "481516";
        Codes.Add(next);
        return next;
    }
    public string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public bool Verify(string value, string storedHashHex) => string.Equals(Hash(value), storedHashHex, StringComparison.OrdinalIgnoreCase);
    public string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[..1]}***@{email[(at + 1)..]}";
    }
    public Task SendAsync(string to, string code, int ttlMinutes, CancellationToken ct) { Sent.Add(to); return Task.CompletedTask; }
}