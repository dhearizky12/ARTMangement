using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BantuBantu.Api.Diagnostics;

/// <summary>
/// Times EF Core command execution and records it on the current request's
/// <see cref="RequestDbMetrics"/>. The interceptor is a singleton, so the
/// scoped accumulator is reached through <see cref="IHttpContextAccessor"/>.
/// Each command instance is timed individually via a weak table, which keeps
/// interleaved commands correct. All work is skipped unless
/// <see cref="DiagnosticsSettings.ServerTiming"/> is on.
/// </summary>
public sealed class DbMetricsInterceptor(IHttpContextAccessor accessor, DiagnosticsSettings settings) : DbCommandInterceptor
{
    private static readonly ConditionalWeakTable<DbCommand, Stopwatch> Timers = new();

    private RequestDbMetrics? Metrics => accessor.HttpContext?.RequestServices.GetService<RequestDbMetrics>();

    private void Start(DbCommand command)
    {
        Timers.Remove(command);
        Timers.Add(command, Stopwatch.StartNew());
    }

    private void Stop(DbCommand command)
    {
        if (!Timers.TryGetValue(command, out var watch)) return;
        Timers.Remove(command);
        watch.Stop();
        Metrics?.Record(watch.Elapsed.Ticks);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        if (settings.ServerTiming) Start(command);
        return result;
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Stop(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (settings.ServerTiming) Start(command);
        return new ValueTask<InterceptionResult<DbDataReader>>(result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Stop(command);
        return new ValueTask<DbDataReader>(result);
    }

    public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        if (settings.ServerTiming) Start(command);
        return result;
    }

    public override object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Stop(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
    {
        if (settings.ServerTiming) Start(command);
        return new ValueTask<InterceptionResult<object>>(result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Stop(command);
        return new ValueTask<object?>(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        if (settings.ServerTiming) Start(command);
        return result;
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Stop(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (settings.ServerTiming) Start(command);
        return new ValueTask<InterceptionResult<int>>(result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Stop(command);
        return new ValueTask<int>(result);
    }
}
