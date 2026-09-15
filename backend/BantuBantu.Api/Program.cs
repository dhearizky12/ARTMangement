using BantuBantu.Api.Filters;
using System.Threading.RateLimiting;
using BantuBantu.Application;
using BantuBantu.Infrastructure;
using BantuBantu.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);
string[] required = ["ConnectionStrings:Default", "Google:ClientId", "Jwt:PrivateKeyPath", "Jwt:PublicKeyPath", "Jwt:KeyId", "Jwt:Issuer", "Jwt:Audience", "Jwt:AccessMinutes", "Jwt:RefreshDays", "Frontend:Origin", "Auth:CookieSecure", "Auth:CookieSameSite", "Storage:RootPath"];
foreach (var key in required) if (string.IsNullOrWhiteSpace(builder.Configuration[key])) throw new InvalidOperationException($"Missing configuration: {key}");
foreach (var key in new[] { "Jwt:AccessMinutes", "Jwt:RefreshDays" }) if (!int.TryParse(builder.Configuration[key], out var value) || value < 1) throw new InvalidOperationException($"Invalid configuration: {key}");
var secure = bool.Parse(builder.Configuration["Auth:CookieSecure"]!);
var sameSite = Enum.Parse<SameSiteMode>(builder.Configuration["Auth:CookieSameSite"]!, true);
if ((!builder.Environment.IsDevelopment() && !secure) || (sameSite == SameSiteMode.None && !secure)) throw new InvalidOperationException("Secure cookies required outside Development and with SameSite=None.");
var origin = builder.Configuration["Frontend:Origin"]!;
if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.GetLeftPart(UriPartial.Authority) != origin) throw new InvalidOperationException("Frontend:Origin must be an exact origin without a trailing slash.");
builder.Services.AddControllers(options => options.Filters.Add<CompletedProfileFilter>());
builder.Services.AddScoped<CompletedProfileFilter>();
builder.Services.AddScoped<IWilayahRepository, WilayahRepository>();
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddSingleton<IDocumentProcessor, DocumentProcessor>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<RsaKeys>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(origin).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
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
    var user = new User { Email = email, FullName = "Administrator", Role = UserRole.Admin, ProfileCompleted = true };
    user.PasswordHash = scope.ServiceProvider.GetRequiredService<IPasswordService>().Hash(user, password);
    repository.AddUser(user); await repository.SaveAsync(default); return;
}
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (ProfileException error) { context.Response.StatusCode = error.Status; await context.Response.WriteAsJsonAsync(new { title = error.Message, status = error.Status, code = error.Code, profileStep = error.Step }); }
    catch (AuthenticationFailedException) { context.Response.StatusCode = 401; await context.Response.WriteAsJsonAsync(new { title = "Autentikasi gagal. Silakan login kembali.", status = 401 }); }
    catch (DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { context.Response.StatusCode = 409; await context.Response.WriteAsJsonAsync(new { title = "Akun sedang diproses atau sudah terdaftar. Coba login kembali.", status = 409 }); }
});
app.UseCors();
// Cookie-authenticated mutations require an exact allowed browser origin, including login (login CSRF).
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/auth") && HttpMethods.IsPost(context.Request.Method) && context.Request.Headers.Origin.ToString() != origin) { context.Response.StatusCode = 403; return; }
    await next();
});
app.UseRateLimiter();
app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/.well-known/jwks.json", (RsaKeys keys) =>
{
    var key = JsonWebKeyConverter.ConvertFromRSASecurityKey(keys.ValidationKey);
    return Results.Ok(new { keys = new[] { new { kty = key.Kty, kid = key.Kid, n = key.N, e = key.E, alg = "RS256", use = "sig" } } });
});
app.Run();
public partial class Program { }
