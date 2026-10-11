using System.Net;
using System.Text.Json;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Infrastructure.Services;
using Diten.Platform.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.PlatformAdministrators;

/// <summary>
/// BL-521 FIX5 item 6 — A REFUSED CALL TO AUTH LEAVES NO PERSONAL DATA IN THE LOG OR IN THE EXCEPTION. The failure branch of the
/// production <see cref="PlatformAdministratorProvisioningService"/> wrote <c>Email={Email}</c> and Auth's WHOLE reply body at
/// Error level, and threw an exception carrying Auth's own message (the global handler logs that exception's text and puts it
/// in the problem+json it answers with): a validation or conflict reply can name the address, a username, a phone number.
/// Now the log says only WHO (the administrator's id), WHAT (the status code) and WHY (a reason code derived from the status);
/// the exception says the reason code; Auth's body is never read into either. (Only the HTTP handler underneath is fake.)
///
/// <para>FIX5 item 10 — the exception also says whether Auth GAVE NO ANSWER (a 5xx, a timeout): the update path then makes no
/// second call to the same Auth.</para>
/// </summary>
public sealed class PlatformAdministratorProvisioningLogTests
{
    private const string TestOnlyKey = "bl521-fix5-test-only-internal-key";
    private const string Address = "alice.secret@bl521.test";

    /// <summary>What a refusing Auth might say: the address, a username, a phone number, a validation sentence.</summary>
    private const string RevealingBody =
        """{"message":"alice.secret@bl521.test (user alice.secret, phone +41 79 555 01 23) already exists","detail":"Validation failed: Email 'alice.secret@bl521.test' is not allowed","title":"Conflict"}""";

