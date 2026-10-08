using System.Net;
using System.Text.Json;
using BantuBantu.Api.Controllers;
using BantuBantu.Application;
using BantuBantu.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
namespace BantuBantu.Tests;

public sealed class EmailSendingTests
{
    [Fact]
    public async Task ResendSenderUsesSendingKeyAndConfigurableFromAddress()
    {
        var handler = new StubHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var config = new ConfigurationManager
        {
            ["Resend:ApiKey"] = "unit-test-sending-key",
            ["Resend:From"] = "Bantu-Bantu <no-reply@example.test>"
        };
        var sender = new ResendEmailSender(http, config, NullLogger<ResendEmailSender>.Instance);

        await sender.SendAsync(new EmailMessage("user@example.test", "Verifikasi email", "<p>123456</p>"));

        Assert.Equal("https://api.resend.com/emails", handler.RequestUri?.ToString());
        Assert.Equal("Bearer unit-test-sending-key", handler.Authorization);
        using var payload = JsonDocument.Parse(handler.Body);
        var root = payload.RootElement;
        Assert.Equal("Bantu-Bantu <no-reply@example.test>", root.GetProperty("from").GetString());
        Assert.Equal("user@example.test", root.GetProperty("to")[0].GetString());
        Assert.Equal("Verifikasi email", root.GetProperty("subject").GetString());
        Assert.Equal("<p>123456</p>", root.GetProperty("html").GetString());
        // The key must only ever travel in the Authorization header, never in the body.
        Assert.DoesNotContain("unit-test-sending-key", handler.Body);
    }

    [Fact]
    public async Task ResendSenderReportsMissingKeyWithProvisioningHint()
    {
        var handler = new StubHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var config = new ConfigurationManager { ["Resend:From"] = "Bantu-Bantu <no-reply@example.test>" };
        var sender = new ResendEmailSender(http, config, NullLogger<ResendEmailSender>.Instance);

        var error = await Assert.ThrowsAsync<ProfileException>(() => sender.SendAsync(new EmailMessage("user@example.test", "s", "<p>s</p>")));
        Assert.Equal("EMAIL_CONFIG_MISSING", error.Code);
        Assert.Contains("provision-resend-key.sh", error.Message);
        Assert.Null(handler.RequestUri);
    }

    [Fact]
    public async Task ResendSenderSurfacesRejectedSend()
    {
        var handler = new StubHandler { Status = HttpStatusCode.UnprocessableEntity, Payload = "{\"message\":\"domain not verified\"}" };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var config = new ConfigurationManager { ["Resend:ApiKey"] = "k", ["Resend:From"] = "a@example.test" };
        var sender = new ResendEmailSender(http, config, NullLogger<ResendEmailSender>.Instance);

        var error = await Assert.ThrowsAsync<ProfileException>(() => sender.SendAsync(new EmailMessage("user@example.test", "s", "<p>s</p>")));
        Assert.Equal("EMAIL_SEND_FAILED", error.Code);
        Assert.Equal(502, error.Status);
    }

    [Fact]
    public async Task DevelopmentSenderLogsInsteadOfSending()
    {
        var logger = new CapturingLogger();
        var sender = new LoggingEmailSender(logger);

        await sender.SendAsync(new EmailMessage("user@example.test", "Verifikasi email", "<p>hi</p>"));

        Assert.Contains(logger.Messages, m => m.Contains("user@example.test") && m.Contains("NOT delivered"));
    }

    [Fact]
    public async Task AdminTestEndpointDelegatesToTheSender()
    {
        var recorder = new RecordingSender();
        var controller = new EmailAdminController(recorder);

        var result = await controller.Test(new EmailAdminController.EmailTestRequest("qa@example.test", "Tes kirim"), default);

        var accepted = Assert.IsType<Microsoft.AspNetCore.Mvc.AcceptedResult>(result);
        Assert.Equal("qa@example.test", recorder.Last?.To);
        Assert.Equal("Tes kirim", recorder.Last?.Subject);
        Assert.Contains("<p>", recorder.Last?.Html ?? "");
        Assert.NotNull(accepted.Value);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public Uri? RequestUri; public string? Authorization; public string Body = "";
        public HttpStatusCode Status = HttpStatusCode.Accepted;
        public string Payload = "{\"id\":\"message-id\"}";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(Status) { Content = new StringContent(Payload) };
        }
    }

    private sealed class RecordingSender : IEmailSender
    {
        public EmailMessage? Last;
        public Task SendAsync(EmailMessage message, CancellationToken ct = default) { Last = message; return Task.CompletedTask; }
    }

    private sealed class CapturingLogger : ILogger<LoggingEmailSender>
    {
        public readonly List<string> Messages = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
