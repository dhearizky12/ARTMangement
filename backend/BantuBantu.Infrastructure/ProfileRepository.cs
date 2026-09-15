using BantuBantu.Application;
using BantuBantu.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace BantuBantu.Infrastructure;

public class ProfileRepository(AppDbContext db) : IProfileRepository
{
    public async Task<ProfileState> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.Role == UserRole.User, ct) ?? throw new AuthenticationFailedException();
        return new(user, await db.UserProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct),
            await db.UserAddresses.Include(x => x.Village).OrderBy(x => x.Id).FirstOrDefaultAsync(x => x.UserId == userId, ct),
            await db.UserDocuments.OrderByDescending(x => x.UploadedAt).FirstOrDefaultAsync(x => x.UserId == userId, ct));
    }
    public async Task<IProfileTransaction> LockAsync(Guid userId, CancellationToken ct)
    {
        var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Serialize mutations of one profile; concurrent submissions cannot skip or duplicate a step.
            await db.Users.FromSqlInterpolated($"SELECT * FROM \"Users\" WHERE \"Id\" = {userId} FOR UPDATE").ToListAsync(ct);
            return new ProfileTransaction(transaction);
        }
        catch { await transaction.DisposeAsync(); throw; }
    }
    public void Add(UserProfile profile) => db.UserProfiles.Add(profile);
    public void Add(UserAddress address) => db.UserAddresses.Add(address);
    public void Add(UserDocument document) => db.UserDocuments.Add(document);
    public async Task SaveAsync(CancellationToken ct) { await db.SaveChangesAsync(ct); }
    private sealed class ProfileTransaction(IDbContextTransaction transaction) : IProfileTransaction
    {
        public Task CommitAsync(CancellationToken ct) => transaction.CommitAsync(ct);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
public class CategoryRepository(AppDbContext db) : ICategoryRepository
{
    public async Task<IReadOnlyList<ServiceCategory>> ListActiveAsync(CancellationToken ct) => await db.ServiceCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);
}
