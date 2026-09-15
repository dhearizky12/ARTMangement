using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using BantuBantu.Application;
using BantuBantu.Domain;
using BantuBantu.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
namespace BantuBantu.Tests;

public partial class AuthIntegrationTests
{
    private static async Task ProfileLifecycle(HttpClient client, AuthFactory factory, AuthResponse originalSession, string adminToken)
    {
        Assert.Equal("personal", (await client.GetFromJsonAsync<ProfileStatusDto>("/api/profile/status"))!.ProfileStep);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/service-categories")).StatusCode);
        var address = new AddressRequest("3374011003", "Jalan Pengujian nomor 10, RT 01 RW 02", "12810");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/profile/address", address)).StatusCode);
        var personal = new PersonalInfoRequest("Nama Pengguna", new DateOnly(1995, 3, 10), "081234567890", "female");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/profile/personal-info", personal with { BirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/profile/personal-info", personal with { PhoneNumber = "abc" })).StatusCode);
        var concurrent = await Task.WhenAll(client.PostAsJsonAsync("/api/profile/personal-info", personal), client.PostAsJsonAsync("/api/profile/personal-info", personal));
        Assert.Single(concurrent, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(concurrent, r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal("address", (await client.GetFromJsonAsync<ProfileStatusDto>("/api/profile/status"))!.ProfileStep);
        Assert.Equal("address", (await client.GetFromJsonAsync<UserDto>("/api/auth/me"))!.ProfileStep);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/profile/address", address with { PostalCode = "abc" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/profile/address", address with { VillageId = "0000000000" })).StatusCode);
        var matches = await client.GetFromJsonAsync<VillageResult[]>("/api/wilayah/search?q=Sekaran&limit=100");
        Assert.InRange(matches!.Length, 2, 20);
        Assert.True(matches.Select(x => x.ProvinceName).Distinct().Count() > 1);
        Assert.All(matches, x => Assert.Contains(x.ProvinceName, x.DisplayLabel));
        Assert.Empty((await client.GetFromJsonAsync<VillageResult[]>("/api/wilayah/search?q=%25%25"))!);
        (await client.PostAsJsonAsync("/api/profile/address", address)).EnsureSuccessStatusCode();
        Assert.Equal("documents", (await client.GetFromJsonAsync<ProfileStatusDto>("/api/profile/status"))!.ProfileStep);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/user/dashboard")).StatusCode);
        using var picture = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(200, 120);
        using var imageStream = new MemoryStream();
        await picture.SaveAsync(imageStream, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        var bytes = imageStream.ToArray();
        static MultipartFormDataContent Form(byte[] bytes, string mime = "image/png", string type = "KTP")
        {
            var form = new MultipartFormDataContent();
            form.Add(new StringContent(type), "documentType");
            var file = new ByteArrayContent(bytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime);
            form.Add(file, "file", "../../identity.png");
            return form;
        }
        using (var invalid = Form([1, 2, 3])) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/profile/documents", invalid)).StatusCode);
        using (var wrongMime = Form(bytes, "image/jpeg")) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/profile/documents", wrongMime)).StatusCode);
        using (var wrongType = Form(bytes, type: "Script")) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/profile/documents", wrongType)).StatusCode);
        using (var tooLarge = Form(new byte[DocumentProcessor.MaxBytes + 1])) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/profile/documents", tooLarge)).StatusCode);
        using (var valid = Form(bytes)) (await client.PostAsync("/api/profile/documents", valid)).EnsureSuccessStatusCode();
        var completed = await client.GetFromJsonAsync<ProfileStatusDto>("/api/profile/status");
        Assert.True(completed!.ProfileCompleted); Assert.Equal("done", completed.ProfileStep);
        Assert.Equal("Pending", completed.Document!.VerificationStatus);
        Assert.Equal("false", new JwtSecurityTokenHandler().ReadJwtToken(originalSession.AccessToken).Claims.Single(c => c.Type == "profileCompleted").Value);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/user/dashboard")).StatusCode);
        using (var duplicate = Form(bytes)) Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/api/profile/documents", duplicate)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await db.UserProfiles.CountAsync());
            Assert.Equal(1, await db.UserAddresses.CountAsync());
            var document = await db.UserDocuments.SingleAsync();
            Assert.Matches(@"^[a-f0-9]{32}\.jpg$", document.StorageKey);
            var saved = Path.Combine(factory.DocumentPath, document.StorageKey);
            Assert.True(File.Exists(saved));
            Assert.Equal("image/jpeg", Image.DetectFormat(saved).DefaultMimeType);
            Assert.Single(Directory.GetFiles(factory.DocumentPath));
            db.ServiceCategories.Add(new ServiceCategory { Id = Guid.NewGuid(), Slug = "test-new-service", Name = "Layanan baru", Description = "Kategori ditambah lewat database", IconKey = "unknown-icon", SortOrder = 99, IsActive = true });
            db.ServiceCategories.Add(new ServiceCategory { Id = Guid.NewGuid(), Slug = "hidden-service", Name = "Tidak aktif", IsActive = false });
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/" + document.StorageKey)).StatusCode);
        }
        var categories = await client.GetFromJsonAsync<ServiceCategoryDto[]>("/api/service-categories");
        Assert.Contains(categories!, c => c.Slug == "test-new-service");
        Assert.DoesNotContain(categories!, c => c.Slug == "hidden-service");
        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await adminClient.GetAsync("/api/profile/status")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/admin/dashboard")).StatusCode);
    }
}
