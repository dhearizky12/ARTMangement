using System.Collections.Concurrent;
using BantuBantu.Application;
using Microsoft.Extensions.Caching.Memory;
namespace BantuBantu.Infrastructure;

// Bounded read-through cache. A per-key gate collapses concurrent misses into
// a single factory call so a cold cache cannot stampede the database.
public sealed class MemoryReadCache(IMemoryCache cache) : IReadCache
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new();

    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<CancellationToken, Task<T>> factory, CancellationToken ct)
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null) return cached;
        var gate = _gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(key, out cached) && cached is not null) return cached;
            var value = await factory(ct);
            cache.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl, Size = 1 });
            return value;
        }
        finally { gate.Release(); }
    }

    public void Invalidate(string key) => cache.Remove(key);
}
