using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using BantuBantu.Application;
using BantuBantu.Domain;
using BantuBantu.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
namespace BantuBantu.Tests;

public partial class AuthIntegrationTests
{
    [Fact]
    public async Task PostgreSqlAuthLifecycleAndRoleBoundaries()
    {
        using var factory = new AuthFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            Assert.DoesNotContain(db.Model.FindEntityType(typeof(UserProfile))!.GetProperties(), p => p.IsShadowProperty());
            var user = new User { Email = "admin@example.test", FullName = "Test admin", Role = UserRole.Admin, ProfileCompleted = true };
            user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(user, "Test-password-long-42!");
            db.Users.Add(user); await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/admin/login", new { email = "admin@example.test", password = "wrong" })).StatusCode);
        var login = await client.PostAsJsonAsync("/api/auth/admin/login", new { email = "admin@example.test", password = "Test-password-long-42!" });
        login.EnsureSuccessStatusCode();
        var admin = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(admin.AccessToken);
        Assert.Equal("RS256", jwt.Header.Alg); Assert.Equal("Admin", admin.User.Role);
        using var rsa = RSA.Create(); rsa.ImportFromPem(File.ReadAllText(factory.PublicPath));
        var parameters = new TokenValidationParameters { IssuerSigningKey = new RsaSecurityKey(rsa), ValidateIssuerSigningKey = true, ValidIssuer = "test-issuer", ValidAudience = "test-audience", ValidateLifetime = true, ValidAlgorithms = ["RS256"] };
        new JwtSecurityTokenHandler().ValidateToken(admin.AccessToken, parameters, out _);
        var wrongAudience = parameters.Clone(); wrongAudience.ValidAudience = "wrong";
        Assert.Throws<SecurityTokenInvalidAudienceException>(() => new JwtSecurityTokenHandler().ValidateToken(admin.AccessToken, wrongAudience, out _));
        var wrongIssuer = parameters.Clone(); wrongIssuer.ValidIssuer = "wrong";
        Assert.Throws<SecurityTokenInvalidIssuerException>(() => new JwtSecurityTokenHandler().ValidateToken(admin.AccessToken, wrongIssuer, out _));
        var cookie = login.Headers.GetValues("Set-Cookie").Single(); Assert.Contains("httponly", cookie.ToLowerInvariant());
        var refreshCookie = cookie.Split(';')[0];
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/user/dashboard")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken[..^8] + "AAAAAAAA");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var keys = scope.ServiceProvider.GetRequiredService<RsaKeys>();
            var expired = new JwtSecurityToken("test-issuer", "test-audience", [], DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), new SigningCredentials(keys.SigningKey, SecurityAlgorithms.RsaSha256));
            client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(expired));
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/.well-known/jwks.json")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Add("Cookie", refreshCookie);
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { }); refresh.EnsureSuccessStatusCode();
        var newCookie = refresh.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        Assert.NotEqual(refreshCookie, newCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { })).StatusCode);
        client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", newCookie);
        client.DefaultRequestHeaders.Remove("Origin"); client.DefaultRequestHeaders.Add("Origin", "https://attacker.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/auth/refresh", new { })).StatusCode);
        client.DefaultRequestHeaders.Remove("Origin"); client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { })).StatusCode);
        client.DefaultRequestHeaders.Remove("Cookie");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/google", new { credential = "invalid" })).StatusCode);
        var google = await client.PostAsJsonAsync("/api/auth/google", new { credential = "verified-test-identity" }); google.EnsureSuccessStatusCode();
        var userSession = (await google.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Equal("User", userSession.User.Role); Assert.False(userSession.User.ProfileCompleted);
        client.DefaultRequestHeaders.Authorization = new("Bearer", userSession.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/user/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/dashboard")).StatusCode);
        var again = await client.PostAsJsonAsync("/api/auth/google", new { credential = "verified-test-identity" });
        Assert.Equal(userSession.User.Id, (await again.Content.ReadFromJsonAsync<AuthResponse>())!.User.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/google", new { credential = "admin-email" })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.ExternalLogins.CountAsync());
            Assert.Equal(2, await db.Users.CountAsync());
            Assert.All(await db.RefreshSessions.ToListAsync(), s => Assert.Equal(64, s.TokenHash.Length));
        }
        await ProfileLifecycle(client, factory, userSession, admin.AccessToken);
    }
    [Fact]
    public async Task RealGoogleVerifierRejectsForgedToken()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Google:ClientId"] = "test.apps.googleusercontent.com" }).Build();
        await Assert.ThrowsAsync<AuthenticationFailedException>(() => new GoogleIdentityVerifier(config).VerifyAsync("not.a.valid-token"));
    }
}
public sealed class AuthFactory : WebApplicationFactory<Program>
{
    public const string Origin = "http://localhost:5173";
    private readonly string directory = Path.Combine(Path.GetTempPath(), "bantubantu-tests-" + Guid.NewGuid());
    public string DocumentPath => Path.Combine(directory, "documents");
    public string PublicPath => Path.Combine(directory, "public.pem");
    private readonly string connection;
    public AuthFactory()
    {
        connection = Environment.GetEnvironmentVariable("BANTUBANTU_TEST_DB") ?? throw new InvalidOperationException("Set BANTUBANTU_TEST_DB to a dedicated EMPTY PostgreSQL test database.");
        Directory.CreateDirectory(directory); using var rsa = RSA.Create(2048);
        File.WriteAllText(PublicPath, rsa.ExportSubjectPublicKeyInfoPem()); File.WriteAllText(Path.Combine(directory, "private.pem"), rsa.ExportRSAPrivateKeyPem());
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        var config = new Dictionary<string, string?> { ["ConnectionStrings:Default"] = connection, ["Storage:RootPath"] = Path.Combine(directory, "documents"), ["Google:ClientId"] = "test.apps.googleusercontent.com", ["Jwt:PrivateKeyPath"] = Path.Combine(directory, "private.pem"), ["Jwt:PublicKeyPath"] = PublicPath, ["Jwt:KeyId"] = "test-key", ["Jwt:Issuer"] = "test-issuer", ["Jwt:Audience"] = "test-audience", ["Jwt:AccessMinutes"] = "15", ["Jwt:RefreshDays"] = "7", ["Frontend:Origin"] = Origin, ["Auth:CookieSecure"] = "false", ["Auth:CookieSameSite"] = "Lax" };
        foreach (var entry in config) builder.UseSetting(entry.Key, entry.Value);
        builder.ConfigureServices(services => { services.RemoveAll<IGoogleIdentityVerifier>(); services.AddSingleton<IGoogleIdentityVerifier, FakeGoogleVerifier>(); });
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing); if (disposing && Directory.Exists(directory)) Directory.Delete(directory, true); }
    private class FakeGoogleVerifier : IGoogleIdentityVerifier
    {
        public Task<GoogleIdentity> VerifyAsync(string credential) => credential switch
        {
            "verified-test-identity" => Task.FromResult(new GoogleIdentity("google-sub-123", "user@example.test", "Google User", "https://example.test/photo", "{\"sub\":\"google-sub-123\"}")),
            "admin-email" => Task.FromResult(new GoogleIdentity("other-sub", "admin@example.test", "Impostor", null, "{}")),
            _ => throw new AuthenticationFailedException()
        };
    }
}
