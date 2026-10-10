using BantuBantu.Domain;
using BantuBantu.Application;
using Microsoft.EntityFrameworkCore;
namespace BantuBantu.Infrastructure;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<UserDocument> UserDocuments => Set<UserDocument>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<Regency> Regencies => Set<Regency>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Village> Villages => Set<Village>();
    public DbSet<AdminAccount> AdminAccounts => Set<AdminAccount>();
    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<ProviderCredential> ProviderCredentials => Set<ProviderCredential>();
    public DbSet<ProviderRefreshSession> ProviderRefreshSessions => Set<ProviderRefreshSession>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();
    public DbSet<AuditLogEntry> AuditEntries => Set<AuditLogEntry>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
    public DbSet<TwoFactorAccountState> TwoFactorAccountStates => Set<TwoFactorAccountState>();
    public DbSet<EmailOutbox> EmailOutboxes => Set<EmailOutbox>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().ToTable("Users");
        b.Entity<AdminAccount>().ToTable("AdminAccounts");
        b.Entity<AdminAccount>().HasOne(x => x.Agency).WithMany().HasForeignKey(x => x.AgencyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Agency>().Property(x => x.Status).HasConversion<string>();
        b.Entity<Provider>().Property(x => x.VerificationStatus).HasConversion<string>();
        b.Entity<Provider>().Property(x => x.ApplicationStatus).HasConversion<string>();
        b.Entity<Provider>().Property(x => x.PricingType).HasConversion<string>();
        b.Entity<Provider>().Property(x => x.Price).HasPrecision(18, 2);
        b.Entity<Provider>().Property(x => x.Version).IsRowVersion();
        b.Entity<Provider>().HasOne(x => x.Agency).WithMany().HasForeignKey(x => x.AgencyId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Provider>().HasOne(x => x.Village).WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProviderCredential>().HasKey(x => x.ProviderId);
        b.Entity<ProviderCredential>().HasOne(x => x.Provider).WithOne(x => x.Credential).HasForeignKey<ProviderCredential>(x => x.ProviderId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProviderCredential>().HasIndex(x => x.Email).IsUnique();
        b.Entity<ProviderRefreshSession>().HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProviderRefreshSession>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<ProviderCategory>().HasKey(x => new { x.ProviderId, x.ServiceCategoryId });
        b.Entity<ProviderCategory>().HasOne(x => x.ServiceCategory).WithMany().HasForeignKey(x => x.ServiceCategoryId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ProviderSkill>().HasKey(x => new { x.ProviderId, x.SkillName });
        b.Entity<ProviderLanguage>().HasKey(x => new { x.ProviderId, x.LanguageName });
        b.Entity<ProviderAvailability>().HasKey(x => new { x.ProviderId, x.DayOfWeek });
        b.Entity<ProviderDocument>().HasIndex(x => new { x.ProviderId, x.DocumentType }).IsUnique();
        b.Entity<Order>().Property(x => x.Status).HasConversion<string>();
        b.Entity<Order>().Property(x => x.PricingType).HasConversion<string>();
        b.Entity<Order>().Property(x => x.Price).HasPrecision(18, 2);
        b.Entity<Order>().Property(x => x.Version).IsRowVersion();
        b.Entity<Order>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Order>().HasOne(x => x.Provider).WithMany(x => x.Orders).HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Review>().HasOne(x => x.Order).WithOne(x => x.Review).HasForeignKey<Review>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Review>().HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Review>().HasOne(x => x.Provider).WithMany(x => x.Reviews).HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Review>().ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "\"Rating\" BETWEEN 1 AND 5"));
        b.Entity<Review>().HasIndex(x => new { x.ProviderId, x.IsHidden, x.CreatedAt });
        b.Entity<ContentBlock>().Property(x => x.IconName).HasMaxLength(80);
        b.Entity<ContentBlock>().HasData(
            new ContentBlock { Id = "trust-intro", Title = "Kepercayaan dimulai dari kejelasan.", Body = "Kami menjelaskan bagaimana penyedia jasa diperiksa sebelum Anda memesan layanan.", IconName = "shield-check", SortOrder = 1 },
            new ContentBlock { Id = "trust-identity", Title = "Identitas diperiksa", Body = "Kami memeriksa dokumen identitas penyedia dan mencocokkannya dengan data pendaftaran.", IconName = "badge-check", SortOrder = 2 },
            new ContentBlock { Id = "trust-background", Title = "Latar belakang ditinjau", Body = "Tim kami meninjau informasi latar belakang dan pengalaman kerja sebelum profil diterbitkan.", IconName = "search-check", SortOrder = 3 },
            new ContentBlock { Id = "trust-guarantee", Title = "Jaminan proses yang transparan", Body = "Lencana terverifikasi menunjukkan pemeriksaan telah selesai. Anda tetap dapat melihat detail profil, harga, ketersediaan, dan ulasan sebelum memesan.", IconName = "sparkles", SortOrder = 4 },
            new ContentBlock { Id = "trust-protection", Title = "Perlindungan pelanggan", Body = "Pesan melalui platform, simpan detail pesanan, dan laporkan masalah kepada tim Bantu-Bantu agar dapat ditindaklanjuti.", IconName = "heart-handshake", SortOrder = 5 },
            new ContentBlock { Id = "verification", Title = "Kenali proses verifikasi kami", Body = "Admin memeriksa identitas, latar belakang, dan kontrak sebelum profil penyedia diterbitkan.", IconName = "shield-check", SortOrder = 6 });
        b.HasPostgresExtension("pg_trgm");
        b.Entity<Province>().Property(x => x.Id).HasMaxLength(2);
        b.Entity<Regency>().Property(x => x.Id).HasMaxLength(4);
        b.Entity<District>().Property(x => x.Id).HasMaxLength(6);
        b.Entity<Village>().Property(x => x.Id).HasMaxLength(10);
        b.Entity<Village>().Property(x => x.Type).HasConversion<string>();
        b.Entity<Village>().HasIndex(x => x.Name);
        b.Entity<Village>().HasIndex(x => x.Name, "IX_Villages_Name_Trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
        b.Entity<UserAddress>().HasOne(x => x.Village).WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<ServiceCategory>().HasIndex(x => x.Slug).IsUnique();
        b.Entity<ServiceCategory>().HasData(
            new ServiceCategory { Id = Guid.Parse("ceaba520-c877-44b0-8950-a590c3277101"), Slug = "asisten-rumah-tangga", Name = "Asisten rumah tangga", Description = "Bantuan untuk rutinitas rumah, dari merapikan ruangan hingga kebutuhan harian keluarga.", IconKey = "house", SortOrder = 1, IsActive = true, IsFeatured = true },
            new ServiceCategory { Id = Guid.Parse("ceaba520-c877-44b0-8950-a590c3277102"), Slug = "driver", Name = "Driver", Description = "Kenali layanan pengemudi untuk perjalanan harian dan mobilitas keluarga Anda.", IconKey = "car", SortOrder = 2, IsActive = true, IsFeatured = true },
            new ServiceCategory { Id = Guid.Parse("ceaba520-c877-44b0-8950-a590c3277103"), Slug = "kebersihan", Name = "Kebersihan", Description = "Bantuan membersihkan rumah dan ruang kerja supaya hari terasa lebih nyaman.", IconKey = "sparkles", SortOrder = 3, IsActive = true },
            new ServiceCategory { Id = Guid.Parse("ceaba520-c877-44b0-8950-a590c3277104"), Slug = "perawatan-taman", Name = "Perawatan taman", Description = "Temukan jenis bantuan untuk merawat tanaman dan menjaga halaman tetap rapi.", IconKey = "sprout", SortOrder = 4, IsActive = true });
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<User>().Property(x => x.Role).HasConversion<string>();
        b.Entity<ExternalLogin>().HasIndex(x => new { x.Provider, x.ProviderKey }).IsUnique();
        b.Entity<ExternalLogin>().Property(x => x.RawProfileData).HasColumnType("jsonb");
        b.Entity<RefreshSession>().HasIndex(x => x.TokenHash).IsUnique();
        b.Entity<UserProfile>().HasKey(x => x.UserId);
        b.Entity<UserProfile>().HasOne(x => x.User).WithOne().HasForeignKey<UserProfile>(x => x.UserId);
        b.Entity<AuditLogEntry>().ToTable("AuditEntries");
        b.Entity<AuditLogEntry>().HasIndex(x => x.CreatedAt);
        b.Entity<AuditLogEntry>().HasIndex(x => new { x.TargetEntityType, x.TargetEntityId });
        b.Entity<TwoFactorChallenge>().Property(x => x.Role).HasConversion<string>();
        b.Entity<TwoFactorChallenge>().Property(x => x.Requirement).HasConversion<string>();
        b.Entity<TwoFactorChallenge>().HasIndex(x => new { x.AccountId, x.Requirement });
        b.Entity<TwoFactorAccountState>().HasKey(x => x.AccountId);
        b.Entity<TwoFactorAccountState>().Property(x => x.Role).HasConversion<string>();
        b.Entity<EmailOutbox>().Property(x => x.Status).HasConversion<string>();
        b.Entity<EmailOutbox>().HasIndex(x => x.DedupeKey).IsUnique();
        b.Entity<EmailOutbox>().HasIndex(x => new { x.Status, x.NextAttemptAt });
    }
}
public class AuthRepository(AppDbContext db) : IAuthRepository
{
    public Task<User?> FindGoogleAsync(string sub, CancellationToken ct) => db.ExternalLogins.Where(x => x.Provider == "Google" && x.ProviderKey == sub).Select(x => x.User).SingleOrDefaultAsync(ct);
    public async Task<User?> FindAdminAsync(string email, CancellationToken ct) => await db.AdminAccounts.SingleOrDefaultAsync(x => x.Email == email, ct);
    public Task<User?> FindUserAsync(Guid id, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ProviderCredential?> FindProviderCredentialAsync(string email, CancellationToken ct) => db.ProviderCredentials.Include(x => x.Provider).SingleOrDefaultAsync(x => x.Email == email, ct);
    public Task<ProviderCredential?> FindProviderCredentialByProviderAsync(Guid providerId, CancellationToken ct) => db.ProviderCredentials.Include(x => x.Provider).SingleOrDefaultAsync(x => x.ProviderId == providerId, ct);
    public Task<Provider?> FindProviderAsync(Guid id, CancellationToken ct) => db.Providers.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddProvider(Provider provider) => db.Providers.Add(provider);
    public async Task<bool> CanAuthenticateAsync(User user, CancellationToken ct) => user.Role switch
    {
        UserRole.Customer => user is not AdminAccount,
        UserRole.PlatformAdmin => user is AdminAccount { AgencyId: null },
        UserRole.AgencyAdmin => user is AdminAccount admin && admin.AgencyId != null && await db.Agencies.AnyAsync(a => a.Id == admin.AgencyId && a.Status == AgencyStatus.Approved, ct),
        _ => false
    };
    public Task<bool> CanAuthenticateProviderAsync(Guid providerId, CancellationToken ct) => db.Providers.AnyAsync(p => p.Id == providerId && p.Credential != null && p.ApplicationStatus != ProviderApplicationStatus.Suspended && (p.AgencyId == null || p.Agency!.Status == AgencyStatus.Approved), ct);
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) => db.Users.AnyAsync(x => x.Email == email, ct);
    public void AddUser(User user, ExternalLogin? login = null) { db.Users.Add(user); if (login is not null) db.ExternalLogins.Add(login); }
    public void AddSession(RefreshSession session) => db.RefreshSessions.Add(session);
    public void AddProviderSession(ProviderRefreshSession session) => db.ProviderRefreshSessions.Add(session);
    public async Task<RefreshSession?> ConsumeSessionAsync(string hash, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var changed = await db.RefreshSessions.Where(x => x.TokenHash == hash && x.RevokedAt == null && x.ExpiresAt > now).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
        return changed == 1 ? await db.RefreshSessions.Include(x => x.User).SingleAsync(x => x.TokenHash == hash, ct) : null;
    }
    public async Task<ProviderRefreshSession?> ConsumeProviderSessionAsync(string hash, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var changed = await db.ProviderRefreshSessions.Where(x => x.TokenHash == hash && x.RevokedAt == null && x.ExpiresAt > now).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
        return changed == 1 ? await db.ProviderRefreshSessions.Include(x => x.Provider).ThenInclude(x => x.Credential).SingleAsync(x => x.TokenHash == hash, ct) : null;
    }
    public async Task SaveAsync(CancellationToken ct) { await db.SaveChangesAsync(ct); }
    public async Task InvalidateTwoFactorChallengesAsync(Guid accountId, UserRole role, TwoFactorRequirement requirement, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await db.TwoFactorChallenges.Where(x => x.AccountId == accountId && x.Role == role && x.Requirement == requirement && x.VerifiedAt == null && x.InvalidatedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.InvalidatedAt, now), ct);
    }
    public Task<TwoFactorChallenge?> FindTwoFactorChallengeAsync(Guid id, CancellationToken ct) => db.TwoFactorChallenges.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddTwoFactorChallenge(TwoFactorChallenge challenge) => db.TwoFactorChallenges.Add(challenge);
    public Task<TwoFactorAccountState?> FindTwoFactorAccountStateAsync(Guid accountId, CancellationToken ct) => db.TwoFactorAccountStates.SingleOrDefaultAsync(x => x.AccountId == accountId, ct);
    public void AddTwoFactorAccountState(TwoFactorAccountState state) => db.TwoFactorAccountStates.Add(state);
}
