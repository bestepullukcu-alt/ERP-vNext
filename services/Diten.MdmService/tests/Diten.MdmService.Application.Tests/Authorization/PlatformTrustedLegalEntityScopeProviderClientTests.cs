using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Infrastructure.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.Authorization;

public sealed class PlatformTrustedLegalEntityScopeProviderClientTests
{
    private const string Module = "product-item-sku-master";
    private const string Permission = "mdm.global-products.read";

    [Fact]
    public async Task Exact_success_contract_maps_bounded_sorted_candidates_without_retry()
    {
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() }
            .OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray();
        var handler = new ScopeRecordingHandler(_ => Json(HttpStatusCode.OK, Success(tenant, subject, ids)));

        var result = await Client(handler).ResolveAsync(tenant, subject, Module, Permission);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ids, result.LegalEntityIds);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://platform.internal/api/internal/v1/access-governance/legal-entity-scope/resolve", request.Uri);
        Assert.Equal("{\"module_code\":\"product-item-sku-master\",\"permission_key\":\"mdm.global-products.read\"}", request.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.Unauthorized, 401)]
    [InlineData(HttpStatusCode.Forbidden, 403)]
    [InlineData(HttpStatusCode.Conflict, 503)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    [InlineData(HttpStatusCode.GatewayTimeout, 504)]
    public async Task Provider_failure_is_mapped_once_without_retry(HttpStatusCode status, int expected)
    {
        var handler = new ScopeRecordingHandler(_ => Json(status, Failure((int)status)));
        var result = await Client(handler).ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), Module, Permission);
        Assert.False(result.IsSuccessful);
        Assert.Equal(expected, result.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Strict_json_rejects_echo_drift_extra_fields_duplicates_unsorted_and_non_json()
    {
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        var a = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var b = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var payloads = new[]
        {
            Success(Guid.NewGuid(), subject, []),
            Success(tenant, Guid.NewGuid(), []),
            Success(tenant, subject, [a, a]),
            Success(tenant, subject, [b, a]),
            Success(tenant, subject, [], dataExtra: true),
            Success(tenant, subject, [], envelopeExtra: true)
        };
        foreach (var payload in payloads)
        {
            var handler = new ScopeRecordingHandler(_ => Json(HttpStatusCode.OK, payload));
            var result = await Client(handler).ResolveAsync(tenant, subject, Module, Permission);
            Assert.Equal(503, result.StatusCode);
            Assert.Equal("LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID", result.FailureCode);
        }

        var html = new ScopeRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent("<html>login</html>", Encoding.UTF8, "text/html") });
        Assert.Equal(503, (await Client(html).ResolveAsync(tenant, subject, Module, Permission)).StatusCode);
    }

    [Theory]
    [InlineData("{\"data\":null,\"statusCode\":503,\"isSuccessful\":false,\"errors\":[],\"reason_code\":\"x\",\"correlation_id\":\"c\"}")]
    [InlineData("{\"data\":null,\"data\":null,\"statusCode\":503,\"isSuccessful\":false,\"errors\":[\"x\"],\"reason_code\":\"x\",\"correlation_id\":\"c\"}")]
    public async Task Non_success_with_empty_errors_or_duplicate_root_property_is_contract_invalid(string payload)
    {
        var handler = new ScopeRecordingHandler(_ => Json(HttpStatusCode.ServiceUnavailable, payload));
        var result = await Client(handler).ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), Module, Permission);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID", result.FailureCode);
    }

    [Fact]
    public async Task Duplicate_data_property_is_contract_invalid()
    {
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        var payload = $$"""
            {"data":{"tenantId":"{{tenant:D}}","tenantId":"{{tenant:D}}","subjectId":"{{subject:D}}","moduleCode":"{{Module}}","permissionKey":"{{Permission}}","evaluatedAtUtc":"2026-08-27T00:00:00Z","legalEntityIds":[]},"statusCode":200,"isSuccessful":true,"errors":[],"reason_code":null,"correlation_id":"c"}
            """;
        var result = await Client(new ScopeRecordingHandler(_ => Json(HttpStatusCode.OK, payload)))
            .ResolveAsync(tenant, subject, Module, Permission);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID", result.FailureCode);
    }

    [Fact]
    public async Task Response_over_32KiB_redirect_and_transport_failure_fail_closed_without_retry()
    {
        var oversized = new ScopeRecordingHandler(_ => Json(HttpStatusCode.OK, new string('x', 32_769)));
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        Assert.Equal(503, (await Client(oversized).ResolveAsync(tenant, subject, Module, Permission)).StatusCode);
        Assert.Single(oversized.Requests);

        var redirect = new ScopeRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Redirect)
        { Content = new StringContent(Failure(302), Encoding.UTF8, "application/json") });
        Assert.Equal(503, (await Client(redirect).ResolveAsync(tenant, subject, Module, Permission)).StatusCode);
        Assert.Single(redirect.Requests);

        var failed = new ScopeRecordingHandler(_ => throw new HttpRequestException("offline"));
        Assert.Equal(503, (await Client(failed).ResolveAsync(tenant, subject, Module, Permission)).StatusCode);
        Assert.Single(failed.Requests);
    }

    [Fact]
    public async Task Unknown_content_length_stream_over_32KiB_is_rejected()
    {
        var content = new UnknownLengthContent(Encoding.UTF8.GetBytes(new string('x', 32_769)));
        content.Headers.ContentType = new("application/json");
        Assert.Null(content.Headers.ContentLength);
        var handler = new ScopeRecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        var result = await Client(handler).ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), Module, Permission);
        Assert.Equal(503, result.StatusCode);
        Assert.Equal("LEGAL_ENTITY_SCOPE_PROVIDER_CONTRACT_INVALID", result.FailureCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Timeout_maps_504_and_caller_cancellation_propagates()
    {
        var blocking = new ScopeBlockingHandler();
        var tenant = Guid.NewGuid(); var subject = Guid.NewGuid();
        var timeout = await Client(blocking, TimeSpan.FromMilliseconds(20)).ResolveAsync(tenant, subject, Module, Permission);
        Assert.Equal(504, timeout.StatusCode);
        Assert.Equal(1, blocking.CallCount);

        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Client(new ScopeBlockingHandler()).ResolveAsync(tenant, subject, Module, Permission, cancellation.Token));
    }

    [Fact]
    public async Task Invalid_pair_or_configuration_fails_before_dispatch()
    {
        var handler = new ScopeRecordingHandler(_ => throw new InvalidOperationException("must not dispatch"));
        var invalid = await Client(handler).ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), "other-module", Permission);
        Assert.Equal(400, invalid.StatusCode);
        Assert.Empty(handler.Requests);

        var unconfigured = Client(handler, options: new TrustedLegalEntityScopeProviderOptions());
        var result = await unconfigured.ResolveAsync(Guid.NewGuid(), Guid.NewGuid(), Module, Permission);
        Assert.Equal(503, result.StatusCode);
        Assert.Empty(handler.Requests);
    }

    internal static PlatformTrustedLegalEntityScopeProviderClient Client(
        HttpMessageHandler handler, TimeSpan? timeout = null, DefaultHttpContext? context = null,
        TrustedLegalEntityScopeProviderOptions? options = null)
    {
        context ??= AuthenticatedContext();
        options ??= new TrustedLegalEntityScopeProviderOptions
        {
            PlatformBaseAddress = new Uri("https://platform.internal/"), Timeout = timeout ?? TimeSpan.FromSeconds(2),
            CredentialIdentifier = "scope-id", CredentialSecret = "scope-secret"
        };
        return new(new HttpClient(handler), new HttpContextAccessor { HttpContext = context }, Options.Create(options));
    }

    internal static DefaultHttpContext AuthenticatedContext(string token = "delegated-token")
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "user")], "Bearer"));
        context.Request.Headers.Authorization = "Bearer " + token;
        return context;
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string payload) => new(status)
    { Content = new StringContent(payload, Encoding.UTF8, "application/json") };

    private static string Success(Guid tenant, Guid subject, IReadOnlyList<Guid> ids,
        bool dataExtra = false, bool envelopeExtra = false)
    {
        var data = new Dictionary<string, object?>
        {
            ["tenantId"] = tenant, ["subjectId"] = subject, ["moduleCode"] = Module,
            ["permissionKey"] = Permission, ["evaluatedAtUtc"] = "2026-08-27T00:00:00Z", ["legalEntityIds"] = ids
        };
        if (dataExtra) data["extra"] = "x";
        var envelope = new Dictionary<string, object?>
        {
            ["data"] = data, ["statusCode"] = 200, ["isSuccessful"] = true,
            ["errors"] = Array.Empty<string>(), ["reason_code"] = null, ["correlation_id"] = "corr"
        };
        if (envelopeExtra) envelope["extra"] = "x";
        return JsonSerializer.Serialize(envelope);
    }

    private static string Failure(int status) => JsonSerializer.Serialize(new
    {
        data = (object?)null, statusCode = status, isSuccessful = false,
        errors = new[] { "failure" }, reason_code = "failure", correlation_id = "corr"
    });
}

internal sealed class ScopeRecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
{
    public List<ScopeRequestSnapshot> Requests { get; } = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add(new(request.Method, request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.ToString(),
            request.Headers.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.OrdinalIgnoreCase), body));
        return response(request);
    }
}

internal sealed record ScopeRequestSnapshot(HttpMethod Method, string Uri, string? Authorization,
    IReadOnlyDictionary<string, string[]> Headers, string? Body);

internal sealed class ScopeBlockingHandler : HttpMessageHandler
{
    public int CallCount { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("unreachable");
    }
}

internal sealed class UnknownLengthContent(byte[] payload) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        => stream.WriteAsync(payload).AsTask();

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }

    protected override Task<Stream> CreateContentReadStreamAsync()
        => Task.FromResult<Stream>(new MemoryStream(payload, writable: false));
}
