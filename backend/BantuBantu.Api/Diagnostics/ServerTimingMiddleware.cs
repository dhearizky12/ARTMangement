using System.Diagnostics;

namespace BantuBantu.Api.Diagnostics;

/// <summary>
/// Adds a <c>Server-Timing</c> header with the request's database time,
/// statement count and total wall time. Off unless explicitly configured.
/// The header is written directly once the pipeline returns if the response has
/// not started yet, and via <see cref="HttpResponse.OnStarting"/> otherwise, so
/// it is present whether or not the response began streaming mid-pipeline.
/// </summary>
public sealed class ServerTimingMiddleware(RequestDelegate next, DiagnosticsSettings settings)
{
    public async Task InvokeAsync(HttpContext context, RequestDbMetrics metrics)
    {
        if (!settings.ServerTiming)
        {
            await next(context);
            return;
        }
        var start = Stopwatch.GetTimestamp();
        void Write()
        {
            var totalMs = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            context.Response.Headers["Server-Timing"] =
                $"db;dur={metrics.DbMilliseconds}, sql;count={metrics.SqlCount}, total;dur={totalMs}";
        }
        context.Response.OnStarting(() => { Write(); return Task.CompletedTask; });
        await next(context);
        if (!context.Response.HasStarted) Write();
    }
}