    public static TheoryData<HttpStatusCode, string, bool> Refusals() => new()
    {
        { HttpStatusCode.BadRequest, PlatformAdministratorAuthCallException.RequestRejected, false },
        { HttpStatusCode.Unauthorized, PlatformAdministratorAuthCallException.InternalKeyRefused, false },
        { HttpStatusCode.NotFound, PlatformAdministratorAuthCallException.AccountNotFound, false },
        { HttpStatusCode.Conflict, PlatformAdministratorAuthCallException.Conflict, false },
        { HttpStatusCode.InternalServerError, PlatformAdministratorAuthCallException.AuthUnavailable, true },
        { HttpStatusCode.ServiceUnavailable, PlatformAdministratorAuthCallException.AuthUnavailable, true },
        { HttpStatusCode.RequestTimeout, PlatformAdministratorAuthCallException.AuthUnavailable, true }
    };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_refused_sync_logs_identity_status_and_reason_only_and_throws_a_body_free_exception(HttpStatusCode status, string reason, bool noAnswer)
    {
        var log = new ListLogger();
        var id = Guid.NewGuid();
        var service = Service(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(RevealingBody) }), log);

        var thrown = await Assert.ThrowsAsync<PlatformAdministratorAuthCallException>(() => service.SyncAsync(
            new PlatformAdministratorProvisioningSyncRequest(Address, "alice.secret", "Alice Secret", "PlatformAdmin", ["SupportAdmin"], "Active")
            { AdministratorId = id }, CancellationToken.None));

        AssertNoPersonalData(log, thrown);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains(id.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains(((int)status).ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains(reason, entry.Message, StringComparison.Ordinal);
        Assert.Equal((int)status, thrown.StatusCode);
        Assert.Equal(reason, thrown.ReasonCode);
        Assert.Equal(noAnswer, thrown.Unavailable);
        Assert.Contains(reason, thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task A_refused_provision_logs_identity_status_and_reason_only_and_throws_a_body_free_exception(HttpStatusCode status, string reason, bool noAnswer)
    {
        var log = new ListLogger();
        var id = Guid.NewGuid();
        var service = Service(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(RevealingBody) }), log);

        var thrown = await Assert.ThrowsAsync<PlatformAdministratorAuthCallException>(() => service.ProvisionAsync(
            new PlatformAdministratorProvisioningRequest(Address, "alice.secret", "Alice Secret", "PlatformAdmin", ["SupportAdmin"], true, "Active")
            { AdministratorId = id }, CancellationToken.None));

        AssertNoPersonalData(log, thrown);
        var entry = Assert.Single(log.Entries);
        Assert.Contains(id.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Contains(reason, entry.Message, StringComparison.Ordinal);
        Assert.Equal(noAnswer, thrown.Unavailable);
    }

    /// <summary>Only a 5xx / timeout reply, a refused connection or the client's own timeout is "no answer"; a refusal, a
    /// cancellation the caller asked for and any other failure are not.</summary>
    [Fact]
    public void Only_a_missing_answer_is_no_answer()
    {
        using var none = new CancellationTokenSource();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.True(PlatformAdministratorAuthCallException.IsNoAnswer(new PlatformAdministratorAuthCallException(HttpStatusCode.BadGateway), none.Token));
        Assert.True(PlatformAdministratorAuthCallException.IsNoAnswer(new HttpRequestException("connection refused"), none.Token));
        Assert.True(PlatformAdministratorAuthCallException.IsNoAnswer(new TaskCanceledException("the client's timeout", new TimeoutException()), none.Token));
        Assert.False(PlatformAdministratorAuthCallException.IsNoAnswer(new PlatformAdministratorAuthCallException(HttpStatusCode.Conflict), none.Token));
        Assert.False(PlatformAdministratorAuthCallException.IsNoAnswer(new OperationCanceledException(cancelled.Token), cancelled.Token));
        Assert.False(PlatformAdministratorAuthCallException.IsNoAnswer(new InvalidOperationException("anything else"), none.Token));
    }

    /// <summary>The administrator's id is for the log only: the body Auth reads carries the record's status (provision, FIX5
    /// item 11) and never an id field.</summary>
    [Fact]
    public async Task The_provision_body_carries_the_status_and_no_administrator_id()
    {
        string? body = null;
        var service = Service(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"emailSent":true}""") };
        }, new ListLogger());

        await service.ProvisionAsync(new PlatformAdministratorProvisioningRequest(Address, "alice", "Alice", "PlatformAdmin", ["ReadOnly"], true, "Suspended")
        { AdministratorId = Guid.NewGuid() }, CancellationToken.None);

        using var json = JsonDocument.Parse(body!);
        Assert.Equal("Suspended", json.RootElement.GetProperty("status").GetString());
        Assert.False(json.RootElement.TryGetProperty("administratorId", out _));
    }

    [Fact]
    public async Task The_sync_body_carries_no_administrator_id()
    {
        string? body = null;
        var service = Service(async request =>
        {
            body = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }, new ListLogger());

        await service.SyncAsync(new PlatformAdministratorProvisioningSyncRequest(Address, "alice", "Alice", "PlatformAdmin", ["ReadOnly"], "Active")
        { AdministratorId = Guid.NewGuid() }, CancellationToken.None);

        using var json = JsonDocument.Parse(body!);
        Assert.False(json.RootElement.TryGetProperty("administratorId", out _));
    }

    private static void AssertNoPersonalData(ListLogger log, Exception thrown)
    {
        var everything = string.Join("\n", log.Entries.Select(e => e.Message)) + "\n" + thrown.Message + "\n" + thrown;
        foreach (var forbidden in new[] { Address, "alice.secret", "555 01 23", "Validation failed", "already exists", "is not allowed" })
        {
            Assert.DoesNotContain(forbidden, everything, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static PlatformAdministratorProvisioningService Service(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply, ILogger<PlatformAdministratorProvisioningService> logger) =>
        new(new StubFactory(new StubHandler(reply)),
            Options.Create(new AuthServiceOptions { BaseUrl = "http://auth.bl521.test", InternalApiKey = TestOnlyKey }),
            logger);

    private sealed class ListLogger : ILogger<PlatformAdministratorProvisioningService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception) + (exception is null ? string.Empty : " | " + exception)));
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => reply(request);
    }
}
