using System.Net;
using System.Text;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// BL-521 FIX2 item 2/3 — THE WIRE CONTRACT OF THE ADMINISTRATOR STATUS CALL, measured on the PRODUCTION
/// <see cref="PlatformAdministratorStatusClient"/> (only the HTTP handler underneath is fake). Before this file the refresh
/// tests replaced the client itself, so every mapping below could be broken without a red test: an inactive record read
/// as active, PartnerAdmin minted as platform_admin, an unknown type defaulting to platform_admin.
///
/// <para>The bodies are the Platform status endpoint's own JSON (camelCase envelope, <c>actorType</c> = the record's enum
/// name) — the Platform side pins the same shape in <c>InternalPlatformAdministratorStatusEndpointTests</c>.</para>
///
/// <para>Item 3 — Platform's definitive no (its 404 envelope, an inactive record, an unknown type) is Refused; NO answer
/// (network error, timeout, 5xx, a bare 404, an unreadable or empty body, no internal key) is Unavailable. FIX3: a record
/// with no type at all (an older Platform) and the framework's problem-details 404 are Unavailable too; a refused internal
/// key is logged as an error.</para>
/// </summary>
public sealed class PlatformAdministratorStatusClientWireTests
{
    private const string Email = "Wire.Probe@BL521.test";
    private const string TestOnlyInternalKey = "bl521-fix2-test-only-internal-key";

    // ── confirmed: each record actor type maps to its token actor type ─────────────────────────────────

