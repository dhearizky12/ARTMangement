using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace BantuBantu.Api;

/// <summary>
/// Small dependency-free JSON-lines logger for hosts where the platform log
/// retention is outside the application's control. The files are intentionally
/// plain text so they can be retrieved through the existing FTP/SFTP access.
/// </summary>
internal sealed class JsonFileLoggerProvider : ILoggerProvider
{
    private readonly object gate = new();
    private readonly string pathPrefix;
    private readonly LogLevel minimumLevel;
    private readonly int retainedFiles;
    private readonly long maxFileBytes;
    private StreamWriter? writer;
    private DateOnly? writerDate;
    private int writerIndex;
    private bool disposed;

    public JsonFileLoggerProvider(IConfiguration configuration)
    {
        pathPrefix = Path.GetFullPath(configuration["Logging:File:Path"] ?? "App_Data/logs/api");
        minimumLevel = ParseLevel(configuration["Logging:File:MinimumLevel"]);
        retainedFiles = ParseInt(configuration["Logging:File:RetainedFiles"], 14, 1, 90);
        maxFileBytes = ParseLong(configuration["Logging:File:MaxFileBytes"], 10 * 1024 * 1024, 1024 * 1024, 250 * 1024 * 1024);
        Directory.CreateDirectory(Path.GetDirectoryName(pathPrefix) ?? AppContext.BaseDirectory);
    }

    public ILogger CreateLogger(string categoryName) => new JsonFileLogger(this, categoryName);

    internal bool IsEnabled(LogLevel level) => level >= minimumLevel && level != LogLevel.None;

    internal void Write(string categoryName, LogLevel level, EventId eventId, string message, Exception? exception)
    {
        if (!IsEnabled(level)) return;

        var now = DateTimeOffset.UtcNow;
        var traceId = Activity.Current?.TraceId.ToString();
        var entry = new
        {
            timestamp = now,
            level = level.ToString(),
            category = categoryName,
            eventId = eventId.Id,
            eventName = eventId.Name,
            message,
            traceId,
            activityId = Activity.Current?.Id,
            exception = exception?.ToString()
        };

        try
        {
            lock (gate)
            {
                if (disposed) return;
                EnsureWriter(now);
                writer!.WriteLine(JsonSerializer.Serialize(entry));
                writer.Flush();
            }
        }
        catch (Exception writeError)
        {
            // Logging must never take the API down. Keep a fallback visible in
            // the host process log when the FTP-retrievable directory is not
            // writable (for example, during a misconfigured deployment).
            Console.Error.WriteLine($"File logging failed: {writeError.Message}");
        }
    }

    private void EnsureWriter(DateTimeOffset now)
    {
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        if (writer is not null && writerDate == date && writer.BaseStream.Length < maxFileBytes) return;

        writer?.Dispose();
        writer = null;

        if (writerDate != date) writerIndex = 0;
        writerDate = date;
        while (true)
        {
            var path = $"{pathPrefix}-{date:yyyyMMdd}-{writerIndex:00}.jsonl";
            if (!File.Exists(path) || new FileInfo(path).Length < maxFileBytes) break;
            writerIndex++;
        }

        var currentPath = $"{pathPrefix}-{date:yyyyMMdd}-{writerIndex:00}.jsonl";
        writer = new StreamWriter(new FileStream(currentPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new System.Text.UTF8Encoding(false))
        {
            AutoFlush = false
        };
        PruneOldFiles();
    }

    private void PruneOldFiles()
    {
        var directory = Path.GetDirectoryName(pathPrefix) ?? AppContext.BaseDirectory;
        var prefix = Path.GetFileName(pathPrefix) + "-";
        var files = Directory.EnumerateFiles(directory, prefix + "*.jsonl")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Skip(retainedFiles)
            .ToArray();
        foreach (var file in files)
        {
            try { File.Delete(file); } catch { /* best effort */ }
        }
    }

    private static LogLevel ParseLevel(string? value) => Enum.TryParse<LogLevel>(value, true, out var level) ? level : LogLevel.Information;

    private static int ParseInt(string? value, int fallback, int min, int max) => int.TryParse(value, out var parsed) ? Math.Clamp(parsed, min, max) : fallback;

    private static long ParseLong(string? value, long fallback, long min, long max) => long.TryParse(value, out var parsed) ? Math.Clamp(parsed, min, max) : fallback;

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            writer?.Dispose();
            writer = null;
        }
    }

    private sealed class JsonFileLogger(JsonFileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => provider.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            provider.Write(categoryName, logLevel, eventId, formatter(state, exception), exception);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
