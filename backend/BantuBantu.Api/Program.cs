using System.Threading.RateLimiting;
using System.Diagnostics;
using BantuBantu.Application;
using BantuBantu.Infrastructure;
using BantuBantu.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using BantuBantu.Api;
using BantuBantu.Infrastructure.Notifications;
var builder = WebApplication.CreateBuilder(args);
var seedMode = args.Contains("--seed-admin") || args.Contains("--seed-accounts");
var databaseConnection = new DatabaseConnectionStringResolver().Resolve(builder.Configuration);
var originPolicy = seedMode ? null : AllowedOriginPolicy.FromConfiguration(builder.Configuration);
var twoFactor = TwoFactorSettingsFactory.FromConfiguration(builder.Configuration);
var (notifications, appSettings, emailSettings) = NotificationSettingsFactory.FromConfiguration(builder.Configuration);
var storageProvider = (builder.Configuration["Storage:Provider"] ?? "Local").Trim();
if (!seedMode)
{
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
    // Email verification needs a sending-only Resend key (Resend:ApiKey) and a
    // from-address on a verified domain. Both are required exactly when this
    // environment is allowed to send mail; deploy-be.sh enforces the same rule.
    if (string.Equals(builder.Configuration["EmailVerification:Enabled"], "true", StringComparison.OrdinalIgnoreCase))
        foreach (var key in new[] { "Resend:ApiKey", "Resend:From" })
            if (string.IsNullOrWhiteSpace(builder.Configuration[key]))
                throw new InvalidOperationException($"Missing configuration: {key} (required when EmailVerification:Enabled=true). Run scripts/provision-resend-key.sh and set RESEND_FROM.");
    // Two-factor authentication sends a real email code. It will not start
    // outside Development unless a sending-only Resend key and a from-address
    // are configured (deploy-be.sh enforces the same rule, and the frontend
    // cap is that 2FA sessions can never send mail with a missing key).
    if (twoFactor.Enabled && !builder.Environment.IsDevelopment())
        foreach (var key in new[] { "Resend:ApiKey", "Resend:From" })
            if (string.IsNullOrWhiteSpace(builder.Configuration[key]))
                throw new InvalidOperationException($"Missing configuration: {key} (required when TwoFactor:Enabled=true outside Development). Run scripts/provision-resend-key.sh and set RESEND_FROM.");
    foreach (var key in new[] { "Jwt:AccessMinutes", "Jwt:RefreshDays" }) if (!int.TryParse(builder.Configuration[key], out var value) || value < 1) throw new InvalidOperationException($"Invalid configuration: {key}");
    if (!twoFactor.Enabled && !builder.Environment.IsDevelopment())
        Console.Error.WriteLine("WARNING: TwoFactor:Enabled=false. Admin and provider logins do NOT require an email code.");
    // Transactional business notifications go through the same Resend sending
    // path. Fail fast when Notifications are switched on without a key + from-address.
    if (notifications.Enabled && !builder.Environment.IsDevelopment())
        foreach (var key in new[] { "Resend:ApiKey", "Resend:From" })
            if (string.IsNullOrWhiteSpace(builder.Configuration[key]))
                throw new InvalidOperationException($"Missing configuration: {key} (required when Notifications:Enabled=true outside Development). Run scripts/provision-resend-key.sh and set RESEND_FROM.");
    if (!notifications.Enabled && !builder.Environment.IsDevelopment())
        Console.Error.WriteLine("WARNING: Notifications:Enabled=false. No transactional emails will be sent.");
    // Provider notification addresses are gated on EmailVerifiedAt only while 2FA is on
    // (the 2FA code verification is the only writer of that Notifications field).
    if (notifications.Enabled && !twoFactor.Enabled)
        Console.Error.WriteLine("WARNING: Notifications are enabled while TwoFactor:Enabled=false, so provider emails are sent without EmailVerifiedAt gating.");
}
builder.Logging.AddProvider(new JsonFileLoggerProvider(builder.Configuration));
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddScoped<IWilayahRepository, WilayahRepository>();
builder.Services.AddSingleton<IDocumentProcessor, DocumentProcessor>();
builder.Services.AddSingleton<IFileStorage>(_ => storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase)
    ? new S3FileStorage(builder.Configuration)
    : new LocalFileStorage(builder.Configuration));
// Email: Development logs instead of sending (no Resend key needed locally).
// Everywhere else mail goes through Resend with the sending-only key from
// Resend:ApiKey; the sender address comes from the configurable Resend:From.
if (builder.Environment.EnvironmentName == "Development")
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
else
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(client => client.BaseAddress = new Uri("https://api.resend.com/"));
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
{
    var traceId = string.IsNullOrWhiteSpace(context.HttpContext.TraceIdentifier)
        ? Activity.Current?.TraceId.ToString() ?? "unknown"
        : context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Extensions["traceId"] = traceId;
    context.HttpContext.Response.Headers["X-Correlation-ID"] = traceId;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, CurrentActor>();
builder.Services.AddScoped<IProviderScope, ProviderScope>();
builder.Services.AddScoped<IMarketplaceRepository, MarketplaceRepository>();
builder.Services.AddScoped<ProviderService>();
builder.Services.AddScoped<AgencyService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(databaseConnection, npgsql =>
    npgsql.EnableRetryOnFailure(
        maxRetryCount: 3,
        maxRetryDelay: TimeSpan.FromSeconds(5),
        errorCodesToAdd: null)));
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IGoogleIdentityVerifier, GoogleIdentityVerifier>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddScoped<DemoAccountSeeder>();
builder.Services.AddSingleton<RsaKeys>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddSingleton(twoFactor);
builder.Services.AddSingleton(notifications);
builder.Services.AddSingleton(appSettings);
builder.Services.AddSingleton(emailSettings);
builder.Services.AddScoped<INotificationOutbox, NotificationOutbox>();
builder.Services.AddScoped<INotificationData, NotificationData>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<INotificationRenderer, NotificationRenderer>();
builder.Services.AddHostedService<EmailOutboxWorker>();
builder.Services.AddSingleton<IEmailOtpService, EmailOtpService>();
if (originPolicy is null)
    builder.Services.AddCors();
