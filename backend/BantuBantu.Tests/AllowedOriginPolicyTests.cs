using BantuBantu.Api;
using Microsoft.Extensions.Configuration;

namespace BantuBantu.Tests;

public sealed class AllowedOriginPolicyTests
{
    [Fact]
    public void AllowsConfiguredProductionAndPagesPreviewOrigins()
    {
        var config = new ConfigurationManager();
        config["Frontend:Origin"] = "https://app.example.com";
        config["Frontend:OriginPatterns"] = "https://*.pages.dev";
        var policy = AllowedOriginPolicy.FromConfiguration(config);

        Assert.True(policy.IsAllowed("https://app.example.com"));
        Assert.True(policy.IsAllowed("https://preview-123.pages.dev"));
        Assert.False(policy.IsAllowed("https://pages.dev"));
        Assert.False(policy.IsAllowed("http://preview-123.pages.dev"));
        Assert.False(policy.IsAllowed("https://evil.example.net"));
    }

    [Fact]
    public void RejectsTrailingSlashAndUnsupportedPatterns()
    {
        var config = new ConfigurationManager();
        config["Frontend:Origin"] = "https://app.example.com/";
        Assert.Throws<InvalidOperationException>(() => AllowedOriginPolicy.FromConfiguration(config));

        config["Frontend:Origin"] = "https://app.example.com";
        config["Frontend:OriginPatterns"] = "*.pages.dev";
        Assert.Throws<InvalidOperationException>(() => AllowedOriginPolicy.FromConfiguration(config));
    }
}
