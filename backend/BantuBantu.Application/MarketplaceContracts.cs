using System.ComponentModel.DataAnnotations;
using BantuBantu.Domain;
namespace BantuBantu.Application;

public record Actor(Guid Id, UserRole Role, Guid? AgencyId);
public interface ICurrentActor { Actor Get(); }
public record AgencyRequest([Required, StringLength(120, MinimumLength = 2)] string Name, [Required, StringLength(500)] string ContactInfo);
public record AgencyStatusRequest([EnumDataType(typeof(AgencyStatus))] AgencyStatus Status);
public record AdminAccountRequest([Required, EmailAddress] string Email, [Required, StringLength(256, MinimumLength = 14)] string Password, [Required, StringLength(120)] string FullName, Guid AgencyId);
public record DraftRequest(Guid? AgencyId);
public record ProviderPersonalRequest([Required, StringLength(120, MinimumLength = 2)] string FullName, [Range(18, 80)] int Age, [Required, StringLength(2000, MinimumLength = 10)] string Bio, [Range(0, 62)] int YearsOfExperience);
public record AvailabilityRequest([EnumDataType(typeof(DayOfWeek))] DayOfWeek DayOfWeek, bool IsAvailable);
public record ProviderProfileRequest([Required, MinLength(1), MaxLength(10)] Guid[] CategoryIds, [Required, MinLength(1), MaxLength(20)] string[] Skills, [Required, MinLength(1), MaxLength(10)] string[] Languages, [EnumDataType(typeof(PricingType))] PricingType PricingType, [Range(typeof(decimal), "1", "999999999")] decimal Price, [Required, MinLength(7), MaxLength(7)] AvailabilityRequest[] Availability);
public record VerifyRequest(bool IdentityVerified, bool BackgroundCheckPassed, bool ContractSigned, [EnumDataType(typeof(VerificationStatus))] VerificationStatus Status, [StringLength(1000)] string? Note);
public record CategoryRequest([Required, RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$"), StringLength(80)] string Slug, [Required, StringLength(120)] string Name, [Required, StringLength(500)] string Description, [Required, StringLength(30)] string IconKey, int SortOrder, bool IsActive, bool IsFeatured);
public record ContentRequest([Required, StringLength(150)] string Title, [Required, StringLength(5000)] string Body, int SortOrder);
public record BookingRequest(Guid ProviderId, DateOnly ScheduledDate, [Required, RegularExpression("^[0-9]{10}$")] string VillageId, [Required, StringLength(500, MinimumLength = 10)] string AddressDetail);
public record OrderStatusRequest([EnumDataType(typeof(OrderStatus))] OrderStatus Status);
public record ReviewRequest([Range(1, 5)] int Rating, [Required, StringLength(2000, MinimumLength = 3)] string Comment);
public record CategorySummary(Guid Id, string Name);
public record ReviewDto(int Rating, string Comment, DateTimeOffset CreatedAt);
public record ProviderDto(Guid Id, Guid? AgencyId, string? AgencyName, string FullName, int Age, string Bio, int YearsOfExperience, int JobsCompletedCount, PricingType PricingType, decimal Price, VerificationStatus VerificationStatus, bool IdentityVerified, bool BackgroundCheckPassed, bool ContractSigned, string? Location, CategorySummary[] Categories, string[] Skills, string[] Languages, AvailabilityRequest[] Availability, double? Rating, int ReviewCount, ReviewDto[] Reviews);
public record ProviderDocumentDto(Guid Id, string DocumentType, DateTimeOffset UploadedAt);
public record ProviderAdminDto(ProviderDto Provider, string Step, string? VillageId, string AddressDetail, string PostalCode, ProviderDocumentDto[] Documents);
public record ProviderPage(int Total, int Page, int PageSize, ProviderDto[] Items);
public record OrderDto(Guid Id, Guid ProviderId, string ProviderName, OrderStatus Status, DateOnly ScheduledDate, decimal Price, PricingType PricingType, string AddressDetail, string VillageId, bool Reviewed);
public interface IMarketplaceRepository
{
    Task<(int Total, List<Provider> Items)> BrowseAsync(string? q, Guid? category, string? villageId, int page, int pageSize, CancellationToken ct);
    Task<Provider?> PublicProviderAsync(Guid id, CancellationToken ct);
    Task<List<Provider>> AdminProvidersAsync(Actor actor, CancellationToken ct);
    Task<Provider?> AdminProviderAsync(Actor actor, Guid id, CancellationToken ct);
    void AddProvider(Provider provider);
    void AddDocument(ProviderDocument document);
    Task<bool> CategoriesExistAsync(Guid[] ids, CancellationToken ct);
    Task<List<ServiceCategory>> CategoriesAsync(CancellationToken ct);
    Task<ServiceCategory?> CategoryAsync(Guid id, CancellationToken ct);
    void AddCategory(ServiceCategory category);
    Task<bool> CategoryInUseAsync(Guid id, CancellationToken ct);
    void RemoveCategory(ServiceCategory category);
    Task<List<Agency>> AgenciesAsync(CancellationToken ct);
    Task<Agency?> AgencyAsync(Guid id, CancellationToken ct);
    void AddAgency(Agency agency);
    Task<List<ContentBlock>> ContentAsync(CancellationToken ct);
    Task<ContentBlock?> ContentAsync(string id, CancellationToken ct);
    void AddContent(ContentBlock block);
    Task<List<Order>> OrdersAsync(Actor actor, CancellationToken ct);
    Task<Order?> OrderAsync(Actor actor, Guid id, CancellationToken ct);
    void AddOrder(Order order);
    void AddReview(Review review);
    void Audit(Actor actor, Guid? providerId, string action, string detail);
    Task<List<AuditEntry>> AuditAsync(CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
}
