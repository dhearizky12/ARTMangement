using System.Security.Cryptography;
using BantuBantu.Application;
using BantuBantu.Infrastructure;
using BantuBantu.Infrastructure.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
namespace BantuBantu.Tests;

public sealed class EmailOtpServiceTests
{
    [Fact]
    public void OtpIsSixDigits()
    {
        var otp = NewOtp(new ConfigurationManager(), new RecordingSender());
        Assert.Matches("^[0-9]{6}$", otp.Generate());
    }

    [Theory]
    [InlineData("provider@example.test", "p***@example.test")]
    [InlineData("ab@gmail.com", "a***@gmail.com")]
    [InlineData("x@sub.example.co.id", "x***@sub.example.co.id")]
    [InlineData("nousername", "***")]
    public void MaskHidesMiddleOfLocalPart(string email, string expected) => Assert.Equal(expected, NewOtp(new ConfigurationManager(), new RecordingSender()).Mask(email));

    [Fact]
    public void HashVerifyRoundTripsAndRejectsWrongOrDifferentLength()
    {
        var otp = NewOtp(new ConfigurationManager(), new RecordingSender());
        var code = otp.Generate();
        var hash = otp.Hash(code);
        Assert.Equal(64, hash.Length);
        Assert.True(otp.Verify(code, hash));
        Assert.False(otp.Verify("000000", hash));
        Assert.False(otp.Verify(code, "abcd"));
        Assert.False(otp.Verify(code, hash[..^1] + "0"));
    }

    [Fact]
    public async Task SendDelegatesToSenderOutsideDevelopment()
    {
        var sender = new RecordingSender();
        var config = new ConfigurationManager
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["Resend:ApiKey"] = "sending-key"
        };
        var otp = NewOtp(config, sender);

        await otp.SendAsync("provider@example.test", "481516", 10, default);

        var message = sender.Last!;
        Assert.Equal("provider@example.test", message.To);
        Assert.Equal("Kode masuk Bantu-Bantu", message.Subject);
        Assert.Contains("481516", message.Html);
        Assert.Contains("Berlaku 10 menit", message.Html);
    }

    [Fact]
    public async Task SendWithMissingSendingKeyInProductionStillAttemptsDelivery()
    {
        var sender = new RecordingSender();
        var otp = NewOtp(new ConfigurationManager { ["ASPNETCORE_ENVIRONMENT"] = "Production" }, sender);
        await otp.SendAsync("a@example.test", "111111", 10, default);
        Assert.Equal("a@example.test", sender.Last?.To);
    }

    [Fact]
    public async Task ClearCodeLogCanOnlyRunInDevelopment()
    {
        // Development without a key: code is logged in clear, nothing is sent.
        var devLogger = new CapturingOtpLogger();
        var devSender = new RecordingSender();
        var devOtp = NewOtp(new ConfigurationManager { ["ASPNETCORE_ENVIRONMENT"] = "Development" }, devSender, devLogger);
        await devOtp.SendAsync("a@example.test", "481516", 10, default);
        Assert.Null(devSender.Last);
        Assert.Contains("481516", string.Concat(devLogger.Messages));
    }

    [Fact]
    public async Task ProductionWithoutKeyFailsSendAndNeverLogsCode()
    {
        // Outside Development the clear-code branch is unreachable: the real
        // sender throws EMAIL_CONFIG_MISSING instead of logging the code.
        var logger = new CapturingOtpLogger();
        var sender = new ResendEmailSender(new HttpClient(), new ConfigurationManager { ["ASPNETCORE_ENVIRONMENT"] = "Production" }, NullLogger<ResendEmailSender>.Instance);
        var otp = NewOtp(new ConfigurationManager { ["ASPNETCORE_ENVIRONMENT"] = "Production" }, sender, logger);
        var error = await Assert.ThrowsAsync<ProfileException>(() => otp.SendAsync("a@example.test", "481516", 10, default));
        Assert.Equal("EMAIL_CONFIG_MISSING", error.Code);
        Assert.DoesNotContain("481516", string.Concat(logger.Messages));
    }

    private static EmailOtpService NewOtp(IConfiguration config, IEmailSender sender, ILogger<EmailOtpService>? logger = null)
    {
        using var rsa = RSA.Create(2048);
        var directory = Path.Combine(Path.GetTempPath(), "bantubantu-otp-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var publicPath = Path.Combine(directory, "public.pem");
        File.WriteAllText(publicPath, rsa.ExportSubjectPublicKeyInfoPem());
        File.WriteAllText(Path.Combine(directory, "private.pem"), rsa.ExportRSAPrivateKeyPem());
        var keys = new RsaKeys(new ConfigurationManager
        {
            ["Jwt:PrivateKeyPath"] = Path.Combine(directory, "private.pem"),
            ["Jwt:PublicKeyPath"] = publicPath,
            ["Jwt:KeyId"] = "test"
        });
        return new EmailOtpService(keys, sender, new NotificationRenderer(new AppSettings { FrontendBaseUrl = "http://localhost:5173" }, new EmailSettings()), config, logger ?? NullLogger<EmailOtpService>.Instance);
    }

    private sealed class CapturingOtpLogger : ILogger<EmailOtpService>
    {
        public readonly List<string> Messages = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private sealed class RecordingSender : IEmailSender
    {
        public EmailMessage? Last;
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) { Last = message; return Task.CompletedTask; }
    }
}