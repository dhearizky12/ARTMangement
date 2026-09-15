using System.ComponentModel.DataAnnotations;
using BantuBantu.Domain;
namespace BantuBantu.Application;

public record PersonalInfoRequest(
    [Required, StringLength(120, MinimumLength = 2)] string FullName,
    [Required] DateOnly? BirthDate,
    [Required, RegularExpression(@"^(\+62|62|0)8[0-9]{8,12}$", ErrorMessage = "Gunakan nomor HP Indonesia, misalnya 081234567890.")] string PhoneNumber,
    [Required, RegularExpression("^(male|female|other|undisclosed)$")] string Gender) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (BirthDate.HasValue && (BirthDate < new DateOnly(1900, 1, 1) || BirthDate >= DateOnly.FromDateTime(DateTime.UtcNow)))
            yield return new ValidationResult("Tanggal lahir harus sebelum hari ini dan setelah tahun 1899.", [nameof(BirthDate)]);
    }
}
public record AddressRequest(
    [Required, RegularExpression(@"^[0-9]{10}$")] string VillageId,
    [Required, StringLength(500, MinimumLength = 10)] string AddressDetail,
    [Required, RegularExpression(@"^[0-9]{5}$")] string PostalCode);
public record AddressStatus(string Province, string City, string District, string AddressLine, string PostalCode, string? VillageId, string? VillageName);
public record UploadRules(long MaxBytes, string[] ContentTypes, string[] DocumentTypes);
public record DocumentSummary(string DocumentType, string VerificationStatus, DateTimeOffset UploadedAt);
public record ProfileStatusDto(string ProfileStep, bool ProfileCompleted, bool PersonalCompleted, bool AddressCompleted, bool DocumentsCompleted,
    PersonalInfoRequest? PersonalInfo, AddressStatus? Address, DocumentSummary? Document, UploadRules DocumentRules);
public record ProfileState(User User, UserProfile? Personal, UserAddress? Address, UserDocument? Document);
public class ProfileException(string message, int status = 400, string code = "PROFILE_VALIDATION", string? step = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string? Step { get; } = step;
}
public interface IProfileTransaction : IAsyncDisposable { Task CommitAsync(CancellationToken ct); }
public interface IProfileRepository
{
    Task<ProfileState> GetAsync(Guid userId, CancellationToken ct);
    Task<IProfileTransaction> LockAsync(Guid userId, CancellationToken ct);
    void Add(UserProfile profile);
    void Add(UserAddress address);
    void Add(UserDocument document);
    Task SaveAsync(CancellationToken ct);
}
public interface IFileStorage
{
    Task<string> StoreAsync(Stream content, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
public interface IDocumentProcessor
{
    UploadRules Rules { get; }
    Task<Stream> ValidateAndNormalizeAsync(Stream file, long length, string contentType, CancellationToken ct);
}
public interface IProfileService
{
    Task<ProfileStatusDto> StatusAsync(Guid userId, CancellationToken ct);
    Task<ProfileStatusDto> PersonalAsync(Guid userId, PersonalInfoRequest request, CancellationToken ct);
    Task<ProfileStatusDto> AddressAsync(Guid userId, AddressRequest request, CancellationToken ct);
    Task<ProfileStatusDto> DocumentsAsync(Guid userId, string type, Stream file, long length, string contentType, CancellationToken ct);
}
