namespace BantuBantu.Application;

public record VillageResult(string VillageId, string VillageName, string VillageType, string DistrictName, string RegencyName, string ProvinceName, string DisplayLabel);
public interface IWilayahRepository
{
    Task<IReadOnlyList<VillageResult>> SearchAsync(string query, int limit, CancellationToken ct);
    Task<VillageResult?> FindAsync(string id, CancellationToken ct);
}
