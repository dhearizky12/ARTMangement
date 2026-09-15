using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BantuBantu.Application;
using BantuBantu.Domain;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
namespace BantuBantu.Infrastructure;

public class GoogleIdentityVerifier(IConfiguration config) : IGoogleIdentityVerifier
{
    public async Task<GoogleIdentity> VerifyAsync(string credential)
    {
        try
        {
            var p = await GoogleJsonWebSignature.ValidateAsync(credential, new GoogleJsonWebSignature.ValidationSettings { Audience = [config["Google:ClientId"]!] });
            if (!p.EmailVerified || string.IsNullOrWhiteSpace(p.Subject) || string.IsNullOrWhiteSpace(p.Email)) throw new AuthenticationFailedException();
            return new(p.Subject, p.Email.Trim().ToLowerInvariant(), p.Name ?? p.Email, p.Picture, JsonSerializer.Serialize(p));
        }
        catch (Exception e) when (e is InvalidJwtException or Newtonsoft.Json.JsonException or FormatException or ArgumentException) { throw new AuthenticationFailedException(); }
    }
}
public sealed class RsaKeys : IDisposable
{
    private readonly RSA privateRsa = RSA.Create();
    private readonly RSA publicRsa = RSA.Create();
    public RsaSecurityKey SigningKey { get; }
    public RsaSecurityKey ValidationKey { get; }
    public RsaKeys(IConfiguration config)
    {
        privateRsa.ImportFromPem(File.ReadAllText(config["Jwt:PrivateKeyPath"]!));
        publicRsa.ImportFromPem(File.ReadAllText(config["Jwt:PublicKeyPath"]!));
        if (privateRsa.KeySize < 2048 || !privateRsa.ExportSubjectPublicKeyInfo().SequenceEqual(publicRsa.ExportSubjectPublicKeyInfo())) throw new InvalidOperationException("RSA keys must match and have at least 2048 bits.");
        SigningKey = new(privateRsa) { KeyId = config["Jwt:KeyId"] };
        ValidationKey = new(publicRsa) { KeyId = config["Jwt:KeyId"] };
    }
    public void Dispose() { privateRsa.Dispose(); publicRsa.Dispose(); }
}
public class TokenService(IConfiguration config, RsaKeys keys) : ITokenService
{
    public int RefreshDays => int.Parse(config["Jwt:RefreshDays"]!);
    public AccessToken Create(User user)
    {
        var now = DateTimeOffset.UtcNow; var expiry = now.AddMinutes(int.Parse(config["Jwt:AccessMinutes"]!));
        Claim[] claims = [new("sub", user.Id.ToString()), new("email", user.Email), new("role", user.Role.ToString()), new("profileCompleted", user.ProfileCompleted ? "true" : "false"), new("jti", Guid.NewGuid().ToString())];
        var jwt = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims, now.UtcDateTime, expiry.UtcDateTime, new SigningCredentials(keys.SigningKey, SecurityAlgorithms.RsaSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expiry);
    }
    public string NewRefreshToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
    public string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
public class PasswordService : IPasswordService
{
    private readonly PasswordHasher<User> hasher = new();
    private readonly string dummy;
    public PasswordService() { dummy = hasher.HashPassword(new User(), Convert.ToHexString(RandomNumberGenerator.GetBytes(32))); }
    public string Hash(User user, string password) => hasher.HashPassword(user, password);
    public bool Verify(User user, string password) => hasher.VerifyHashedPassword(user, user.PasswordHash ?? dummy, password) != PasswordVerificationResult.Failed && user.PasswordHash is not null;
}
