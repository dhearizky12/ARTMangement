namespace BantuBantu.Application;

// Small read-through cache for global, anonymous, rarely-changing catalog
// reads. Deliberately a seam: production uses an in-memory implementation,
// tests substitute a counting implementation to assert hit/miss behaviour.
public interface IReadCache
{
    Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct);
    void Invalidate(string key);
}

public static class ReadCacheKeys
{
    public const string ActiveCategories = "catalog:categories:active";
    public const string ActiveContent = "catalog:content:active";
    public static readonly TimeSpan CatalogTtl = TimeSpan.FromSeconds(60);
}
