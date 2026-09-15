using BantuBantu.Application;
using BantuBantu.Domain;
using Microsoft.EntityFrameworkCore;
namespace BantuBantu.Infrastructure;
public class WilayahRepository(AppDbContext db) : IWilayahRepository
{
    private IQueryable<Village> Query() => db.Villages.AsNoTracking().Include(v => v.District).ThenInclude(d => d.Regency).ThenInclude(r => r.Province);
    private static VillageResult Map(Village v) => new(v.Id, v.Name, v.Type.ToString(), v.District.Name, v.District.Regency.Name, v.District.Regency.Province.Name, $"{v.Name}, {v.District.Name}, {v.District.Regency.Name}, {v.District.Regency.Province.Name}");
    public async Task<VillageResult?> FindAsync(string id, CancellationToken ct)
    {
        var village = await Query().SingleOrDefaultAsync(v => v.Id == id, ct);
        return village is null ? null : Map(village);
    }
    public async Task<IReadOnlyList<VillageResult>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        var escaped = query.Trim().Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
        if (escaped.Length < 2) return [];
        var villages = await Query().Where(v => EF.Functions.ILike(v.Name, "%" + escaped + "%", @"\"))
            .OrderBy(v => v.Name).ThenBy(v => v.Id).Take(Math.Clamp(limit, 1, 20)).ToListAsync(ct);
        return villages.Select(Map).ToArray();
    }
}
