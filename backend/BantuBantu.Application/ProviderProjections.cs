using System.Linq.Expressions;
using BantuBantu.Domain;
namespace BantuBantu.Application;

// SQL-side projection of the public provider DTO. Kept separate from the
// in-memory Map in ProviderService so a list read collapses to a single
// database round trip instead of one split query per collection. The two
// shapes are locked together by ProviderBrowseReadTests.
public static class ProviderProjections
{
    public static readonly Expression<Func<Provider, ProviderDto>> Public = p => new ProviderDto(
        p.Id,
        p.AgencyId,
        p.Agency == null ? null : p.Agency.Name,
        p.FullName,
        p.Age,
        p.Bio,
        p.YearsOfExperience,
        p.Orders.Count(o => o.Status == OrderStatus.Completed),
        p.PricingType,
        p.Price,
        p.VerificationStatus,
        p.IdentityVerified,
        p.BackgroundCheckPassed,
        p.ContractSigned,
        p.Village == null ? null : p.Village.Name + ", " + p.Village.District.Name + ", " + p.Village.District.Regency.Name + ", " + p.Village.District.Regency.Province.Name,
        p.Categories.Select(c => new CategorySummary(c.ServiceCategoryId, c.ServiceCategory.Name)).ToArray(),
        p.Skills.Select(s => s.SkillName).ToArray(),
        p.Languages.Select(s => s.LanguageName).ToArray(),
        p.Availability.OrderBy(a => a.DayOfWeek).Select(a => new AvailabilityRequest(a.DayOfWeek, a.IsAvailable)).ToArray(),
        p.Reviews.Where(r => !r.IsHidden).Select(r => (double?)r.Rating).Average(),
        p.Reviews.Count(r => !r.IsHidden),
        p.Reviews.Where(r => !r.IsHidden).OrderByDescending(r => r.CreatedAt).Take(50).Select(r => new ReviewDto(r.Rating, r.Comment, r.CreatedAt)).ToArray(),
        p.ApplicationStatus,
        p.ModerationNote);
}
