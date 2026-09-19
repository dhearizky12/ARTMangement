using System.ComponentModel.DataAnnotations;
using BantuBantu.Domain;
namespace BantuBantu.Application;

public record GoogleRequest([Required, MaxLength(10000)] string Credential);
public record AdminRequest([Required, EmailAddress] string Email, [Required, MaxLength(256)] string Password);
public record RefreshRequest([Required, MaxLength(256)] string RefreshToken);
public record GoogleIdentity(string Sub, string Email, string Name, string? Picture, string Json);
public record UserDto(Guid Id, string Email, string FullName, string? PictureUrl, string Role, bool ProfileCompleted, string ProfileStep, Guid? AgencyId = null)
{
    public static UserDto From(User u) => new(u.Id, u.Email, u.FullName, u.PictureUrl, u.Role.ToString(), u.ProfileCompleted, u.ProfileCompleted ? "done" : u.ProfileStep, (u as AdminAccount)?.AgencyId);
}
public record AccessToken(string Value, DateTimeOffset ExpiresAt);
public record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User, string RefreshToken, DateTimeOffset RefreshExpiresAt);
public record AuthResult(AuthResponse Response);
public class AuthenticationFailedException : Exception { }
public interface IGoogleIdentityVerifier { Task<GoogleIdentity> VerifyAsync(string credential); }
public interface ITokenService
{
    AccessToken Create(User user);
    string NewRefreshToken();
    string Hash(string token);
    int RefreshDays { get; }
}
public interface IPasswordService { string Hash(AdminAccount user, string password); bool Verify(AdminAccount user, string password); }
public interface IAuthRepository
{
    Task<User?> FindGoogleAsync(string sub, CancellationToken ct);
    Task<User?> FindAdminAsync(string email, CancellationToken ct);
    Task<User?> FindUserAsync(Guid id, CancellationToken ct);
    Task<bool> CanAuthenticateAsync(User user, CancellationToken ct);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct);
    void AddUser(User user, ExternalLogin? login = null);
    void AddSession(RefreshSession session);
    Task<RefreshSession?> ConsumeSessionAsync(string hash, CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
public interface IAuthService
{
    Task<AuthResult> GoogleAsync(string credential, CancellationToken ct);
    Task<AuthResult> AdminAsync(string email, string password, CancellationToken ct);
    Task<AuthResult> RefreshAsync(string token, CancellationToken ct);
    Task LogoutAsync(string token, CancellationToken ct);
    Task<UserDto> MeAsync(Guid id, CancellationToken ct);
}
public class AuthService(IAuthRepository repository, IGoogleIdentityVerifier google, ITokenService tokens, IPasswordService passwords) : IAuthService
{
    public async Task<AuthResult> GoogleAsync(string credential, CancellationToken ct)
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
        return await IssueAsync(user, ct);
    }
    public async Task<AuthResult> AdminAsync(string email, string password, CancellationToken ct)
    {
        var user = await repository.FindAdminAsync(email.Trim().ToLowerInvariant(), ct);
        if (!passwords.Verify(user as AdminAccount ?? new AdminAccount(), password) || user is null) throw new AuthenticationFailedException();
        return await IssueAsync(user, ct);
    }
    public async Task<AuthResult> RefreshAsync(string token, CancellationToken ct)
    {
        var session = await repository.ConsumeSessionAsync(tokens.Hash(token), ct);
        if (session is null) throw new AuthenticationFailedException();
        return await IssueAsync(session.User, ct);
    }
    public async Task LogoutAsync(string token, CancellationToken ct) { await repository.ConsumeSessionAsync(tokens.Hash(token), ct); }
    public async Task<UserDto> MeAsync(Guid id, CancellationToken ct) => UserDto.From(await repository.FindUserAsync(id, ct) ?? throw new AuthenticationFailedException());
    private async Task<AuthResult> IssueAsync(User user, CancellationToken ct)
    {
        if (!await repository.CanAuthenticateAsync(user, ct)) throw new AuthenticationFailedException();
        var access = tokens.Create(user); var refresh = tokens.NewRefreshToken(); var expiry = DateTimeOffset.UtcNow.AddDays(tokens.RefreshDays);
        repository.AddSession(new RefreshSession { UserId = user.Id, TokenHash = tokens.Hash(refresh), ExpiresAt = expiry });
        await repository.SaveAsync(ct);
        return new(new(access.Value, access.ExpiresAt, UserDto.From(user), refresh, expiry));
    }
}
