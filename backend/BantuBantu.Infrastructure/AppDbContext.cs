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
    protected override void OnModelCreating(ModelBuilder b)
    {
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
    }
}
public class AuthRepository(AppDbContext db) : IAuthRepository
{
    public Task<User?> FindGoogleAsync(string sub, CancellationToken ct) => db.ExternalLogins.Where(x => x.Provider == "Google" && x.ProviderKey == sub).Select(x => x.User).SingleOrDefaultAsync(ct);
    public Task<User?> FindAdminAsync(string email, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.Email == email && x.Role == UserRole.Admin, ct);
    public Task<User?> FindUserAsync(Guid id, CancellationToken ct) => db.Users.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) => db.Users.AnyAsync(x => x.Email == email, ct);
    public void AddUser(User user, ExternalLogin? login = null) { db.Users.Add(user); if (login is not null) db.ExternalLogins.Add(login); }
    public void AddSession(RefreshSession session) => db.RefreshSessions.Add(session);
    public async Task<RefreshSession?> ConsumeSessionAsync(string hash, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var changed = await db.RefreshSessions.Where(x => x.TokenHash == hash && x.RevokedAt == null && x.ExpiresAt > now).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
        return changed == 1 ? await db.RefreshSessions.Include(x => x.User).SingleAsync(x => x.TokenHash == hash, ct) : null;
    }
    public async Task SaveAsync(CancellationToken ct) { await db.SaveChangesAsync(ct); }
}
