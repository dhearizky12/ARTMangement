using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using BantuBantu.Application;
using Microsoft.Extensions.Configuration;

namespace BantuBantu.Infrastructure;

/// <summary>
/// Private object storage implementation for Cloudflare R2 and other
/// S3-compatible providers. Keys are opaque values stored in the database;
/// objects are never exposed as public URLs by this class.
/// </summary>
public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly IAmazonS3 client;
    private readonly string bucket;
    private readonly string prefix;

    public S3FileStorage(IConfiguration configuration)
    {
        var serviceUrl = Required(configuration, "Storage:S3:ServiceUrl");
        var accessKey = Required(configuration, "Storage:S3:AccessKey");
        var secretKey = Required(configuration, "Storage:S3:SecretKey");
        bucket = Required(configuration, "Storage:S3:Bucket");
        prefix = (configuration["Storage:S3:KeyPrefix"] ?? "provider-documents").Trim('/');
        var region = configuration["Storage:S3:Region"] ?? "auto";

        client = new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), new AmazonS3Config
        {
            ServiceURL = serviceUrl,
            AuthenticationRegion = region,
            ForcePathStyle = true
        });
    }

    public async Task<string> StoreAsync(Stream content, CancellationToken ct)
    {
        var key = $"{prefix}/{Guid.NewGuid():N}.jpg";
        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = "image/jpeg",
            AutoCloseStream = false,
            AutoResetStreamPosition = false
        };
        try
        {
            await client.PutObjectAsync(request, ct);
            return key;
        }
        catch
        {
            // The caller owns the input stream and is responsible for cleanup.
            throw;
        }
    }

    public async Task<Stream> OpenAsync(string storageKey, CancellationToken ct)
    {
        ValidateKey(storageKey);
        try
        {
            var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = storageKey }, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new ProfileException("Dokumen tidak ditemukan.", 404);
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        ValidateKey(storageKey);
        await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = bucket, Key = storageKey }, ct);
    }

    public void Dispose() => client.Dispose();

    private void ValidateKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || storageKey.Contains("..", StringComparison.Ordinal) ||
            storageKey.StartsWith("/", StringComparison.Ordinal) || !storageKey.EndsWith(".jpg", StringComparison.Ordinal) ||
            !Guid.TryParseExact(storageKey[..^4].Split('/').Last(), "N", out _))
            throw new ArgumentException("Invalid storage key.", nameof(storageKey));
    }

    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key])
            ? configuration[key]!.Trim()
            : throw new InvalidOperationException($"Missing configuration: {key}");
}
