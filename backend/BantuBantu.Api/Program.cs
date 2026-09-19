using System.Threading.RateLimiting;
using BantuBantu.Application;
using BantuBantu.Infrastructure;
using BantuBantu.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BantuBantu.Api;
var builder = WebApplication.CreateBuilder(args);
var databaseConnection = new DatabaseConnectionStringResolver().Resolve(builder.Configuration);
var originPolicy = AllowedOriginPolicy.FromConfiguration(builder.Configuration);
var storageProvider = (builder.Configuration["Storage:Provider"] ?? "Local").Trim();
if (!storageProvider.Equals("Local", StringComparison.OrdinalIgnoreCase) && !storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("Storage:Provider must be Local or S3.");
string[] required = ["Google:ClientId", "Jwt:KeyId", "Jwt:Issuer", "Jwt:Audience", "Jwt:AccessMinutes", "Jwt:RefreshDays", "Frontend:Origin"];
foreach (var key in required) if (string.IsNullOrWhiteSpace(builder.Configuration[key])) throw new InvalidOperationException($"Missing configuration: {key}");
if (new[] { "PrivateKey", "PublicKey" }.Any(name => new[] { $"Jwt:{name}Base64", $"Jwt:{name}", $"Jwt:{name}Path" }.All(key => string.IsNullOrWhiteSpace(builder.Configuration[key]))))
    throw new InvalidOperationException("Configure both JWT RSA key materials using Base64 PEM, raw PEM, or file paths.");
if (storageProvider.Equals("Local", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(builder.Configuration["Storage:RootPath"])) throw new InvalidOperationException("Missing configuration: Storage:RootPath");
if (storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
    foreach (var key in new[] { "Storage:S3:ServiceUrl", "Storage:S3:AccessKey", "Storage:S3:SecretKey", "Storage:S3:Bucket" })
        if (string.IsNullOrWhiteSpace(builder.Configuration[key])) throw new InvalidOperationException($"Missing configuration: {key}");
foreach (var key in new[] { "Jwt:AccessMinutes", "Jwt:RefreshDays" }) if (!int.TryParse(builder.Configuration[key], out var value) || value < 1) throw new InvalidOperationException($"Invalid configuration: {key}");
var origin = originPolicy.PrimaryOrigin;
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddScoped<IWilayahRepository, WilayahRepository>();
builder.Services.AddSingleton<IDocumentProcessor, DocumentProcessor>();
builder.Services.AddSingleton<IFileStorage>(_ => storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase)
    ? new S3FileStorage(builder.Configuration)
    : new LocalFileStorage(builder.Configuration));
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, CurrentActor>();
builder.Services.AddScoped<IProviderScope, ProviderScope>();
builder.Services.AddScoped<IMarketplaceRepository, MarketplaceRepository>();
builder.Services.AddScoped<ProviderService>();
builder.Services.AddScoped<AgencyService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(databaseConnection));
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<RsaKeys>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.SetIsOriginAllowed(originPolicy.IsAllowed).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<RsaKeys>((o, keys) =>
{
    o.MapInboundClaims = false;
    o.TokenValidationParameters = new() { ValidateIssuerSigningKey = true, IssuerSigningKey = keys.ValidationKey, ValidateIssuer = true, ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidateAudience = true, ValidAudience = builder.Configuration["Jwt:Audience"], ValidateLifetime = true, RequireExpirationTime = true, ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.FromSeconds(30), RoleClaimType = "role", NameClaimType = "sub" };
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("upload", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
var app = builder.Build();
if (args.Contains("--seed-admin"))
{
    using var scope = app.Services.CreateScope();
    var repository = scope.ServiceProvider.GetRequiredService<IAuthRepository>();
    var email = builder.Configuration["AdminSeed:Email"]?.Trim().ToLowerInvariant(); var password = builder.Configuration["AdminSeed:Password"];
    if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email) || password is null || password.Length < 14) throw new InvalidOperationException("Set AdminSeed:Email and AdminSeed:Password (at least 14 characters).");
    if (await repository.EmailExistsAsync(email, default)) throw new InvalidOperationException("Email already exists; no account was modified.");
    var user = new AdminAccount { Email = email, FullName = "Administrator", Role = UserRole.PlatformAdmin, ProfileCompleted = true };
    user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(user, password);
    repository.AddUser(user); await repository.SaveAsync(default); return;
}
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (ProfileException error) { context.Response.StatusCode = error.Status; await context.Response.WriteAsJsonAsync(new { title = error.Message, status = error.Status, code = error.Code, profileStep = error.Step }); }
    catch (AuthenticationFailedException) { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { title = "Autentikasi gagal. Silakan login kembali.", status = 401 }); }
    catch (DbUpdateConcurrencyException) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { title = "Data berubah. Muat ulang sebelum menyimpan." }); }
    catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { title = "Akun sedang diproses atau sudah terdaftar. Coba login kembali.", status = 409 }); }
});
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
// Reject stale role/agency claims and suspended agencies on every authenticated request.
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var repo = context.RequestServices.GetRequiredService<IAuthRepository>();
        var id = Guid.TryParse(context.User.FindFirst("sub")?.Value, out var parsed) ? parsed : Guid.Empty;
        var user = await repo.FindUserAsync(id, context.RequestAborted);
        if (user is null || context.User.FindFirst("role")?.Value != user.Role.ToString() ||
            context.User.FindFirst("agencyId")?.Value != (user as AdminAccount)?.AgencyId?.ToString() ||
            !await repo.CanAuthenticateAsync(user, context.RequestAborted)) { context.Response.StatusCode = 401; return; }
    }
    await next();
});
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/.well-known/jwks.json", (RsaKeys keys) =>
{
    var key = JsonWebKeyConverter.ConvertFromRSASecurityKey(keys.ValidationKey);
    return Results.Ok(new { keys = new[] { new { kty = key.Kty, kid = key.Kid, n = key.N, e = key.E, alg = "RS256", use = "sig" } } });
});
app.Run();
public partial class Program { }