    [Theory]
    [InlineData("PartnerAdmin", "partner_admin")]
    [InlineData("PlatformAdmin", "platform_admin")]
    public async Task An_active_record_is_confirmed_with_the_token_actor_type_its_record_type_names(string recordType, string tokenType)
    {
        var answer = await ClientFor(Ok(Status(isActive: true, recordType))).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Confirmed, answer.Verdict);
        Assert.Equal(tokenType, answer.ActorType);
    }

    // ── BL-521 FIX4 item 2: the record's roles travel with a confirmed answer ──────────────────────────

    [Fact]
    public async Task A_confirmed_answer_carries_the_records_roles()
    {
        var body = $$"""{"data":{"id":"{{Guid.NewGuid()}}","email":"wire.probe@bl521.test","status":"Active","isDeleted":false,"isActive":true,"actorType":"PlatformAdmin","roles":["SupportAdmin","BillingAdmin"]},"statusCode":200,"isSuccessful":true,"errors":[]}""";

        var answer = await ClientFor(Ok(body)).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Confirmed, answer.Verdict);
        Assert.Equal(["SupportAdmin", "BillingAdmin"], answer.Roles!);
    }

    /// <summary>An older Platform sends no roles: still a confirmed answer, and nothing to converge to.</summary>
    [Fact]
    public async Task An_answer_without_roles_is_still_confirmed_and_carries_none()
    {
        var answer = await ClientFor(Ok(Status(isActive: true, "PartnerAdmin"))).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Confirmed, answer.Verdict);
        Assert.Null(answer.Roles);
    }

    // ── refused: Platform's definitive no ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("PartnerAdmin")]
    [InlineData("PlatformAdmin")]
    public async Task An_inactive_record_is_refused_whatever_its_type(string recordType)
    {
        var answer = await ClientFor(Ok(Status(isActive: false, recordType, status: "Suspended"))).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Refused, answer.Verdict);
        Assert.Null(answer.ActorType);
    }

    [Theory]
    [InlineData("\"Auditor\"")]
    [InlineData("\"platform_admin\"")]
    [InlineData("\"\"")]
    public async Task An_active_record_of_an_unknown_type_is_refused_never_defaulted(string actorTypeJson)
    {
        var body = $$"""{"data":{"id":"{{Guid.NewGuid()}}","email":"wire.probe@bl521.test","status":"Active","isDeleted":false,"isActive":true,"actorType":{{actorTypeJson}}},"statusCode":200,"isSuccessful":true,"errors":[]}""";

        var answer = await ClientFor(Ok(body)).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Refused, answer.Verdict);
        Assert.Null(answer.ActorType);
    }

    /// <summary>
    /// BL-521 FIX3 item 5 — an answer with NO type at all comes from an older Platform that never sent the field (a version
    /// gap while a deployment rolls, not a statement about this administrator): no answer — 503, nothing revoked. Before,
    /// it was Refused, and in a mixed fleet every platform session that refreshed through an old copy was revoked.
    /// </summary>
    [Theory]
    [InlineData(""","actorType":null""")]
    [InlineData("")]
    public async Task An_active_record_without_any_type_is_an_older_platform_and_unavailable_not_a_no(string actorTypeMember)
    {
        var body = $$"""{"data":{"id":"{{Guid.NewGuid()}}","email":"wire.probe@bl521.test","status":"Active","isDeleted":false,"isActive":true{{actorTypeMember}}},"statusCode":200,"isSuccessful":true,"errors":[]}""";

        var answer = await ClientFor(Ok(body)).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Unavailable, answer.Verdict);
        Assert.Null(answer.ActorType);
    }

    [Fact]
    public async Task Platforms_own_not_found_envelope_is_refused()
    {
        var notFound = Reply(HttpStatusCode.NotFound,
            """{"data":null,"statusCode":404,"isSuccessful":false,"errors":["Platform administrator not found."],"reason_code":null,"correlation_id":null}""");

        var answer = await ClientFor(notFound).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Refused, answer.Verdict);
    }

    // ── BL-521 FIX5 item 2: the answer must be about the address that was asked ───────────────────────

    /// <summary>
    /// The answer carries the record's roles and actor type, so an answer about ANOTHER administrator (a mixed-up route, a cache,
    /// a wrong base address) would mint this account a token with someone else's authority. A record whose own address is not the
    /// one asked about — or that has none — is no answer: Unavailable (503 at the doors; nothing minted, nothing revoked). The
    /// password-reset doors, which ask the same question, fail closed on it too.
    /// </summary>
    [Theory]
    [InlineData("someone.else@bl521.test")]
    [InlineData("")]
    public async Task An_answer_about_another_address_is_unavailable_never_confirmed(string answeredAddress)
    {
        var body = $$"""{"data":{"id":"{{Guid.NewGuid()}}","email":"{{answeredAddress}}","status":"Active","isDeleted":false,"isActive":true,"actorType":"PlatformAdmin","roles":["SuperAdmin"]},"statusCode":200,"isSuccessful":true,"errors":[]}""";

        var answer = await ClientFor(Ok(body)).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Unavailable, answer.Verdict);
        Assert.Null(answer.ActorType);
        Assert.Null(answer.Roles);
        Assert.False(await ClientFor(Ok(body)).IsActiveAsync(Email, CancellationToken.None));
    }

    /// <summary>The comparison is on the normalized address: another letter case or surrounding spaces is the same address.</summary>
    [Fact]
    public async Task The_same_address_in_another_letter_case_or_with_spaces_is_the_same_address()
    {
        var body = $$"""{"data":{"id":"{{Guid.NewGuid()}}","email":"  WIRE.PROBE@bl521.TEST ","status":"Active","isDeleted":false,"isActive":true,"actorType":"PartnerAdmin","roles":["ReadOnly"]},"statusCode":200,"isSuccessful":true,"errors":[]}""";

        var answer = await ClientFor(Ok(body)).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Confirmed, answer.Verdict);
        Assert.Equal("partner_admin", answer.ActorType);
    }

    /// <summary>A mismatch is logged without either address (the log is for the operator, not a place for two people's addresses).</summary>
    [Fact]
    public async Task A_mismatched_answer_is_logged_without_either_address()
    {
        var logger = new ListLogger();
        var body = $$"""{"data":{"id":"{{Guid.NewGuid()}}","email":"someone.else@bl521.test","status":"Active","isDeleted":false,"isActive":true,"actorType":"PlatformAdmin"},"statusCode":200,"isSuccessful":true,"errors":[]}""";

        await Client(new StubHandler((_, _) => Task.FromResult(Ok(body)(null!))), logger: logger).GetActiveActorTypeAsync(Email, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain("someone.else", entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wire.probe", entry.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── unavailable: no answer is not a "no" ──────────────────────────────────────────────────────────

    public static TheoryData<string, HttpStatusCode, string> NoAnswerReplies() => new()
    {
        { "deployment 503", HttpStatusCode.ServiceUnavailable, "" },
        { "server error 500", HttpStatusCode.InternalServerError, """{"title":"Server Error","status":500}""" },
        { "bad gateway 502", HttpStatusCode.BadGateway, "" },
        { "internal key refused 401", HttpStatusCode.Unauthorized, """{"data":null,"statusCode":401,"isSuccessful":false,"errors":["Unauthorized."]}""" },
        { "a 404 that is not Platform's envelope", HttpStatusCode.NotFound, "" },
        { "an unreadable 200", HttpStatusCode.OK, "<html>not json</html>" },
        { "a 200 without a record", HttpStatusCode.OK, """{"data":null,"statusCode":200,"isSuccessful":true,"errors":[]}""" },
        { "an empty 200", HttpStatusCode.OK, "" },
    };

    [Theory]
    [MemberData(nameof(NoAnswerReplies))]
    public async Task A_reply_that_is_not_an_answer_about_the_record_is_unavailable(string reply, HttpStatusCode status, string body)
    {
        var answer = await ClientFor(Reply(status, body)).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.True(answer.Verdict == PlatformAdministratorRecordVerdict.Unavailable, $"{reply}: {answer.Verdict}");
        Assert.Null(answer.ActorType);
    }

    [Fact]
    public async Task A_network_error_is_unavailable_not_an_exception()
    {
        var answer = await ClientFor(_ => throw new HttpRequestException("connection refused"))
            .GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Unavailable, answer.Verdict);
    }

    [Fact]
    public async Task A_timeout_is_unavailable_not_an_exception()
    {
        var handler = new StubHandler(async (_, ct) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = Client(handler, timeout: TimeSpan.FromMilliseconds(100));

        var answer = await client.GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Unavailable, answer.Verdict);
    }

    [Fact]
    public async Task The_callers_own_cancellation_is_not_turned_into_an_answer()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ClientFor(Ok(Status(isActive: true, "PlatformAdmin"))).GetActiveActorTypeAsync(Email, cancelled.Token));
    }

    [Fact]
    public async Task Without_its_internal_key_the_client_has_no_answer_and_does_not_call()
    {
        var calls = 0;
        var handler = new StubHandler((_, _) => { calls++; return Task.FromResult(Ok(Status(true, "PlatformAdmin"))(null!)); });

        var answer = await Client(handler, internalKey: "").GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Unavailable, answer.Verdict);
        Assert.Equal(0, calls);
    }

    /// <summary>
    /// BL-521 FIX3 G1 — a route Platform does not have answers 404 with an RFC 7807 problem body (Platform's pipeline:
    /// AddProblemDetails + UseExceptionHandler + UseStatusCodePages). That is "no such route", not "no such administrator":
    /// Unavailable, nothing revoked. Measured on a real ASP.NET Core pipeline configured the same way, so the body is the one
    /// the framework writes, not a hand-written guess.
    /// </summary>
    [Fact]
    public async Task A_missing_route_answered_with_the_frameworks_problem_details_404_is_unavailable_not_a_no()
    {
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services => services.AddProblemDetails())
            .Configure(app =>
            {
                app.UseExceptionHandler();
                app.UseStatusCodePages();
            }));
        using var http = server.CreateClient();

        using (var raw = await http.GetAsync("/api/internal/platform-administrators/status?email=wire.probe%40bl521.test"))
        {
            // the reply really is the framework's problem-details 404 — the thing this test is about
            Assert.Equal(HttpStatusCode.NotFound, raw.StatusCode);
            Assert.Equal("application/problem+json", raw.Content.Headers.ContentType?.MediaType);
            Assert.Contains("\"status\":404", await raw.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        var answer = await new PlatformAdministratorStatusClient(
                http,
                new HttpContextAccessor(),
                Options.Create(new PlatformServiceOptions { InternalApiKey = TestOnlyInternalKey }),
                NullLogger<PlatformAdministratorStatusClient>.Instance)
            .GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Unavailable, answer.Verdict);
    }

    /// <summary>
    /// BL-521 FIX3 item 7 — Platform refusing THIS service's internal key (401 / 403) is a configuration fault: every
    /// platform sign-in answers 503 until it is fixed, so it is logged at Error, and never with the key or its value. A 5xx
    /// stays a passing outage (Warning).
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, LogLevel.Error)]
    [InlineData(HttpStatusCode.Forbidden, LogLevel.Error)]
    [InlineData(HttpStatusCode.ServiceUnavailable, LogLevel.Warning)]
    public async Task A_refused_internal_key_is_logged_as_an_error_without_the_key(HttpStatusCode status, LogLevel expected)
    {
        var logger = new ListLogger();
        var client = Client(
            new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("") })),
            logger: logger);

        var answer = await client.GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Unavailable, answer.Verdict);
        var entry = Assert.Single(logger.Entries);
        Assert.True(entry.Level == expected, $"{status}: logged at {entry.Level}: {entry.Message}");
        Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(TestOnlyInternalKey, entry.Message, StringComparison.Ordinal);
    }

    // ── the request itself ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_request_asks_the_internal_status_route_by_normalized_email_with_the_internal_key()
    {
        HttpRequestMessage? seen = null;
        var answer = await ClientFor(request =>
        {
            seen = request;
            return Ok(Status(true, "PartnerAdmin"))(request);
        }).GetActiveActorTypeAsync("  " + Email + " ", CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Confirmed, answer.Verdict);
        Assert.NotNull(seen);
        Assert.Equal(HttpMethod.Get, seen!.Method);
        Assert.Equal("/api/internal/platform-administrators/status", seen.RequestUri!.AbsolutePath);
        Assert.Equal("?email=wire.probe@bl521.test", Uri.UnescapeDataString(seen.RequestUri.Query));
        Assert.Equal(TestOnlyInternalKey, Assert.Single(seen.Headers.GetValues("X-Internal-Api-Key")));
    }

    // ── IsActiveAsync (the password-reset doors): fail closed ─────────────────────────────────────────

    [Fact]
    public async Task Is_active_is_true_only_for_an_active_record_and_false_when_there_is_no_answer()
    {
        Assert.True(await ClientFor(Ok(Status(true, "PartnerAdmin"))).IsActiveAsync(Email, CancellationToken.None));
        Assert.False(await ClientFor(Ok(Status(false, "PlatformAdmin", "Suspended"))).IsActiveAsync(Email, CancellationToken.None));
        Assert.False(await ClientFor(Reply(HttpStatusCode.ServiceUnavailable, "")).IsActiveAsync(Email, CancellationToken.None));
        Assert.False(await ClientFor(_ => throw new HttpRequestException("down")).IsActiveAsync(Email, CancellationToken.None));
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────

    private static string Status(bool isActive, string actorType, string status = "Active") =>
        $$"""{"data":{"id":"{{Guid.NewGuid()}}","email":"wire.probe@bl521.test","status":"{{status}}","isDeleted":false,"isActive":{{(isActive ? "true" : "false")}},"actorType":"{{actorType}}"},"statusCode":200,"isSuccessful":true,"errors":[],"reason_code":null,"correlation_id":null}""";

    private static Func<HttpRequestMessage, HttpResponseMessage> Ok(string body) => Reply(HttpStatusCode.OK, body);

    private static Func<HttpRequestMessage, HttpResponseMessage> Reply(HttpStatusCode status, string body) =>
        _ => new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static PlatformAdministratorStatusClient ClientFor(Func<HttpRequestMessage, HttpResponseMessage> reply) =>
        Client(new StubHandler((request, _) => Task.FromResult(reply(request))));

    private static PlatformAdministratorStatusClient Client(
        HttpMessageHandler handler,
        string internalKey = TestOnlyInternalKey,
        TimeSpan? timeout = null,
        ILogger<PlatformAdministratorStatusClient>? logger = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://platform.bl521.test") };
        if (timeout is { } t)
        {
            http.Timeout = t;
        }

        return new PlatformAdministratorStatusClient(
            http,
            new HttpContextAccessor(),
            Options.Create(new PlatformServiceOptions { InternalApiKey = internalKey }),
            logger ?? NullLogger<PlatformAdministratorStatusClient>.Instance);
    }

    /// <summary>Every entry the client logged: level and the rendered message.</summary>
    private sealed class ListLogger : ILogger<PlatformAdministratorStatusClient>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