else
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
    o.AddPolicy("2fa", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
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
if (args.Contains("--preview-mail"))
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("--preview-mail is Development-only.");
    using var scope = app.Services.CreateScope();
    var renderer = scope.ServiceProvider.GetRequiredService<INotificationRenderer>();
    var dir = Path.Combine(app.Environment.ContentRootPath, "App_Data", "mail-preview");
    Directory.CreateDirectory(dir);
    foreach (var (templateKey, payloadJson) in renderer.PreviewSamples())
    {
        var rendered = renderer.Render(templateKey, System.Text.Json.JsonDocument.Parse(payloadJson).RootElement);
        var file = Path.Combine(dir, templateKey + ".html");
        await File.WriteAllTextAsync(file, rendered.Html);
        Console.WriteLine($"preview: {templateKey} -> {file} [{rendered.Subject}]");
    }
    return;
}
if (args.Contains("--seed-accounts"))
{
    using var scope = app.Services.CreateScope();
    var options = DemoAccountSeedOptions.FromConfiguration(builder.Configuration);
    await scope.ServiceProvider.GetRequiredService<DemoAccountSeeder>().SeedAsync(options);
    return;
}
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    context.Response.Headers["X-Correlation-ID"] = traceId;
    context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("BantuBantu.UnhandledException")
        .LogError(error, "Unhandled HTTP exception. TraceId={TraceId} Method={Method} Path={Path}", traceId, context.Request.Method, context.Request.Path);
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(new
    {
        type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
        title = "An error occurred while processing your request.",
        status = StatusCodes.Status500InternalServerError,
        traceId
    });
}));
app.Use(async (context, next) =>
{
    var incoming = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
    var traceId = IsSafeCorrelationId(incoming)
        ? incoming!
        : Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    context.TraceIdentifier = traceId;
    context.Response.Headers["X-Correlation-ID"] = traceId;
    try { await next(); }
    catch (ProfileException error)
    {
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BantuBantu.Validation")
            .LogWarning("Request validation failed. TraceId={TraceId} Status={Status} Code={Code} Path={Path}", traceId, error.Status, error.Code, context.Request.Path);
        context.Response.StatusCode = error.Status;
        await context.Response.WriteAsJsonAsync(new { title = error.Message, status = error.Status, code = error.Code, profileStep = error.Step, traceId });
    }
    catch (AuthenticationFailedException)
    {
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BantuBantu.Authentication")
            .LogWarning("Authentication failed. TraceId={TraceId} Path={Path}", traceId, context.Request.Path);
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new { title = "Autentikasi gagal. Silakan login kembali.", status = 401, traceId });
    }
    catch (DbUpdateConcurrencyException error)
    {
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BantuBantu.Database")
            .LogWarning(error, "Database concurrency conflict. TraceId={TraceId} Path={Path}", traceId, context.Request.Path);
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { title = "Data berubah. Muat ulang sebelum menyimpan.", status = 409, traceId });
    }
    catch (DbUpdateException error) when (error.InnerException is Npgsql.PostgresException { SqlState: "23505" })
    {
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("BantuBantu.Database")
            .LogWarning(error, "Database uniqueness conflict. TraceId={TraceId} Path={Path}", traceId, context.Request.Path);
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { title = "Akun sedang diproses atau sudah terdaftar. Coba login kembali.", status = 409, traceId });
    }
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
        var role = context.User.FindFirst("role")?.Value;
        if (role == UserRole.Provider.ToString())
        {
            var providerId = Guid.TryParse(context.User.FindFirst("providerId")?.Value, out var provider) ? provider : Guid.Empty;
            if (id == Guid.Empty || providerId == Guid.Empty || id != providerId || !await repo.CanAuthenticateProviderAsync(providerId, context.RequestAborted)) { context.Response.StatusCode = 401; return; }
        }
        else
        {
            var user = await repo.FindUserAsync(id, context.RequestAborted);
            if (user is null || role != user.Role.ToString() ||
                context.User.FindFirst("agencyId")?.Value != (user as AdminAccount)?.AgencyId?.ToString() ||
                !await repo.CanAuthenticateAsync(user, context.RequestAborted)) { context.Response.StatusCode = 401; return; }
        }
    }
    await next();
});
// While 2FA is enabled, admin/provider access tokens with no session level
// (issued before rollout) are rejected: the client must log in again.
app.Use(async (context, next) =>
{
    if (twoFactor.Enabled && context.User.Identity?.IsAuthenticated == true)
    {
        var role = context.User.FindFirst("role")?.Value;
        if (role is "PlatformAdmin" or "AgencyAdmin" or "Provider" &&
            !context.User.HasClaim("amr", "email_otp") && !context.User.HasClaim("limited", "true"))
        {
            context.Response.StatusCode = 401;
            return;
        }
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
static bool IsSafeCorrelationId(string? value) =>
    !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.');

app.Run();
public partial class Program { }
