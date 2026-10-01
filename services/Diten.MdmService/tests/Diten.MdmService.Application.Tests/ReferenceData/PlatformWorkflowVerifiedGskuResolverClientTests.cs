using System.Net;
using System.Text;
using System.Text.Json;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

public sealed class PlatformWorkflowVerifiedGskuResolverClientTests
{
    [Fact]
    public async Task Resolve_uses_tenant_service_bearer_and_exact_two_selection_contract()
    {
        var handler = new CaptureHandler(_ => Success());
        var identities = new IdentityProvider();
        var tenantId = Guid.NewGuid();

        var result = await Client(handler, identities).ResolveLatestAsync(
            tenantId, "SCALAR_QUANTITY_APPLIES", "KGM");

        Assert.True(result.IsSuccessful, result.FailureCode);
        Assert.Equal([(tenantId, false)], identities.Calls);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer service-token-1", request.Authorization);
        Assert.False(request.Headers.ContainsKey("X-Tenant-Id"));
        Assert.Equal("resolver-id", request.Headers[PlatformWorkflowVerifiedGskuResolverClient.CredentialIdHeader].Single());
        Assert.Equal("resolver-secret", request.Headers[PlatformWorkflowVerifiedGskuResolverClient.CredentialSecretHeader].Single());
        Assert.Equal("VERIFIED_GSKU_RESOLVE", request.Headers[PlatformWorkflowVerifiedGskuResolverClient.AudienceHeader].Single());
        Assert.Equal(
            "{\"selections\":[{\"set_code\":\"pack-applicability\",\"value_code\":\"SCALAR_QUANTITY_APPLIES\",\"resolution_mode\":\"LATEST\"},{\"set_code\":\"uom\",\"value_code\":\"KGM\",\"resolution_mode\":\"LATEST\"}]}",
            request.Body);
    }

    [Fact]
    public async Task Resolve_401_refreshes_service_token_once()
    {
        var attempt = 0;
        var handler = new CaptureHandler(_ => ++attempt == 1 ? Failure(HttpStatusCode.Unauthorized) : Success());
        var identities = new IdentityProvider();

        var result = await Client(handler, identities).ResolveLatestAsync(
            Guid.NewGuid(), "SCALAR_QUANTITY_APPLIES", "KGM");

        Assert.True(result.IsSuccessful);
        Assert.Equal([false, true], identities.Calls.Select(call => call.Refresh));
        Assert.Equal(["Bearer service-token-1", "Bearer service-token-2"], handler.Requests.Select(x => x.Authorization));
    }

    [Fact]
    public async Task Resolve_rejects_extended_or_mismatched_success_contract()
    {
        var response = Json(HttpStatusCode.OK, new
        {
            data = new { selections = Selections(), secret = "not-allowed" },
            statusCode = 200, isSuccessful = true, errors = Array.Empty<string>(),
            reason_code = (string?)null, correlation_id = (string?)null
        });

        var result = await Client(new CaptureHandler(_ => response), new IdentityProvider())
            .ResolveLatestAsync(Guid.NewGuid(), "SCALAR_QUANTITY_APPLIES", "KGM");

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("REFERENCE_CONTRACT_MISMATCH", result.FailureCode);
    }

    [Fact]
    public async Task Resolve_invalid_input_fails_before_identity_or_transport()
    {
        var handler = new CaptureHandler(_ => throw new InvalidOperationException());
        var identities = new IdentityProvider();

        var result = await Client(handler, identities).ResolveLatestAsync(Guid.NewGuid(), "scalar", "kgm");

        Assert.Equal(503, result.StatusCode);
        Assert.Empty(identities.Calls);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Resolve_timeout_maps_504_and_caller_cancellation_propagates()
    {
        var blocking = new BlockingHandler();
        var timeout = await Client(blocking, new IdentityProvider(), TimeSpan.FromMilliseconds(20))
            .ResolveLatestAsync(Guid.NewGuid(), "SCALAR_QUANTITY_APPLIES", "KGM");
        Assert.Equal(504, timeout.StatusCode);

        using var caller = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Client(blocking, new IdentityProvider())
            .ResolveLatestAsync(Guid.NewGuid(), "SCALAR_QUANTITY_APPLIES", "KGM", caller.Token));
    }

    private static PlatformWorkflowVerifiedGskuResolverClient Client(
        HttpMessageHandler handler, IWorkflowVerifiedMarketServiceIdentityProvider identities,
        TimeSpan? timeout = null) => new(new Factory(handler), identities, Options.Create(new VerifiedGskuResolverOptions
        {
            PlatformBaseAddress = new Uri("https://platform.internal/"),
            Timeout = timeout ?? TimeSpan.FromSeconds(1),
            CredentialIdentifier = "resolver-id",
            CredentialSecret = "resolver-secret"
        }));

    private static HttpResponseMessage Success() => Json(HttpStatusCode.OK, new
    {
        data = new { selections = Selections() }, statusCode = 200, isSuccessful = true,
        errors = Array.Empty<string>(), reason_code = (string?)null, correlation_id = (string?)null
    });
    private static object[] Selections() =>
    [
        Selection("pack-applicability", "SCALAR_QUANTITY_APPLIES"),
        Selection("uom", "KGM")
    ];
    private static object Selection(string set, string code) => new
    {
        set_code = set, value_code = code, catalog_version_id = Guid.NewGuid(), catalog_version_number = 1,
        resolution_mode = "LATEST", resolved_at_utc = DateTimeOffset.UtcNow, is_retired = false, selectable_for_new = true
    };
    private static HttpResponseMessage Failure(HttpStatusCode status) => Json(status, new
    {
        data = (object?)null, statusCode = (int)status, isSuccessful = false,
        errors = new[] { "REFERENCE_UNAUTHENTICATED" }, reason_code = "REFERENCE_UNAUTHENTICATED", correlation_id = "corr"
    });
    private static HttpResponseMessage Json(HttpStatusCode status, object value) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
    };

    private sealed class IdentityProvider : IWorkflowVerifiedMarketServiceIdentityProvider
    {
        public List<(Guid TenantId, bool Refresh)> Calls { get; } = [];
        public Task<WorkflowVerifiedMarketServiceIdentity> GetAsync(Guid tenantId, bool forceRefresh, CancellationToken cancellationToken = default)
        {
            Calls.Add((tenantId, forceRefresh));
            return Task.FromResult(new WorkflowVerifiedMarketServiceIdentity($"service-token-{Calls.Count}", DateTimeOffset.UtcNow.AddMinutes(5)));
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
            var snapshot = new RequestSnapshot(request.Headers.Authorization?.ToString(),
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
