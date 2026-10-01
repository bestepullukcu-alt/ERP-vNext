using System.Net;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class PlatformWorkflowVerifiedMarketResolverClientTests
{
    [Fact]
    public async Task Resolve_uses_service_bearer_and_static_second_factor_without_tenant_header()
    {
        var handler = new CaptureHandler(_ => Success("TR"));
        var identities = new IdentityProvider();

        var result = await Client(handler, identities).ResolveLatestAsync(Guid.NewGuid(), "TR");

        Assert.True(result.IsSuccessful, result.FailureCode);
        Assert.Equal("TR", result.Selection!.ValueCode);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer service-token-1", request.Authorization);
        Assert.Equal("resolver-id", request.Headers[PlatformWorkflowVerifiedMarketResolverClient.CredentialIdHeader].Single());
        Assert.Equal("resolver-secret", request.Headers[PlatformWorkflowVerifiedMarketResolverClient.CredentialSecretHeader].Single());
        Assert.Equal("VERIFIED_GSKU_RESOLVE", request.Headers[PlatformWorkflowVerifiedMarketResolverClient.AudienceHeader].Single());
        Assert.False(request.Headers.ContainsKey("X-Tenant-Id"));
        Assert.Equal("{\"market_code\":\"TR\"}", request.Body);
    }

    [Fact]
    public async Task Resolve_401_refreshes_once_and_never_reuses_interactive_identity()
    {
        var attempts = 0;
        var handler = new CaptureHandler(_ => Interlocked.Increment(ref attempts) == 1
            ? Failure(HttpStatusCode.Unauthorized, "REFERENCE_UNAUTHENTICATED")
            : Success("TR"));
        var identities = new IdentityProvider();

        var result = await Client(handler, identities).ResolveLatestAsync(Guid.NewGuid(), "TR");

        Assert.True(result.IsSuccessful);
        Assert.Equal([false, true], identities.Refreshes);
        Assert.Equal(["Bearer service-token-1", "Bearer service-token-2"], handler.Requests.Select(x => x.Authorization));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, 403, "REFERENCE_PROVIDER_FORBIDDEN")]
    [InlineData(HttpStatusCode.NotFound, 404, "REFERENCE_MARKET_NOT_FOUND")]
    [InlineData(HttpStatusCode.Conflict, 409, "REFERENCE_CONTRACT_MISMATCH")]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503, "REFERENCE_PROVIDER_UNAVAILABLE")]
    [InlineData(HttpStatusCode.GatewayTimeout, 504, "REFERENCE_PROVIDER_TIMEOUT")]
    public async Task Resolve_maps_stable_provider_failures(
        HttpStatusCode providerStatus,
        int expectedStatus,
        string expectedCode)
    {
        var result = await Client(
            new CaptureHandler(_ => Failure(providerStatus, expectedCode)),
            new IdentityProvider()).ResolveLatestAsync(Guid.NewGuid(), "TR");

        Assert.False(result.IsSuccessful);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal(expectedCode, result.FailureCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, null, 503, "REFERENCE_PROVIDER_UNAVAILABLE")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "text/html", 503, "REFERENCE_PROVIDER_UNAVAILABLE")]
    [InlineData(HttpStatusCode.GatewayTimeout, null, 504, "REFERENCE_PROVIDER_TIMEOUT")]
    [InlineData(HttpStatusCode.GatewayTimeout, "text/html", 504, "REFERENCE_PROVIDER_TIMEOUT")]
    [InlineData(HttpStatusCode.RequestTimeout, "text/html", 503, "REFERENCE_PROVIDER_UNAVAILABLE")]
    [InlineData(HttpStatusCode.TooManyRequests, "text/html", 503, "REFERENCE_PROVIDER_UNAVAILABLE")]
    [InlineData(HttpStatusCode.Forbidden, "text/html", 403, "REFERENCE_PROVIDER_FORBIDDEN")]
    [InlineData(HttpStatusCode.NotFound, "text/html", 404, "REFERENCE_MARKET_NOT_FOUND")]
    [InlineData(HttpStatusCode.Conflict, "text/html", 409, "REFERENCE_CONTRACT_MISMATCH")]
    public async Task Resolve_non_json_failure_preserves_transport_semantics(
        HttpStatusCode providerStatus,
        string? contentType,
        int expectedStatus,
        string expectedCode)
    {
        var content = new ByteArrayContent(contentType is null ? [] : Encoding.UTF8.GetBytes("<html>down</html>"));
        if (contentType is not null) content.Headers.ContentType = new(contentType);
        var response = new HttpResponseMessage(providerStatus) { Content = content };

        var result = await Client(new CaptureHandler(_ => response), new IdentityProvider())
            .ResolveLatestAsync(Guid.NewGuid(), "TR");

        Assert.False(result.IsSuccessful);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal(expectedCode, result.FailureCode);
    }

    [Fact]
    public async Task Resolve_rejects_extended_or_mismatched_success_contract()
    {
        var extended = Json(HttpStatusCode.OK, new
        {
            data = new { market = Market("TR"), secret = "must-not-be-accepted" },
            statusCode = 200, isSuccessful = true, errors = Array.Empty<string>(),
            reason_code = (string?)null, correlation_id = "corr"
        });
        var result = await Client(new CaptureHandler(_ => extended), new IdentityProvider())
            .ResolveLatestAsync(Guid.NewGuid(), "TR");

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("REFERENCE_CONTRACT_MISMATCH", result.FailureCode);
    }

    [Fact]
    public async Task Resolve_budget_maps_timeout_and_caller_cancellation_propagates()
    {
        var blocking = new BlockingHandler();
        var timeout = await Client(blocking, new IdentityProvider(TimeSpan.FromSeconds(3)))
            .ResolveLatestAsync(Guid.NewGuid(), "TR");
        Assert.Equal(504, timeout.StatusCode);

        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Client(blocking, new IdentityProvider()).ResolveLatestAsync(Guid.NewGuid(), "TR", caller.Token));
    }

    [Fact]
    public async Task Resolve_invalid_input_or_configuration_fails_before_identity_and_transport()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException());
        var identities = new IdentityProvider();
        var client = Client(handler, identities, new VerifiedMarketResolverOptions());

        var result = await client.ResolveLatestAsync(Guid.NewGuid(), "tr");

        Assert.Equal(503, result.StatusCode);
        Assert.Empty(handler.Requests);
        Assert.Empty(identities.Refreshes);
    }

    private static PlatformWorkflowVerifiedMarketResolverClient Client(
        HttpMessageHandler handler,
        IWorkflowVerifiedMarketServiceIdentityProvider identities,
        VerifiedMarketResolverOptions? options = null) => new(
        new Factory(handler), identities, Options.Create(options ?? ValidOptions()));

    private static VerifiedMarketResolverOptions ValidOptions() => new()
    {
        PlatformBaseAddress = new Uri("https://platform.internal/"),
        Timeout = TimeSpan.FromSeconds(1),
        CredentialIdentifier = "resolver-id",
        CredentialSecret = "resolver-secret"
    };

    private static HttpResponseMessage Success(string code) => Json(HttpStatusCode.OK, new
    {
        data = new { market = Market(code) }, statusCode = 200, isSuccessful = true,
        errors = Array.Empty<string>(), reason_code = (string?)null, correlation_id = (string?)null
    });
    private static object Market(string code) => new
    {
        set_code = "market", value_code = code, catalog_version_id = Guid.NewGuid(),
        catalog_version_number = 1, resolution_mode = "LATEST", resolved_at_utc = DateTimeOffset.UtcNow
    };
    private static HttpResponseMessage Failure(HttpStatusCode status, string reason) => Json(status, new
    {
        data = (object?)null, statusCode = (int)status, isSuccessful = false,
        errors = new[] { reason }, reason_code = reason, correlation_id = "corr"
    });
    private static HttpResponseMessage Json(HttpStatusCode status, object value) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private sealed class IdentityProvider(TimeSpan? delay = null) : IWorkflowVerifiedMarketServiceIdentityProvider
    {
        public List<bool> Refreshes { get; } = [];
        public async Task<WorkflowVerifiedMarketServiceIdentity> GetAsync(Guid tenantId, bool forceRefresh, CancellationToken cancellationToken = default)
        {
            Refreshes.Add(forceRefresh);
            if (delay is { } value) await Task.Delay(value, cancellationToken);
            return new($"service-token-{Refreshes.Count}", DateTimeOffset.UtcNow.AddMinutes(5));
        }
    }
    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, false);
    }
    private sealed class CaptureHandler(Func<RequestSnapshot, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<RequestSnapshot> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var snapshot = new RequestSnapshot(
                request.Headers.Authorization?.ToString(),
                request.Headers.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
                await request.Content!.ReadAsStringAsync(cancellationToken));
            Requests.Add(snapshot);
            return response(snapshot);
        }
    }
    private sealed record RequestSnapshot(string? Authorization, IReadOnlyDictionary<string, string[]> Headers, string Body);
    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException();
        }
    }
}
