using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.RecordCodes;
using Diten.MdmService.Infrastructure.RecordCodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.BankMaster;

/// <summary>
/// MOD-0289 DL-1 (AC-60) — the ONE adapter of the record code port, measured against RECORD-CODE-ALLOCATE 1.0.0 (§1 headers,
/// §2 body, §3 / §4 answers): the call it makes and the way it classifies every answer, with no network.
/// </summary>
public sealed class RecordCodeAllocatorClientTests
{
    private const string Ok = """{"data":{"code":"GMG-BNK-000042","sequence":42},"statusCode":200,"isSuccessful":true,"errors":[],"reason_code":null,"correlation_id":"c1"}""";

    private static PlatformRecordCodeAllocatorClient Client(
        StubHandler handler, bool configured = true, bool authenticated = true, TimeSpan? timeout = null, Uri? baseAddress = null)
    {
        var options = new RecordCodeAllocatorOptions
        {
            PlatformBaseAddress = baseAddress ?? new Uri("https://platform.test/"),
            CredentialIdentifier = configured ? "mdm-credential-id" : null,
            CredentialSecret = configured ? "mdm-credential-secret" : null,
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };
        var context = new DefaultHttpContext();
        if (authenticated)
        {
            context.Request.Headers.Authorization = "Bearer user-token";
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())], "test"));
        }

        return new PlatformRecordCodeAllocatorClient(
            new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }, new HttpContextAccessor { HttpContext = context }, Options.Create(options));
    }

    private static Task<RecordCodeAllocationResult> BankAsync(PlatformRecordCodeAllocatorClient client) =>
        client.AllocateAsync(RecordCodePrefixScope.Group, null, "BNK", "mdm-bank-key-1");

    [Fact]
    public async Task Mod0289Dl1_AC60_The_call_is_the_contracts_credential_audience_forwarded_user_token_and_group_body()
    {
        var handler = new StubHandler(_ => Respond(HttpStatusCode.OK, Ok));
        var result = await BankAsync(Client(handler));

        Assert.Equal(RecordCodeAllocationOutcome.Allocated, result.Outcome);
        Assert.Equal(("GMG-BNK-000042", 42L), (result.Code, result.Sequence));
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://platform.test/api/internal/record-codes/allocate", request.Uri);
        Assert.Equal("mdm-credential-id", request.Header("X-Record-Code-Credential-Id"));
        Assert.Equal("mdm-credential-secret", request.Header("X-Record-Code-Credential"));
        Assert.Equal("TRUSTED_RECORD_CODE_ALLOCATE", request.Header("X-Record-Code-Audience"));
        Assert.Equal("Bearer user-token", request.Header("Authorization"));
        // The tenant is the token's: never sent as a header.
        Assert.Null(request.Header("X-Tenant-Id"));
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal(["recordType", "prefixScope", "legalEntityId", "idempotencyKey"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("BNK", root.GetProperty("recordType").GetString());
        Assert.Equal("Group", root.GetProperty("prefixScope").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("legalEntityId").ValueKind);
        Assert.Equal("mdm-bank-key-1", root.GetProperty("idempotencyKey").GetString());
        Assert.Equal("application/json", request.ContentType);
    }

    [Theory]
    // 200 is an allocation only when the envelope says so.
    [InlineData(200, """{"data":{"code":"GMG-BNK-000042","sequence":42},"statusCode":200,"isSuccessful":false,"errors":[]}""", "Unavailable", null)]
    [InlineData(200, """{"data":null,"statusCode":200,"isSuccessful":true,"errors":[]}""", "Unavailable", null)]
    [InlineData(200, """{"statusCode":200,"isSuccessful":true,"errors":[]}""", "Unavailable", null)]
    [InlineData(200, """{"data":{"sequence":42},"statusCode":200,"isSuccessful":true,"errors":[]}""", "Unavailable", null)]
    [InlineData(200, """{"data":{"code":"","sequence":42},"statusCode":200,"isSuccessful":true,"errors":[]}""", "Unavailable", null)]
    [InlineData(200, """{"data":{"code":"GMG-BNK-000042"},"statusCode":200,"isSuccessful":true,"errors":[]}""", "Unavailable", null)]
    [InlineData(200, """{"data":{"code":"GMG-BNK-000042","sequence":"42"},"statusCode":200,"isSuccessful":true,"errors":[]}""", "Unavailable", null)]
    [InlineData(200, """["not an envelope"]""", "Unavailable", null)]
    [InlineData(200, "not json", "Unavailable", null)]
    // The two permission refusals are Create 403; the unknown company is Create 422.
    [InlineData(403, """{"data":null,"statusCode":403,"isSuccessful":false,"errors":["x"],"reason_code":"RECORD_CODE_PERMISSION_MISSING"}""", "PermissionMissing", "RECORD_CODE_PERMISSION_MISSING")]
    [InlineData(403, """{"data":null,"statusCode":403,"isSuccessful":false,"errors":["x"],"reason_code":"LEGAL_ENTITY_READ_PERMISSION_MISSING"}""", "PermissionMissing", "LEGAL_ENTITY_READ_PERMISSION_MISSING")]
    [InlineData(404, """{"data":null,"statusCode":404,"isSuccessful":false,"errors":["x"],"reason_code":"LEGAL_ENTITY_NOT_FOUND"}""", "LegalEntityUnknown", "LEGAL_ENTITY_NOT_FOUND")]
    // Everything else is "unavailable" (Create 503) — each a different status, ProblemDetails included.
    [InlineData(403, """{"data":null,"statusCode":403,"isSuccessful":false,"errors":["x"],"reason_code":"RECORD_CODE_FORBIDDEN"}""", "Unavailable", "RECORD_CODE_FORBIDDEN")]
    [InlineData(403, """{"data":null,"statusCode":403,"isSuccessful":false,"errors":["x"],"reason_code":"RECORD_CODE_TENANT_NOT_ACTIVE"}""", "Unavailable", "RECORD_CODE_TENANT_NOT_ACTIVE")]
    [InlineData(404, """{"data":null,"statusCode":404,"isSuccessful":false,"errors":["x"],"reason_code":"SOMETHING_ELSE"}""", "Unavailable", "SOMETHING_ELSE")]
    [InlineData(422, """{"data":null,"statusCode":422,"isSuccessful":false,"errors":["x"],"reason_code":"RECORD_TYPE_UNKNOWN"}""", "Unavailable", "RECORD_TYPE_UNKNOWN")]
    [InlineData(422, """{"data":null,"statusCode":422,"isSuccessful":false,"errors":["x"],"reason_code":"RECORD_CODE_SCOPE_INVALID"}""", "Unavailable", "RECORD_CODE_SCOPE_INVALID")]
    [InlineData(409, """{"data":null,"statusCode":409,"isSuccessful":false,"errors":["x"],"reason_code":"GROUP_PREFIX_MISSING"}""", "Unavailable", "GROUP_PREFIX_MISSING")]
    [InlineData(409, """{"data":null,"statusCode":409,"isSuccessful":false,"errors":["x"],"reason_code":"LEGAL_ENTITY_SHORT_CODE_MISSING"}""", "Unavailable", "LEGAL_ENTITY_SHORT_CODE_MISSING")]
    [InlineData(401, """{"data":null,"statusCode":401,"isSuccessful":false,"errors":["x"],"reason_code":"RECORD_CODE_UNAUTHENTICATED"}""", "Unavailable", "RECORD_CODE_UNAUTHENTICATED")]
    [InlineData(429, """{"data":null,"statusCode":429,"isSuccessful":false,"errors":["x"],"reason_code":"RECORD_CODE_RATE_LIMITED"}""", "Unavailable", "RECORD_CODE_RATE_LIMITED")]
    [InlineData(503, """{"data":null,"statusCode":503,"isSuccessful":false,"errors":["x"],"reason_code":"RECORD_CODE_UNAVAILABLE"}""", "Unavailable", "RECORD_CODE_UNAVAILABLE")]
    [InlineData(503, """{"type":"about:blank","title":"x","status":503,"reason_code":"AUDIT_RECORD_UNAVAILABLE"}""", "Unavailable", "AUDIT_RECORD_UNAVAILABLE")]
    [InlineData(500, """{"type":"about:blank","title":"x","status":500,"reason_code":"AUDIT_INTENT_INVALID"}""", "Unavailable", "AUDIT_INTENT_INVALID")]
    public async Task Mod0289Dl1_AC60_Every_answer_of_the_contract_is_classified_and_only_a_clean_200_is_a_code(
        int status, string body, string expectedOutcome, string? reason)
    {
        var handler = new StubHandler(_ => Respond((HttpStatusCode)status, body));
        var result = await BankAsync(Client(handler));

        Assert.Equal(expectedOutcome, result.Outcome.ToString());
        if (result.Outcome != RecordCodeAllocationOutcome.Allocated) Assert.Null(result.Code);
        if (reason is not null) Assert.Equal(reason, result.ReasonCode);
        Assert.Single(handler.Requests); // never retried
    }

    [Fact]
    public async Task Mod0289Dl1_AC60_No_json_content_type_or_an_empty_body_is_not_an_answer()
    {
        var notJson = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Ok, Encoding.UTF8, "text/html") });
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable, (await BankAsync(Client(notJson))).Outcome);
        var empty = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("", Encoding.UTF8, "application/json") });
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable, (await BankAsync(Client(empty))).Outcome);
    }

    [Fact]
    public async Task Mod0289Dl1_AC60_A_port_without_configuration_or_a_user_token_makes_no_call_and_is_unavailable()
    {
        var handler = new StubHandler(_ => Respond(HttpStatusCode.OK, Ok));
        // The credential is the environment's: with none, the port is not bound.
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable, (await BankAsync(Client(handler, configured: false))).Outcome);
        // No signed-in user: nobody to act for.
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable, (await BankAsync(Client(handler, authenticated: false))).Outcome);
        // A plain-http address that is not loopback is not an internal address.
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable,
            (await BankAsync(Client(handler, baseAddress: new Uri("http://platform.example.com/")))).Outcome);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Mod0289Dl1_AC60_A_call_that_breaks_the_scope_rule_never_leaves_the_process()
    {
        var handler = new StubHandler(_ => Respond(HttpStatusCode.OK, Ok));
        var client = Client(handler);
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable,
            (await client.AllocateAsync(RecordCodePrefixScope.LegalEntity, null, "BAC", "k")).Outcome);
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable,
            (await client.AllocateAsync(RecordCodePrefixScope.Group, Guid.NewGuid(), "BNK", "k")).Outcome);
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable,
            (await client.AllocateAsync(RecordCodePrefixScope.Group, null, "BNK", " ")).Outcome);
        Assert.Empty(handler.Requests);
        // A company-scoped call carries the company.
        var ok = await client.AllocateAsync(RecordCodePrefixScope.LegalEntity, Guid.Parse("11111111-1111-1111-1111-111111111111"), "BAC", "k2");
        Assert.Equal(RecordCodeAllocationOutcome.Allocated, ok.Outcome);
        using var body = JsonDocument.Parse(handler.Requests.Single().Body);
        Assert.Equal("LegalEntity", body.RootElement.GetProperty("prefixScope").GetString());
        Assert.Equal("11111111-1111-1111-1111-111111111111", body.RootElement.GetProperty("legalEntityId").GetString());
    }

    [Fact]
    public async Task Mod0289Dl1_AC60_A_timeout_an_unreachable_host_or_a_dropped_connection_is_unavailable_never_an_exception()
    {
        var slow = new StubHandler(async (_, ct) => { await Task.Delay(TimeSpan.FromSeconds(10), ct); return Respond(HttpStatusCode.OK, Ok); });
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable,
            (await BankAsync(Client(slow, timeout: TimeSpan.FromMilliseconds(80)))).Outcome);
        var down = new StubHandler(_ => throw new HttpRequestException("connection refused"));
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable, (await BankAsync(Client(down))).Outcome);
        var dropped = new StubHandler(_ => throw new IOException("reset"));
        Assert.Equal(RecordCodeAllocationOutcome.Unavailable, (await BankAsync(Client(dropped))).Outcome);
    }

    private static HttpResponseMessage Respond(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
            _respond = (request, _) => Task.FromResult(respond(request));

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

        public List<Seen> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new Seen(
                request.Method, request.RequestUri!.ToString(),
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase),
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Content?.Headers.ContentType?.MediaType));
            return await _respond(request, cancellationToken);
        }
    }

    private sealed record Seen(HttpMethod Method, string Uri, Dictionary<string, string> Headers, string Body, string? ContentType)
    {
        public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
    }
}
