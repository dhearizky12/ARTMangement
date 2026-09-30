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
        Assert.Equal(HttpStatusCode.Forbidden, (await customer.PostAsJsonAsync("/api/admin/providers", new DraftRequest(null, "customer-provider@example.test", "Provider-password-long-42!"))).StatusCode);
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
        var direct = await Read<ProviderAdminDto>(await platform.PostAsJsonAsync("/api/admin/providers", new DraftRequest(null, "direct-provider@example.test", "Provider-password-long-42!")));
        Assert.Null(direct.Provider.AgencyId);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/providers/{direct.Provider.Id}")).StatusCode);
        var agencies = new List<Agency>(); var agencyClients = new List<HttpClient>();
        foreach (var name in new[] { "Agency A", "Agency B" })
        {
            var a = await Read<Agency>(await platform.PostAsJsonAsync("/api/admin/agencies", new AgencyRequest(name, "Kontak pengujian")));
            Assert.Equal(AgencyStatus.Approved, a.Status);
            await Read<Agency>(await platform.PostAsJsonAsync($"/api/admin/agencies/{a.Id}/status", new AgencyStatusRequest(AgencyStatus.Approved), Json));
            var admin = await Read<UserDto>(await platform.PostAsJsonAsync("/api/admin/accounts", new AdminAccountRequest($"{a.Id}@example.test", "Agency-password-long-42!", name, a.Id)));
            var client = factory.CreateClient(); client.DefaultRequestHeaders.Add("Origin", AuthFactory.Origin);
            var login = await Read<AuthResponse>(await client.PostAsJsonAsync("/api/auth/admin/login", new AdminRequest(admin.Email, "Agency-password-long-42!")));
            Assert.Equal("AgencyAdmin", login.User.Role); Assert.Equal(a.Id.ToString(), new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken).Claims.Single(c => c.Type == "agencyId").Value);
            client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken); agencies.Add(a); agencyClients.Add(client);
        }
        using var aClient = agencyClients[0]; using var bClient = agencyClients[1];
        var own = await Read<ProviderAdminDto>(await aClient.PostAsJsonAsync("/api/admin/providers", new DraftRequest(null, "agency-a-provider@example.test", "Provider-password-long-42!")));
        var other = await Read<ProviderAdminDto>(await bClient.PostAsJsonAsync("/api/admin/providers", new DraftRequest(null, "agency-b-provider@example.test", "Provider-password-long-42!")));
        Assert.Equal(agencies[0].Id, own.Provider.AgencyId);
        Assert.Equal(HttpStatusCode.Forbidden, (await aClient.PostAsJsonAsync("/api/admin/providers", new DraftRequest(agencies[1].Id, "invalid-provider@example.test", "Provider-password-long-42!"))).StatusCode);
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
        Assert.Equal(HttpStatusCode.Forbidden, (await aClient.GetAsync("/api/admin/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await aClient.PostAsJsonAsync("/api/admin/categories", categoryBody)).StatusCode);
        var legacyCategoryBody = new CategoryRequest("legacy-layanan", "Kategori legacy", "Kategori yang sudah dipakai provider.", "briefcase", 10, true, false);
        var legacyCategory = await Read<ServiceCategory>(await platform.PostAsJsonAsync("/api/admin/categories", legacyCategoryBody));
        await Read<ContentBlock>(await platform.PutAsJsonAsync("/api/admin/content/qa-information", new ContentRequest("Informasi uji", "Konten diperbarui oleh platform admin.", 10)));
        Assert.Contains(await Read<ContentBlock[]>(await guest.GetAsync("/api/content")), c => c.Id == "qa-information");
        var category = (await Read<ServiceCategoryDto[]>(await guest.GetAsync("/api/service-categories")))[0];
        var profile = new ProviderProfileRequest([category.Id, legacyCategory.Id], ["Mengemudi"], ["Indonesia"], PricingType.PerVisit, 150000, Enum.GetValues<DayOfWeek>().Select(d => new AvailabilityRequest(d, true)).ToArray());
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
        async Task SubmitPending(ProviderAdminDto draft, string fullName)
        {
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var provider = await db.Providers.SingleAsync(p => p.Id == draft.Provider.Id);
                var categoryId = await db.ServiceCategories.Where(c => c.IsActive).Select(c => c.Id).FirstAsync();
                provider.FullName = fullName;
                provider.Age = 30;
                provider.Bio = "Provider queue untuk pengujian integrasi dan scoping.";
                provider.YearsOfExperience = 5;
                provider.VillageId = address.VillageId;
                provider.AddressDetail = address.AddressDetail;
                provider.PostalCode = address.PostalCode;
                provider.Price = profile.Price;
                provider.PricingType = profile.PricingType;
                provider.ApplicationStatus = ProviderApplicationStatus.Submitted;
                provider.SubmittedAt = DateTimeOffset.UtcNow;
                db.Set<ProviderCategory>().Add(new ProviderCategory { ProviderId = provider.Id, ServiceCategoryId = categoryId });
                db.Set<ProviderSkill>().Add(new ProviderSkill { ProviderId = provider.Id, SkillName = "Mengemudi" });
                db.Set<ProviderLanguage>().Add(new ProviderLanguage { ProviderId = provider.Id, LanguageName = "Indonesia" });
                foreach (var day in Enum.GetValues<DayOfWeek>()) db.Set<ProviderAvailability>().Add(new ProviderAvailability { ProviderId = provider.Id, DayOfWeek = day, IsAvailable = true });
                foreach (var type in new[] { "KTP", "KK" }) db.Set<ProviderDocument>().Add(new ProviderDocument { ProviderId = provider.Id, DocumentType = type, StorageKey = $"{Guid.NewGuid():N}.jpg" });
                await db.SaveChangesAsync();
            }
        }
        await SubmitPending(own, "Agency Queue A");
        await SubmitPending(other, "Agency Queue B");
        var platformQueue = await Read<ProviderAdminDto[]>(await platform.GetAsync("/api/admin/providers/verification-queue"));
        Assert.Equal(2, platformQueue.Length);
        Assert.Equal(2, platformQueue.Count(p => p.Provider.Id == own.Provider.Id || p.Provider.Id == other.Provider.Id));
        Assert.Contains(platformQueue, p => p.Provider.Id == own.Provider.Id);
        Assert.Contains(platformQueue, p => p.Provider.Id == other.Provider.Id);
        var agencyAQueue = await Read<ProviderAdminDto[]>(await aClient.GetAsync("/api/admin/providers/verification-queue"));
        Assert.Single(agencyAQueue);
        Assert.Equal(own.Provider.Id, agencyAQueue[0].Provider.Id);
        var agencyBQueue = await Read<ProviderAdminDto[]>(await bClient.GetAsync("/api/admin/providers/verification-queue"));
        Assert.Single(agencyBQueue);
        Assert.Equal(other.Provider.Id, agencyBQueue[0].Provider.Id);
        await Read<ProviderAdminDto>(await aClient.PostAsJsonAsync($"/api/admin/providers/{own.Provider.Id}/verify", verify, Json));
        var missingReject = await bClient.PostAsJsonAsync($"/api/admin/providers/{other.Provider.Id}/verify", verify with { Status = VerificationStatus.Rejected, Note = null }, Json);
        Assert.Equal(HttpStatusCode.BadRequest, missingReject.StatusCode);
        Assert.Contains("Alasan penolakan wajib diisi", await missingReject.Content.ReadAsStringAsync());
        var rejected = await Read<ProviderAdminDto>(await bClient.PostAsJsonAsync($"/api/admin/providers/{other.Provider.Id}/verify", verify with { Status = VerificationStatus.Rejected, Note = "Dokumen tidak sesuai." }, Json));
        Assert.Equal(VerificationStatus.Rejected, rejected.Provider.VerificationStatus);
        Assert.Equal(ProviderApplicationStatus.Rejected, rejected.Provider.ApplicationStatus);
        Assert.DoesNotContain(await Read<ProviderAdminDto[]>(await platform.GetAsync("/api/admin/providers/verification-queue")), p => p.Provider.Id == own.Provider.Id || p.Provider.Id == other.Provider.Id);
        Assert.DoesNotContain(await Read<ProviderAdminDto[]>(await platform.GetAsync("/api/admin/providers/verification-queue")), p => p.Provider.Id == direct.Provider.Id);
        Assert.DoesNotContain(await Read<ProviderAdminDto[]>(await platform.GetAsync("/api/admin/providers/verification-queue")), p => p.Provider.ApplicationStatus is ProviderApplicationStatus.Draft or ProviderApplicationStatus.Approved or ProviderApplicationStatus.Rejected);
        // Post-verification suspension is scoped to the owning agency and removes
        // the provider from public discovery without changing its verification.
        var suspended = await aClient.PatchAsJsonAsync($"/api/admin/providers/{own.Provider.Id}/suspend", new ProviderModerationRequest("Dokumen perlu ditinjau ulang."), Json);
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);
        var suspendedDto = await Read<ProviderAdminDto>(suspended);
        Assert.Equal(ProviderApplicationStatus.Suspended, suspendedDto.Provider.ApplicationStatus);
        Assert.Equal(VerificationStatus.Verified, suspendedDto.Provider.VerificationStatus);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/providers/{own.Provider.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await aClient.PatchAsJsonAsync($"/api/admin/providers/{other.Provider.Id}/suspend", new ProviderModerationRequest("Tidak boleh lintas agency."), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await aClient.PatchAsync($"/api/admin/providers/{own.Provider.Id}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/providers/{own.Provider.Id}")).StatusCode);
        // Full direct-talent wizard; no agency is required to publish it.
        await Save(platform, direct.Provider.Id, "personal-info", personal);
        Assert.Equal(HttpStatusCode.BadRequest, (await platform.PostAsJsonAsync($"/api/admin/providers/{direct.Provider.Id}/address", address with { VillageId = "0000000000" })).StatusCode);
        await Save(platform, direct.Provider.Id, "address", address);
        using (var invalid = Form("KTP", [1, 2, 3])) Assert.Equal(HttpStatusCode.BadRequest, (await platform.PostAsync($"/api/admin/providers/{direct.Provider.Id}/documents", invalid)).StatusCode);
        foreach (var type in new[] { "KTP", "KK" }) { using var form = Form(type); direct = await Read<ProviderAdminDto>(await platform.PostAsync($"/api/admin/providers/{direct.Provider.Id}/documents", form)); }
        Assert.Equal("profile", direct.Step);
        Assert.Equal(HttpStatusCode.OK, (await platform.GetAsync($"/api/admin/providers/{direct.Provider.Id}/documents/{direct.Documents[0].Id}")).StatusCode);
        direct = await Save(platform, direct.Provider.Id, "profile", profile); Assert.Equal("verify", direct.Step);
        Assert.Equal(HttpStatusCode.Conflict, (await platform.DeleteAsync($"/api/admin/categories/{legacyCategory.Id}")).StatusCode);
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
        var registeredProvider = await Read<AuthResponse>(await guest.PostAsJsonAsync("/api/auth/provider/register", new ProviderRegistrationRequest("self-register@example.test", "Provider-password-long-42!", "Provider-password-long-42!"), Json));
        Assert.Equal("Provider", registeredProvider.User.Role);
        using var selfProvider = factory.CreateClient(); selfProvider.DefaultRequestHeaders.Authorization = new("Bearer", registeredProvider.AccessToken);
        Assert.Equal(ProviderApplicationStatus.Draft, (await Read<ProviderApplicationDto>(await selfProvider.GetAsync("/api/provider/application/status"))).Status);
        await Read<ProviderAdminDto>(await selfProvider.PostAsJsonAsync("/api/provider/personal-info", personal, Json));
        await Read<ProviderAdminDto>(await selfProvider.PostAsJsonAsync("/api/provider/address", address, Json));
        foreach (var type in new[] { "KTP", "KK" }) { using var form = Form(type); await Read<ProviderAdminDto>(await selfProvider.PostAsync("/api/provider/documents", form)); }
        await Read<ProviderAdminDto>(await selfProvider.PostAsJsonAsync("/api/provider/profile", profile, Json));
        Assert.Equal(ProviderApplicationStatus.Submitted, (await Read<ProviderApplicationDto>(await selfProvider.PostAsync("/api/provider/application/submit", null))).Status);
        Assert.Contains(await Read<ProviderAdminDto[]>(await platform.GetAsync("/api/admin/providers/applications")), x => x.Provider.Id == registeredProvider.User.Id);
        await Read<ProviderAdminDto>(await platform.PostAsJsonAsync($"/api/admin/providers/{registeredProvider.User.Id}/approve", new ProviderModerationRequest(null), Json));
        Assert.Equal(ProviderApplicationStatus.Approved, (await Read<ProviderApplicationDto>(await selfProvider.GetAsync("/api/provider/application/status"))).Status);
        var providerLoginResponse = await guest.PostAsJsonAsync("/api/auth/provider/login", new ProviderLoginRequest("direct-provider@example.test", "Provider-password-long-42!"), Json);
        var providerSession = await Read<AuthResponse>(providerLoginResponse);
        Assert.Equal("Provider", providerSession.User.Role);
        Assert.Equal(direct.Provider.Id.ToString(), new JwtSecurityTokenHandler().ReadJwtToken(providerSession.AccessToken).Claims.Single(c => c.Type == "providerId").Value);
        using var providerClient = factory.CreateClient(); providerClient.DefaultRequestHeaders.Authorization = new("Bearer", providerSession.AccessToken);
        var ownProfile = await Read<ProviderDto>(await providerClient.GetAsync("/api/provider/profile"));
        Assert.Equal(direct.Provider.Id, ownProfile.Id);
        var providerAvailability = Enum.GetValues<DayOfWeek>().Select(day => new AvailabilityRequest(day, day != DayOfWeek.Sunday)).ToArray();
        var updatedProvider = await Read<ProviderDto>(await providerClient.PutAsJsonAsync("/api/provider/availability", new AvailabilityUpdateRequest(providerAvailability), Json));
        Assert.False(updatedProvider.Availability.Single(a => a.DayOfWeek == DayOfWeek.Sunday).IsAvailable);
        Assert.Single(await Read<OrderDto[]>(await providerClient.GetAsync("/api/provider/orders")));
        Assert.Equal(HttpStatusCode.Forbidden, (await providerClient.GetAsync("/api/admin/providers")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await providerClient.PostAsJsonAsync("/api/auth/provider/change-password", new ChangePasswordRequest("Provider-password-long-42!", "Provider-password-new-42!"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/auth/provider/login", new ProviderLoginRequest("direct-provider@example.test", "Provider-password-long-42!"), Json)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.PostAsJsonAsync("/api/auth/provider/login", new ProviderLoginRequest("direct-provider@example.test", "Provider-password-new-42!"), Json)).StatusCode);
        await Read<ReviewDto>(await customer.PostAsJsonAsync($"/api/orders/{order.Id}/review", new ReviewRequest(5, "Layanan baik")));
        Assert.Equal(HttpStatusCode.Conflict, (await customer.PostAsJsonAsync($"/api/orders/{order.Id}/review", new ReviewRequest(1, "Duplikat"))).StatusCode);
        published = await Read<ProviderDto>(await guest.GetAsync($"/api/providers/{direct.Provider.Id}")); Assert.Equal(5, published.Rating); Assert.Equal(1, published.JobsCompletedCount);
        // Agency wizard followed by PlatformAdmin override must remain audited.
        await Save(aClient, own.Provider.Id, "personal-info", personal); await Save(aClient, own.Provider.Id, "address", address);
        foreach (var type in new[] { "KTP", "KK" }) { using var form = Form(type); await Read<ProviderAdminDto>(await aClient.PostAsync($"/api/admin/providers/{own.Provider.Id}/documents", form)); }
        await Save(aClient, own.Provider.Id, "profile", profile with { PricingType = PricingType.PerMonth, Price = 3000000 }); await Save(aClient, own.Provider.Id, "verify", verify);
        var agencyProviderLogin = await Read<AuthResponse>(await guest.PostAsJsonAsync("/api/auth/provider/login", new ProviderLoginRequest("agency-a-provider@example.test", "Provider-password-long-42!"), Json));
        using var agencyProviderClient = factory.CreateClient(); agencyProviderClient.DefaultRequestHeaders.Authorization = new("Bearer", agencyProviderLogin.AccessToken);
        Assert.Equal(own.Provider.Id, (await Read<ProviderDto>(await agencyProviderClient.GetAsync("/api/provider/profile"))).Id);
        Assert.Empty(await Read<OrderDto[]>(await agencyProviderClient.GetAsync("/api/provider/orders")));
        Assert.Equal(HttpStatusCode.OK, (await platform.PatchAsync($"/api/admin/agencies/{agencies[0].Id}/suspend", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/providers/{own.Provider.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await aClient.GetAsync("/api/admin/providers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await platform.GetAsync($"/api/admin/providers/{own.Provider.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await platform.PatchAsync($"/api/admin/agencies/{agencies[0].Id}/reactivate", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync($"/api/providers/{own.Provider.Id}")).StatusCode);
        await Save(platform, own.Provider.Id, "verify", verify with { Status = VerificationStatus.Rejected, Note = "Platform override" });
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync($"/api/providers/{own.Provider.Id}")).StatusCode);
        var overrideAudit = await Read<AuditLogEntry[]>(await platform.GetAsync("/api/admin/audit")); Assert.Contains(overrideAudit, x => x.Reason!.Contains("Platform override"));
        var audit = await Read<AuditLogEntry[]>(await platform.GetAsync("/api/admin/audit"));
        Assert.Contains(audit, x => x.Action == "agency.suspend" && x.TargetEntityType == "Agency" && x.TargetEntityId == agencies[0].Id && x.ActorRole == "PlatformAdmin");
        Assert.Contains(audit, x => x.Action == "agency.reactivate" && x.TargetEntityId == agencies[0].Id);
        Assert.Contains(audit, x => x.Action == "provider.suspend" && x.TargetEntityId == own.Provider.Id && x.Reason!.Contains("ditinjau"));
        Assert.Contains(audit, x => x.Action == "provider.reactivate" && x.TargetEntityId == own.Provider.Id);
        Assert.Contains(audit, x => x.Action == "provider.reject" && x.TargetEntityId == own.Provider.Id && x.Reason!.Contains("Platform override"));
        Assert.Contains(audit, x => x.Action == "agency-admin.create" && x.TargetEntityType == "AdminAccount");
    }
}
