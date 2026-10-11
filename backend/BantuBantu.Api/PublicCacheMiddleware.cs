using System.Security.Cryptography;
using Microsoft.Net.Http.Headers;
namespace BantuBantu.Api;

// Adds shared-cache headers and a weak ETag to anonymous, cacheable catalog
// GETs so repeat visits and intermediaries avoid re-downloading them. Only the
// allowlisted public read paths are buffered for hashing.
public sealed class PublicCacheMiddleware(RequestDelegate next)
{
    private const string Providers = "/api/providers";
    private static readonly HashSet<string> Exact = [Providers, "/api/service-categories", "/api/content", "/api/trust-sections"];

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) || !IsCacheable(context.Request.Path) || context.Request.Headers.ContainsKey(HeaderNames.Authorization))
        {
            await next(context);
            return;
        }
        var original = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await next(context);
            var bytes = buffer.ToArray();
            if (context.Response.StatusCode == StatusCodes.Status200OK && bytes.Length > 0)
            {
                context.Response.Headers.CacheControl = "public, max-age=60, stale-while-revalidate=300";
                context.Response.Headers.Vary = "Origin";
                var tag = "\"" + Convert.ToHexString(SHA256.HashData(bytes))[..32] + "\"";
                var weak = "W/" + tag;
                context.Response.Headers.ETag = weak;
                if (context.Request.Headers.IfNoneMatch.Any(t => t == weak || t == tag))
                {
                    context.Response.StatusCode = StatusCodes.Status304NotModified;
                    context.Response.ContentLength = null;
                    return;
                }
                await original.WriteAsync(bytes, context.RequestAborted);
                return;
            }
            await original.WriteAsync(bytes, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = original;
        }
    }

    private static bool IsCacheable(PathString path)
    {
        var value = path.Value;
        if (value is null) return false;
        if (Exact.Contains(value)) return true;
        return value.StartsWith(Providers + "/", StringComparison.Ordinal) && value.Length == Providers.Length + 1 + 36;
    }
}
