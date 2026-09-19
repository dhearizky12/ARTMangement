using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Text.Json.Serialization;
using BantuBantu.Application;
using BantuBantu.Domain;
using BantuBantu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
namespace BantuBantu.Tests;

public partial class AuthIntegrationTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static async Task<T> Read<T>(HttpResponseMessage response) { response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<T>(Json))!; }
    private static async Task<ProviderAdminDto> Save(HttpClient c, Guid id, string step, object body) => await Read<ProviderAdminDto>(await c.PostAsJsonAsync($"/api/admin/providers/{id}/{step}", body, Json));
    private static async Task MarketplaceLifecycle(HttpClient customer, AuthFactory factory, AuthResponse customerSession, string platformToken)
    {
        using var guest = factory.CreateClient(); using var platform = factory.CreateClient(); platform.DefaultRequestHeaders.Authorization = new("Bearer", platformToken);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/providers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync("/api/service-categories")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await customer.GetAsync("/api/profile/status")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync("/api/admin/providers", new DraftRequest(null))).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (!await db.Villages.AnyAsync())
            {
                var province = new Province { Id = "33", Name = "Jawa Tengah" }; var regency = new Regency { Id = "3374", Name = "Kota Semarang", Province = province }; var district = new District { Id = "337401", Name = "Gunungpati", Regency = regency };
                db.Villages.Add(new Village { Id = "3374011003", Name = "Sekaran", District = district, Type = VillageType.Kelurahan });
                await db.SaveChangesAsync();
            }
        }
        var direct = await Read<ProviderAdminDto>(await platform.PostAsJsonAsync("/api/admin/providers", new DraftRequest(null)));
        Assert.Null(direct.Provider.AgencyId);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/providers/{direct.Provider.Id}")).StatusCode);
        var agencies = new List<Agency>(); var agencyClients = new List<HttpClient>();
        foreach (var name in new[] { "Agency A", "Agency B" })
        {
            var a = await Read<Agency>(await platform.PostAsJsonAsync("/api/admin/agencies", new AgencyRequest(name, "Kontak pengujian")));
            Assert.Equal(AgencyStatus.Pending, a.Status);
            await Read<Agency>(await platform.PostAsJsonAsync($"/api/admin/agencies/{a.Id}/status", new AgencyStatusRequest(AgencyStatus.Approved), Json));
            var admin = await Read<UserDto>(await platform.PostAsJsonAsync("/api/admin/accounts", new AdminAccountRequest($"{a.Id}@example.test", "Agency-password-long-42!", name, a.Id)));
            var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);
            var login = await Read<AuthResponse>(await client.PostAsJsonAsync("/api/auth/admin/login", new AdminRequest(admin.Email, "Agency-password-long-42!")));
            Assert.Equal("AgencyAdmin", login.User.Role); Assert.Equal(a.Id.ToString(), new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken).Claims.Single(c => c.Type == "agencyId").Value);
            client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken); agencies.Add(a); agencyClients.Add(client);
        }
        using var aClient = agencyClients[0]; using var bClient = agencyClients[1];
        var own = await Read<ProviderAdminDto>(await aClient.PostAsJsonAsync("/api/admin/providers", new DraftRequest(null)));
        var other = await Read<ProviderAdminDto>(await bClient.PostAsJsonAsync("/api/admin/providers", new DraftRequest(null)));
        Assert.Equal(agencies[0].Id, own.Provider.AgencyId);
        Assert.Equal(HttpStatusCode.Forbidden, (await aClient.PostAsJsonAsync("/api/admin/providers", new DraftRequest(agencies[1].Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await aClient.GetAsync("/api/admin/agencies")).StatusCode);
        var roster = await Read<ProviderAdminDto[]>(await aClient.GetAsync("/api/admin/providers")); Assert.Single(roster); Assert.Equal(own.Provider.Id, roster[0].Provider.Id);
        var personal = new ProviderPersonalRequest("Penyedia Pengujian", 30, "Bio penyedia untuk pengujian integrasi.", 5);
        var address = new AddressRequest("3374011003", "Jalan Pengujian nomor 10 RT 01 RW 02", "50229");
        var categoryBody = new CategoryRequest("uji-layanan", "Kategori pengujian", "Deskripsi kategori dari admin", "briefcase", 9, true, false);
        var newCategory = await Read<ServiceCategory>(await platform.PostAsJsonAsync("/api/admin/categories", categoryBody));
        Assert.Contains(await Read<ServiceCategoryDto[]>(await guest.GetAsync("/api/service-categories")), c => c.Id == newCategory.Id);
        await Read<ServiceCategory>(await platform.PutAsJsonAsync($"/api/admin/categories/{newCategory.Id}", categoryBody with { IsActive = false }));
        Assert.DoesNotContain(await Read<ServiceCategoryDto[]>(await guest.GetAsync("/api/service-categories")), c => c.Id == newCategory.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await platform.DeleteAsync($"/api/admin/categories/{newCategory.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await aClient.PostAsJsonAsync("/api/admin/categories", categoryBody)).StatusCode);
        await Read<ContentBlock>(await platform.PutAsJsonAsync("/api/admin/content/qa-information", new ContentRequest("Informasi uji", "Konten diperbarui oleh platform admin.", 10)));
        Assert.Contains(await Read<ContentBlock[]>(await guest.GetAsync("/api/content")), c => c.Id == "qa-information");
        var category = (await Read<ServiceCategoryDto[]>(await guest.GetAsync("/api/service-categories")))[0];
        var profile = new ProviderProfileRequest([category.Id], ["Mengemudi"], ["Indonesia"], PricingType.PerVisit, 150000, Enum.GetValues<DayOfWeek>().Select(d => new AvailabilityRequest(d, true)).ToArray());
        var verify = new VerifyRequest(true, true, true, VerificationStatus.Verified, "Test pemeriksaan");
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(200, 120); using var stream = new MemoryStream(); await image.SaveAsync(stream, new SixLabors.ImageSharp.Formats.Png.PngEncoder()); var bytes = stream.ToArray();
        MultipartFormDataContent Form(string type, byte[]? content = null) { var f = new MultipartFormDataContent(); f.Add(new StringContent(type), "documentType"); var file = new ByteArrayContent(content ?? bytes); file.Headers.ContentType = new("image/png"); f.Add(file, "file", "../../identity.png"); return f; }
        foreach (var target in new[] { direct.Provider.Id, other.Provider.Id })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await aClient.GetAsync($"/api/admin/providers/{target}")).StatusCode);
            foreach (var (step, body) in new (string, object)[] { ("personal-info", personal), ("address", address), ("profile", profile), ("verify", verify) })
                Assert.Equal(HttpStatusCode.NotFound, (await aClient.PostAsJsonAsync($"/api/admin/providers/{target}/{step}", body, Json)).StatusCode);
            using var file = Form("KTP"); Assert.Equal(HttpStatusCode.NotFound, (await aClient.PostAsync($"/api/admin/providers/{target}/documents", file)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await aClient.GetAsync($"/api/admin/providers/{target}/documents/{Guid.NewGuid()}")).StatusCode);
        }
        // Full direct-talent wizard; no agency is required to publish it.
        await Save(platform, direct.Provider.Id, "personal-info", personal);
        Assert.Equal(HttpStatusCode.BadRequest, (await platform.PostAsJsonAsync($"/api/admin/providers/{direct.Provider.Id}/address", address with { VillageId = "0000000000" })).StatusCode);
        await Save(platform, direct.Provider.Id, "address", address);
        using (var invalid = Form("KTP", [1, 2, 3])) Assert.Equal(HttpStatusCode.BadRequest, (await platform.PostAsync($"/api/admin/providers/{direct.Provider.Id}/documents", invalid)).StatusCode);
        foreach (var type in new[] { "KTP", "KK" }) { using var form = Form(type); direct = await Read<ProviderAdminDto>(await platform.PostAsync($"/api/admin/providers/{direct.Provider.Id}/documents", form)); }
        Assert.Equal("profile", direct.Step);
        Assert.Equal(HttpStatusCode.OK, (await platform.GetAsync($"/api/admin/providers/{direct.Provider.Id}/documents/{direct.Documents[0].Id}")).StatusCode);
        direct = await Save(platform, direct.Provider.Id, "profile", profile); Assert.Equal("verify", direct.Step);
        Assert.Equal(HttpStatusCode.BadRequest, (await platform.PostAsJsonAsync($"/api/admin/providers/{direct.Provider.Id}/verify", verify with { ContractSigned = false }, Json)).StatusCode);
        await Save(platform, direct.Provider.Id, "verify", verify);
        var published = await Read<ProviderDto>(await guest.GetAsync($"/api/providers/{direct.Provider.Id}")); Assert.Null(published.Rating); Assert.Equal(0, published.JobsCompletedCount);
        var list = await Read<ProviderPage>(await guest.GetAsync($"/api/providers?category={category.Id}&q=Penyedia")); Assert.Equal(1, list.Total);
        var booking = new BookingRequest(direct.Provider.Id, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), address.VillageId, address.AddressDetail);
        var order = await Read<OrderDto>(await customer.PostAsJsonAsync("/api/orders", booking)); Assert.Equal(profile.Price, order.Price);
        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostAsJsonAsync($"/api/orders/{order.Id}/review", new ReviewRequest(5, "Layanan baik"))).StatusCode);
        Assert.Empty(await Read<OrderDto[]>(await aClient.GetAsync("/api/admin/orders")));
        Assert.Equal(HttpStatusCode.NotFound, (await aClient.PostAsJsonAsync($"/api/admin/orders/{order.Id}/status", new OrderStatusRequest(OrderStatus.Completed), Json)).StatusCode);
        await Read<OrderDto>(await platform.PostAsJsonAsync($"/api/admin/orders/{order.Id}/status", new OrderStatusRequest(OrderStatus.Confirmed), Json));
        await Read<OrderDto>(await platform.PostAsJsonAsync($"/api/admin/orders/{order.Id}/status", new OrderStatusRequest(OrderStatus.Completed), Json));
        await Read<ReviewDto>(await customer.PostAsJsonAsync($"/api/orders/{order.Id}/review", new ReviewRequest(5, "Layanan baik")));
        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostAsJsonAsync($"/api/orders/{order.Id}/review", new ReviewRequest(1, "Duplikat"))).StatusCode);
        published = await Read<ProviderDto>(await guest.GetAsync($"/api/providers/{direct.Provider.Id}")); Assert.Equal(5, published.Rating); Assert.Equal(1, published.JobsCompletedCount);
        // Agency wizard followed by PlatformAdmin override must remain audited.
        await Save(aClient, own.Provider.Id, "personal-info", personal); await Save(aClient, own.Provider.Id, "address", address);
        foreach (var type in new[] { "KTP", "KK" }) { using var form = Form(type); await Read<ProviderAdminDto>(await aClient.PostAsync($"/api/admin/providers/{own.Provider.Id}/documents", form)); }
        await Save(aClient, own.Provider.Id, "profile", profile with { PricingType = PricingType.PerMonth, Price = 3000000 }); await Save(aClient, own.Provider.Id, "verify", verify);
        await Save(platform, own.Provider.Id, "verify", verify with { Status = VerificationStatus.Rejected, Note = "Platform override" });
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/providers/{own.Provider.Id}")).StatusCode);
        var audit = await Read<AuditEntry[]>(await platform.GetAsync("/api/admin/audit")); Assert.Contains(audit, x => x.Detail.Contains("Platform override"));
        await Read<Agency>(await platform.PostAsJsonAsync($"/api/admin/agencies/{agencies[0].Id}/status", new AgencyStatusRequest(AgencyStatus.Suspended), Json));
        Assert.Equal(HttpStatusCode.Unauthorized, (await aClient.GetAsync("/api/admin/providers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await platform.GetAsync($"/api/admin/providers/{own.Provider.Id}")).StatusCode);
    }
}
