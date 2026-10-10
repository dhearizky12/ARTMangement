using BantuBantu.Application;
using BantuBantu.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
namespace BantuBantu.Tests;

public class MemoryReadCacheTests
{
    private static MemoryReadCache Cache() => new(new MemoryCache(new MemoryCacheOptions { SizeLimit = 64 }));

    [Fact]
    public async Task GetOrCreateAsync_CachesValue_UntilInvalidated()
    {
        var cache = Cache();
        var calls = 0;
        Task<int> Factory(CancellationToken ct) { calls++; return Task.FromResult(calls); }

        Assert.Equal(1, await cache.GetOrCreateAsync("k", TimeSpan.FromMinutes(1), Factory, default));
        Assert.Equal(1, await cache.GetOrCreateAsync("k", TimeSpan.FromMinutes(1), Factory, default));
        Assert.Equal(1, calls);

        cache.Invalidate("k");
        Assert.Equal(2, await cache.GetOrCreateAsync("k", TimeSpan.FromMinutes(1), Factory, default));
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetOrCreateAsync_CollapsesConcurrentMisses()
    {
        var cache = Cache();
        var calls = 0;
        var release = new TaskCompletionSource();
        async Task<int> Factory(CancellationToken ct) { Interlocked.Increment(ref calls); await release.Task; return 7; }

        var callers = Enumerable.Range(0, 25).Select(_ => cache.GetOrCreateAsync("k", TimeSpan.FromMinutes(1), Factory, default)).ToArray();
        release.SetResult();
        var results = await Task.WhenAll(callers);

        Assert.All(results, r => Assert.Equal(7, r));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Invalidate_RemovesEntry()
    {
        var cache = Cache();
        await cache.GetOrCreateAsync("k", TimeSpan.FromMinutes(1), _ => Task.FromResult(1), default);
        cache.Invalidate("k");
        Assert.Equal(2, await cache.GetOrCreateAsync("k", TimeSpan.FromMinutes(1), _ => Task.FromResult(2), default));
    }
}
