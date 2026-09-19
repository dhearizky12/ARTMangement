namespace BantuBantu.Domain;

public enum AgencyStatus { Pending, Approved, Suspended }
public enum PricingType { PerVisit, PerMonth }
public enum VerificationStatus { Pending, Verified, Rejected }
public enum OrderStatus { Pending, Confirmed, Completed, Cancelled }
public class AdminAccount : User
{
    public string PasswordHash { get; set; } = "";
    public Guid? AgencyId { get; set; }
    public Agency? Agency { get; set; }
}
public class Agency
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ContactInfo { get; set; } = "";
    public AgencyStatus Status { get; set; } = AgencyStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class Provider
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public string FullName { get; set; } = "";
    public int Age { get; set; }
    public string Bio { get; set; } = "";
    public int YearsOfExperience { get; set; }
    public int JobsCompletedCount { get; set; }
    public PricingType PricingType { get; set; }
    public decimal Price { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public bool IdentityVerified { get; set; }
    public bool BackgroundCheckPassed { get; set; }
    public bool ContractSigned { get; set; }
    public string? VillageId { get; set; }
    public Village? Village { get; set; }
    public string AddressDetail { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public uint Version { get; set; }
    public List<ProviderCategory> Categories { get; set; } = [];
    public List<ProviderSkill> Skills { get; set; } = [];
    public List<ProviderLanguage> Languages { get; set; } = [];
    public List<ProviderAvailability> Availability { get; set; } = [];
    public List<ProviderDocument> Documents { get; set; } = [];
    public List<Review> Reviews { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
}
public class ProviderCategory { public Guid ProviderId { get; set; } public Provider Provider { get; set; } = null!; public Guid ServiceCategoryId { get; set; } public ServiceCategory ServiceCategory { get; set; } = null!; }
public class ProviderSkill { public Guid ProviderId { get; set; } public Provider Provider { get; set; } = null!; public string SkillName { get; set; } = ""; }
public class ProviderLanguage { public Guid ProviderId { get; set; } public Provider Provider { get; set; } = null!; public string LanguageName { get; set; } = ""; }
public class ProviderAvailability { public Guid ProviderId { get; set; } public Provider Provider { get; set; } = null!; public DayOfWeek DayOfWeek { get; set; } public bool IsAvailable { get; set; } }
public class ProviderDocument { public Guid Id { get; set; } = Guid.NewGuid(); public Guid ProviderId { get; set; } public Provider Provider { get; set; } = null!; public string DocumentType { get; set; } = ""; public string StorageKey { get; set; } = ""; public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow; }
public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public User Customer { get; set; } = null!;
    public Guid ProviderId { get; set; }
    public Provider Provider { get; set; } = null!;
    public OrderStatus Status { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public decimal Price { get; set; }
    public PricingType PricingType { get; set; }
    public string VillageId { get; set; } = "";
    public Village Village { get; set; } = null!;
    public string AddressDetail { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Review? Review { get; set; }
    public uint Version { get; set; }
}
public class Review { public Guid Id { get; set; } = Guid.NewGuid(); public Guid OrderId { get; set; } public Order Order { get; set; } = null!; public Guid CustomerId { get; set; } public User Customer { get; set; } = null!; public Guid ProviderId { get; set; } public Provider Provider { get; set; } = null!; public int Rating { get; set; } public string Comment { get; set; } = ""; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
public class ContentBlock { public string Id { get; set; } = ""; public string Title { get; set; } = ""; public string Body { get; set; } = ""; public int SortOrder { get; set; } }
public class AuditEntry { public Guid Id { get; set; } = Guid.NewGuid(); public Guid ActorId { get; set; } public Guid? ProviderId { get; set; } public string Action { get; set; } = ""; public string Detail { get; set; } = ""; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow; }
