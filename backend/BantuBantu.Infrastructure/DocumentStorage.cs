using BantuBantu.Application;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
namespace BantuBantu.Infrastructure;

public class DocumentProcessor : IDocumentProcessor
{
    public const long MaxBytes = 5 * 1024 * 1024;
    public UploadRules Rules => new(MaxBytes, ["image/jpeg", "image/png"], ["KTP", "KK"]);
    public async Task<Stream> ValidateAndNormalizeAsync(Stream file, long length, string contentType, CancellationToken ct)
    {
        if (length <= 0 || length > MaxBytes) throw new ProfileException("Ukuran dokumen harus antara 1 byte dan 5 MB.");
        if (!Rules.ContentTypes.Contains(contentType)) throw new ProfileException("Unggah foto JPG atau PNG.");
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int count;
        while ((count = await file.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > MaxBytes) throw new ProfileException("Ukuran dokumen maksimal 5 MB.");
            await buffer.WriteAsync(chunk.AsMemory(0, count), ct);
        }
        buffer.Position = 0;
        try
        {
            var format = await Image.DetectFormatAsync(buffer, ct);
            if (format.DefaultMimeType != contentType || !Rules.ContentTypes.Contains(format.DefaultMimeType)) throw new ProfileException("Isi file tidak sesuai dengan jenis foto yang dipilih.");
            buffer.Position = 0;
            var options = new DecoderOptions { SkipMetadata = true, MaxFrames = 1 };
            var info = await Image.IdentifyAsync(options, buffer, ct);
            if (info.Width < 100 || info.Height < 100 || (long)info.Width * info.Height > 20_000_000) throw new ProfileException("Resolusi foto minimal 100 × 100 dan maksimal 20 megapiksel.");
            buffer.Position = 0;
            using var decoded = await Image.LoadAsync(options, buffer, ct);
            // Re-encode pixel data: discard metadata, client filenames, and appended content.
            var normalized = new MemoryStream();
            try
            {
                await decoded.SaveAsJpegAsync(normalized, new JpegEncoder { Quality = 90, SkipMetadata = true }, ct);
                normalized.Position = 0;
                return normalized;
            }
            catch { normalized.Dispose(); throw; }
        }
        catch (Exception error) when (error is UnknownImageFormatException or InvalidImageContentException or NotSupportedException)
        { throw new ProfileException("Foto tidak dapat dibaca. Unggah ulang file JPG atau PNG yang utuh."); }
    }
}
public class LocalFileStorage : IFileStorage
{
    private readonly string root;
    public LocalFileStorage(IConfiguration config)
    {
        var configured = config["Storage:RootPath"] ?? throw new InvalidOperationException("Missing configuration: Storage:RootPath");
        root = Path.IsPathFullyQualified(configured) ? configured : Path.Combine(AppContext.BaseDirectory, configured);
        Directory.CreateDirectory(root);
    }
    public async Task<string> StoreAsync(Stream content, CancellationToken ct)
    {
        var key = Guid.NewGuid().ToString("N") + ".jpg";
        var path = Path.Combine(root, key);
        try
        {
            await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            await content.CopyToAsync(target, ct);
            return key;
        }
        catch { File.Delete(path); throw; }
    }
    public Task<Stream> OpenAsync(string storageKey, CancellationToken ct)
    {
        if (!storageKey.EndsWith(".jpg", StringComparison.Ordinal) || !Guid.TryParseExact(storageKey[..^4], "N", out _)) throw new ArgumentException("Invalid storage key.");
        var path = Path.Combine(root, storageKey);
        if (!File.Exists(path)) throw new ProfileException("Dokumen tidak ditemukan.", 404);
        return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }
    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        if (!storageKey.EndsWith(".jpg", StringComparison.Ordinal) || !Guid.TryParseExact(storageKey[..^4], "N", out _)) throw new ArgumentException("Invalid storage key.");
        File.Delete(Path.Combine(root, storageKey));
        return Task.CompletedTask;
    }
}
