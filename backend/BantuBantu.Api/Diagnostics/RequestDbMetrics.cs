namespace BantuBantu.Api.Diagnostics;

/// <summary>
/// Per-request accumulator for database work, resolved from the request scope
/// and filled in by <see cref="DbMetricsInterceptor"/>. Reads are lock-free so
/// the interceptor never contends on the hot path.
/// </summary>
public sealed class RequestDbMetrics
{
    private long dbTicks;
    private int sqlCount;

    public int SqlCount => Volatile.Read(ref sqlCount);

    public long DbMilliseconds => (long)TimeSpan.FromTicks(Interlocked.Read(ref dbTicks)).TotalMilliseconds;

    public void Record(long elapsedTicks)
    {
        Interlocked.Increment(ref sqlCount);
        if (elapsedTicks > 0) Interlocked.Add(ref dbTicks, elapsedTicks);
    }
}
