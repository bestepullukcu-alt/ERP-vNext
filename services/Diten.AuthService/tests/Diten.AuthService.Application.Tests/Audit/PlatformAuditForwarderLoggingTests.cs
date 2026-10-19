using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Application.Tests.Audit;

/// <summary>
/// WP-ROLES-CLOSE-01 (CT 4d) — the production <see cref="PlatformAuditForwarder"/> never throws, and never fails in
/// silence either: an event that did not reach Platform leaves a log line that names it — also when the reason is the
/// caller going away (cancellation), which used to be swallowed without a word.
/// </summary>
public sealed class PlatformAuditForwarderLoggingTests
{
    private static readonly PlatformAuditEvent Event = new(
        "role_deleted", Guid.NewGuid(), "Role", Guid.NewGuid(), 3, 1, new Dictionary<string, object?> { ["roleName"] = "Auditors" });

    [Fact]
    public async Task A_cancelled_forward_does_not_throw_and_is_logged_with_the_event_name()
    {
        var logs = new CapturingLoggerProvider();
        using var cts = new CancellationTokenSource();
        var forwarder = Forwarder(new Handler(_ => { cts.Cancel(); throw new OperationCanceledException(cts.Token); }), logs);

        await forwarder.ForwardAsync(Event, cts.Token);

        var entry = Assert.Single(logs.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Contains("cancelled", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("role_deleted", entry.Message);
        Assert.Contains(Event.TenantId.ToString(), entry.Message);
    }

    [Fact]
    public async Task An_unreachable_Platform_does_not_throw_and_is_logged_with_the_event_name()
    {
        var logs = new CapturingLoggerProvider();
        var forwarder = Forwarder(new Handler(_ => throw new HttpRequestException("Connection refused")), logs);

        await forwarder.ForwardAsync(Event);

        var entry = Assert.Single(logs.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Contains("role_deleted", entry.Message);
    }

    [Fact]
    public async Task A_refusing_Platform_does_not_throw_and_is_logged_with_its_status()
    {
        var logs = new CapturingLoggerProvider();
        var forwarder = Forwarder(new Handler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden)), logs);

        await forwarder.ForwardAsync(Event);

        var entry = Assert.Single(logs.Entries, e => e.Level >= LogLevel.Warning);
        Assert.Contains("403", entry.Message);
        Assert.Contains("role_deleted", entry.Message);
    }

    private static PlatformAuditForwarder Forwarder(HttpMessageHandler handler, CapturingLoggerProvider logs)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("http://platform.test") },
            new HttpContextAccessor(),
            Options.Create(new PlatformServiceOptions { InternalApiKey = "test-key" }),
            logs.CreateLogger<PlatformAuditForwarder>());

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(answer(request));
    }
}
