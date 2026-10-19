using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Infrastructure.ReferenceData;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.MdmService.Application.Tests.ReferenceData;

/// <summary>
/// MOD-0290-FU04 S2 FIX1 item 1 — the approval worker's item-list client: MDM's service token for the tenant (no
/// HttpContext anywhere), one refresh on a 401, and its failure classes — Platform or Auth refusing MDM is a
/// configuration error with its own status; an outage stays an outage.
/// </summary>
public sealed class PlatformWorkflowItemReferenceResolverClientTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly ItemReferenceRequest[] Asked = [new(ItemReferenceSets.MaterialType, "RAW_MATERIAL")];

    [Fact]
    public async Task Fix1_The_worker_sends_the_service_token_for_its_tenant_with_the_resolver_credential()
    {
        var identities = new Identities();
        var platform = new Platform(_ => Answer(HttpStatusCode.OK, Selections()));

        var result = await Client(platform, identities).ResolveLatestAsync(Tenant, Asked);

        Assert.True(result.IsSuccessful);
        var sent = Assert.Single(platform.Requests);
        Assert.Equal("Bearer service-token-1", sent.Authorization);
        Assert.Equal("resolver-id", sent.CredentialId);
        Assert.Equal([(Tenant, false)], identities.Calls);
    }

    [Fact]
    public async Task Fix1_A_401_is_retried_once_with_a_refreshed_identity()
    {
        var identities = new Identities();
        var calls = 0;
        var platform = new Platform(_ => ++calls == 1 ? Answer(HttpStatusCode.Unauthorized, Refusal("REFERENCE_UNAUTHENTICATED"))
            : Answer(HttpStatusCode.OK, Selections()));

        var result = await Client(platform, identities).ResolveLatestAsync(Tenant, Asked);

        Assert.True(result.IsSuccessful);
        Assert.Equal(["Bearer service-token-1", "Bearer service-token-2"], platform.Requests.Select(x => x.Authorization));
        Assert.Equal([(Tenant, false), (Tenant, true)], identities.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, 401, ItemReferenceResolveResult.Unauthenticated, 2)]
    [InlineData(HttpStatusCode.Forbidden, 403, ItemReferenceResolveResult.Forbidden, 1)]
    public async Task Fix1_Platform_refusing_the_service_token_is_a_configuration_refusal(HttpStatusCode answer, int status,
        string code, int sends)
    {
        var platform = new Platform(_ => Answer(answer, Refusal(code)));

        var result = await Client(platform, new Identities()).ResolveLatestAsync(Tenant, Asked);

        Assert.Equal((false, status, code), (result.IsSuccessful, result.StatusCode, result.FailureCode));
        Assert.True(result.IsConfigurationRefusal);
        Assert.Equal(sends, platform.Requests.Count);
    }

    [Fact]
    public async Task Fix1_Auth_refusing_to_issue_the_identity_is_a_configuration_refusal_and_its_outage_is_an_outage()
    {
        var refused = await Client(new Platform(_ => throw new InvalidOperationException("not reached")),
            new Identities(new WorkflowVerifiedMarketServiceIdentityException("REFERENCE_WORKFLOW_IDENTITY_REJECTED", false)))
            .ResolveLatestAsync(Tenant, Asked);
        var down = await Client(new Platform(_ => throw new InvalidOperationException("not reached")),
            new Identities(new WorkflowVerifiedMarketServiceIdentityException("REFERENCE_WORKFLOW_IDENTITY_UNAVAILABLE", true)))
            .ResolveLatestAsync(Tenant, Asked);

        Assert.Equal((401, ItemReferenceResolveResult.ServiceIdentityRefused), (refused.StatusCode, refused.FailureCode));
        Assert.True(refused.IsConfigurationRefusal);
        Assert.Equal((503, "REFERENCE_PROVIDER_UNAVAILABLE"), (down.StatusCode, down.FailureCode));
        Assert.False(down.IsConfigurationRefusal);
    }

    [Fact]
    public async Task Fix1_A_Platform_outage_stays_an_outage()
    {
        var result = await Client(new Platform(_ => Answer(HttpStatusCode.ServiceUnavailable,
            Refusal("REFERENCE_PROVIDER_UNAVAILABLE"))), new Identities()).ResolveLatestAsync(Tenant, Asked);

        Assert.Equal((503, "REFERENCE_PROVIDER_UNAVAILABLE"), (result.StatusCode, result.FailureCode));
        Assert.False(result.IsConfigurationRefusal);
    }

    private static PlatformWorkflowItemReferenceResolverClient Client(Platform platform, Identities identities) =>
        new(new Factory(platform), identities, Options.Create(new VerifiedGskuResolverOptions
        {
            PlatformBaseAddress = new Uri("https://platform.test/"), Timeout = TimeSpan.FromSeconds(2),
            CredentialIdentifier = "resolver-id", CredentialSecret = "resolver-secret"
        }), NullLogger<PlatformWorkflowItemReferenceResolverClient>.Instance);

    private static JsonObject Selections() => new()
    {
        ["isSuccessful"] = true,
        ["reason_code"] = null,
        ["data"] = new JsonObject
        {
            ["selections"] = new JsonArray(new JsonObject
            {
                ["set_code"] = ItemReferenceSets.MaterialType, ["value_code"] = "RAW_MATERIAL",
                ["catalog_version"] = "ITEM-UNIVERSAL-V1", ["catalog_version_id"] = Guid.NewGuid().ToString("D"),
                ["catalog_version_number"] = 1, ["resolution_mode"] = "LATEST",
                ["resolved_at_utc"] = "2026-10-10T09:00:00+00:00", ["is_retired"] = false, ["selectable_for_new"] = true,
                ["attributes"] = new JsonObject()
            })
        }
    };

    private static JsonObject Refusal(string code) => new() { ["isSuccessful"] = false, ["reason_code"] = code, ["data"] = null };

    private static HttpResponseMessage Answer(HttpStatusCode status, JsonNode body) =>
        new(status) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };

    private sealed class Identities(Exception? failure = null) : IWorkflowVerifiedMarketServiceIdentityProvider
    {
        public List<(Guid Tenant, bool Refresh)> Calls { get; } = [];

        public Task<WorkflowVerifiedMarketServiceIdentity> GetAsync(Guid tenantId, bool forceRefresh,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((tenantId, forceRefresh));
            if (failure is not null) throw failure;
            return Task.FromResult(new WorkflowVerifiedMarketServiceIdentity("service-token-" + Calls.Count,
                DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }

    private sealed record Sent(string? Authorization, string? CredentialId);

    private sealed class Platform(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<Sent> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests.Add(new(request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues(PlatformItemReferenceResolverClient.CredentialIdHeader, out var id) ? id.Single() : null));
            return Task.FromResult(answer(request));
        }
    }

    private sealed class Factory(Platform platform) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(platform, disposeHandler: false);
    }
}
