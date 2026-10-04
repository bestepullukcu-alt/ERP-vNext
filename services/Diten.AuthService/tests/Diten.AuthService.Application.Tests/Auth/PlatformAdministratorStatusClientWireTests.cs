using System.Net;
using System.Text;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
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
/// (network error, timeout, 5xx, a bare 404, an unreadable or empty body, no internal key) is Unavailable.</para>
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
    [InlineData("null")]
    public async Task An_active_record_of_an_unknown_or_missing_type_is_refused_never_defaulted(string actorTypeJson)
    {
        var body = $$"""{"data":{"id":"{{Guid.NewGuid()}}","email":"wire.probe@bl521.test","status":"Active","isDeleted":false,"isActive":true,"actorType":{{actorTypeJson}}},"statusCode":200,"isSuccessful":true,"errors":[]}""";

        var answer = await ClientFor(Ok(body)).GetActiveActorTypeAsync(Email, CancellationToken.None);

        Assert.Equal(PlatformAdministratorRecordVerdict.Refused, answer.Verdict);
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

    private static PlatformAdministratorStatusClient Client(HttpMessageHandler handler, string internalKey = TestOnlyInternalKey, TimeSpan? timeout = null)
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
            NullLogger<PlatformAdministratorStatusClient>.Instance);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            send(request, cancellationToken);
    }
}
