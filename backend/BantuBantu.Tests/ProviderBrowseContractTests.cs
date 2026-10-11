using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BantuBantu.Application;
using BantuBantu.Domain;
using BantuBantu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace BantuBantu.Tests;

// Contract guard for the public provider list. The browse endpoint and the
// detail endpoint must describe a provider identically; the list is allowed to
// evolve its SQL shape but never its payload.
[Collection("diagnostics-db")]
public class ProviderBrowseContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    internal static readonly Guid CategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid ProviderId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    internal static readonly Guid CustomerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    internal static async Task SeedPublishedProvider(AppDbContext db)
    {
        if (await db.Providers.AnyAsync(p => p.Id == ProviderId)) return;
        if (!await db.Villages.AnyAsync(v => v.Id == "3374011003"))
        {
            var province = new Province { Id = "33", Name = "Jawa Tengah" }; var regency = new Regency { Id = "3374", Name = "Kota Semarang", Province = province }; var district = new District { Id = "337401", Name = "Gunungpati", Regency = regency };
            db.Villages.Add(new Village { Id = "3374011003", Name = "Sekaran", District = district, Type = VillageType.Kelurahan });
        }
        if (!await db.ServiceCategories.AnyAsync(c => c.Id == CategoryId))
            db.ServiceCategories.Add(new ServiceCategory { Id = CategoryId, Slug = "browse-uji", Name = "Kategori Browse Uji", Description = "Kategori untuk uji kontrak browse.", IconKey = "briefcase", SortOrder = 5, IsActive = true });
        var customer = await db.Users.FirstOrDefaultAsync(u => u.Id == CustomerId);
        if (customer is null) { customer = new User { Id = CustomerId, Email = "browse-customer@example.test", FullName = "Pelanggan Browse", Role = UserRole.Customer }; db.Users.Add(customer); }
        await db.SaveChangesAsync();

        var p = new Provider
        {
            Id = ProviderId, FullName = "Penyedia Uji Browse", Age = 34, Bio = "Bio penyedia untuk uji kontrak browse publik.", YearsOfExperience = 7,
            PricingType = PricingType.PerMonth, Price = 1500000m, VerificationStatus = VerificationStatus.Verified, ApplicationStatus = ProviderApplicationStatus.Approved,
            IdentityVerified = true, BackgroundCheckPassed = true, ContractSigned = true, VillageId = "3374011003", AddressDetail = "Jalan Uji nomor 1", PostalCode = "50229",
            JobsCompletedCount = 0, ModerationNote = "catatan moderasi uji"
        };
        p.Categories.Add(new ProviderCategory { ServiceCategoryId = CategoryId });
        p.Skills.Add(new ProviderSkill { SkillName = "Membersihkan" }); p.Skills.Add(new ProviderSkill { SkillName = "Menjemur" });
        p.Languages.Add(new ProviderLanguage { LanguageName = "Indonesia" }); p.Languages.Add(new ProviderLanguage { LanguageName = "Jawa" });
        foreach (var day in Enum.GetValues<DayOfWeek>()) p.Availability.Add(new ProviderAvailability { DayOfWeek = day, IsAvailable = day != DayOfWeek.Sunday });
        db.Providers.Add(p);
        await db.SaveChangesAsync();

        Review Review(Order order, int rating, string comment, bool hidden, string createdAt)
        {
            var review = new Review { OrderId = order.Id, Order = order, ProviderId = ProviderId, Provider = p, CustomerId = CustomerId, Customer = customer!, Rating = rating, Comment = comment, IsHidden = hidden, CreatedAt = DateTimeOffset.Parse(createdAt) };
            order.Review = review; order.Status = OrderStatus.Completed;
            return review;
        }
        Order Order(int day, string detail) { var o = new Order { CustomerId = CustomerId, Customer = customer!, ProviderId = ProviderId, Provider = p, ScheduledDate = new DateOnly(2026, 1, day), Price = 1500000m, PricingType = PricingType.PerMonth, VillageId = "3374011003", AddressDetail = detail }; db.Orders.Add(o); return o; }

        db.Reviews.AddRange(
            Review(Order(10, "Jalan Uji nomor 1"), 5, "Sangat memuaskan", false, "2026-01-11T02:00:00Z"),
            Review(Order(12, "Jalan Uji nomor 2"), 3, "Cukup baik", false, "2026-01-12T02:00:00Z"),
            Review(Order(13, "Jalan Uji nomor 3"), 1, "Disembunyikan", true, "2026-01-13T02:00:00Z"));
        var pending = new Order { CustomerId = CustomerId, Customer = customer!, ProviderId = ProviderId, Provider = p, Status = OrderStatus.Pending, ScheduledDate = new DateOnly(2026, 2, 10), Price = 1500000m, PricingType = PricingType.PerMonth, VillageId = "3374011003", AddressDetail = "Jalan Uji nomor 4" };
        db.Orders.Add(pending);
        await db.SaveChangesAsync();
    }

    internal static string Canon(ProviderDto d) => string.Join("|",
        d.Id, d.AgencyId, d.AgencyName, d.FullName, d.Age, d.Bio, d.YearsOfExperience, d.JobsCompletedCount, d.PricingType, d.Price,
        d.VerificationStatus, d.IdentityVerified, d.BackgroundCheckPassed, d.ContractSigned, d.Location, d.Rating, d.ReviewCount,
        d.ApplicationStatus, d.ModerationNote,
        string.Join(";", d.Categories.OrderBy(c => c.Id).Select(c => c.Id + ":" + c.Name)),
        string.Join(";", d.Skills.OrderBy(s => s)),
        string.Join(";", d.Languages.OrderBy(s => s)),
        string.Join(";", d.Availability.OrderBy(a => a.DayOfWeek).Select(a => a.DayOfWeek + ":" + a.IsAvailable)),
        string.Join(";", d.Reviews.OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Rating).ThenBy(r => r.Comment).Select(r => r.Rating + ":" + r.Comment + ":" + r.CreatedAt.ToString("O"))));

    internal static int SqlCount(HttpResponseMessage response)
    {
        var header = response.Headers.TryGetValues("Server-Timing", out var v) ? v.Single() : "";
        var token = header.Split(',').Select(t => t.Trim()).SingleOrDefault(t => t.StartsWith("sql;count="));
        return token is null ? -1 : int.Parse(token["sql;count=".Length..]);
    }

    [Fact]
    public async Task Browse_And_Detail_DescribeTheProviderIdentically()
    {
        using var factory = new DiagnosticsFactory();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            await SeedPublishedProvider(db);
        }
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);

        var page = (await client.GetFromJsonAsync<ProviderPage>("/api/providers?pageSize=50", Json))!;
        var listed = page.Items.Single(p => p.Id == ProviderId);
        var detail = (await client.GetFromJsonAsync<ProviderDto>($"/api/providers/{ProviderId}", Json))!;

        Assert.Equal(Canon(detail), Canon(listed));
        Assert.Equal(4.0, listed.Rating);
        Assert.Equal(2, listed.ReviewCount);
        Assert.Equal(3, listed.JobsCompletedCount);
        Assert.Equal("Sekaran, Gunungpati, Kota Semarang, Jawa Tengah", listed.Location);
        Assert.False(listed.Availability.Single(a => a.DayOfWeek == DayOfWeek.Sunday).IsAvailable);
        Assert.DoesNotContain(listed.Reviews, r => r.Comment == "Disembunyikan");

        var listResponse = await client.GetAsync("/api/providers?pageSize=50");
        Assert.InRange(SqlCount(listResponse), 1, 12);
    }
}
