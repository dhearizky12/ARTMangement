namespace BantuBantu.Domain;

public enum UserRole { Customer, Provider, AgencyAdmin, PlatformAdmin }
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? PictureUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool ProfileCompleted { get; set; } = true;
    public string ProfileStep { get; set; } = "done";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class ExternalLogin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Provider { get; set; } = "Google";
    public string ProviderKey { get; set; } = "";
    public string RawProfileData { get; set; } = "{}";
}
public class RefreshSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string TokenHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
public class UserProfile
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string FullName { get; set; } = "";
    public DateOnly? BirthDate { get; set; }
    public string PhoneNumber { get; set; } = "";
    public string? Gender { get; set; }
}
public class UserAddress
{
    public string? VillageId { get; set; }
    public Village? Village { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string AddressLine { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string District { get; set; } = "";
    public string PostalCode { get; set; } = "";
}
public class UserDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string DocumentType { get; set; } = "";
    public string StorageKey { get; set; } = "";
    public string VerificationStatus { get; set; } = "Pending";
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}
