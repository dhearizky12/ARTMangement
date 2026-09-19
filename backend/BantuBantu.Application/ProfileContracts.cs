using System.ComponentModel.DataAnnotations;
namespace BantuBantu.Application;

public record AddressRequest(
    [Required, RegularExpression(@"^[0-9]{10}$")] string VillageId,
    [Required, StringLength(500, MinimumLength = 10)] string AddressDetail,
    [Required, RegularExpression(@"^[0-9]{5}$")] string PostalCode);
public record UploadRules(long MaxBytes, string[] ContentTypes, string[] DocumentTypes);
public class ProfileException(string message, int status = 400, string code = "PROFILE_VALIDATION", string? step = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string? Step { get; } = step;
}
public interface IFileStorage
{
    Task<Stream> OpenAsync(string storageKey, CancellationToken ct);
    Task<string> StoreAsync(Stream content, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
public interface IDocumentProcessor
{
    UploadRules Rules { get; }
    Task<Stream> ValidateAndNormalizeAsync(Stream file, long length, string contentType, CancellationToken ct);
}
