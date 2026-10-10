using System.Diagnostics;
using System.Data.Common;
using BantuBantu.Api.Diagnostics;
using BantuBantu.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BantuBantu.Api.Controllers;

/// <summary>
/// Read-only database latency probe for operators. Gated to PlatformAdmin and
/// a verified two-step session, and reachable only when the caller can already
/// act as admin (there is no anonymous path). Returns timings, never schema or
/// row data.
/// </summary>
[ApiController, Route("api/admin/diagnostics"), Authorize(Roles = "PlatformAdmin"), RequireMfa]
public class AdminDiagnosticsController(AppDbContext db, DiagnosticsSettings settings) : ControllerBase
{
    [HttpGet("db-latency")]
    public async Task<IActionResult> DbLatency(CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var opened = false;
        long connectMs = -1;
        long? roundTripMs = null;
        string? serverVersion = null;
        string? error = null;
        try
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                var open = Stopwatch.StartNew();
                await connection.OpenAsync(ct);
                open.Stop();
                connectMs = (long)open.Elapsed.TotalMilliseconds;
                opened = true;
            }
            else
            {
                connectMs = 0;
            }
            serverVersion = connection.ServerVersion;
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var ping = Stopwatch.StartNew();
            await command.ExecuteScalarAsync(ct);
            ping.Stop();
            roundTripMs = (long)ping.Elapsed.TotalMilliseconds;
        }
        catch (Exception exception)
        {
            error = exception.GetType().Name;
        }
        finally
        {
            if (opened) await connection.CloseAsync();
        }
        return Ok(new
        {
            connected = error is null,
            connectMs,
            roundTripMs,
            provider = db.Database.ProviderName,
            serverVersion,
            serverTimingEnabled = settings.ServerTiming,
            serverTimeUtc = DateTimeOffset.UtcNow,
            error
        });
    }
}
