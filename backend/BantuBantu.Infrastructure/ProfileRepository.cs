using BantuBantu.Application;
using BantuBantu.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace BantuBantu.Infrastructure;

public class CategoryRepository(AppDbContext db) : ICategoryRepository
{
    public async Task<IReadOnlyList<ServiceCategory>> ListActiveAsync(CancellationToken ct) => await db.ServiceCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);
}
