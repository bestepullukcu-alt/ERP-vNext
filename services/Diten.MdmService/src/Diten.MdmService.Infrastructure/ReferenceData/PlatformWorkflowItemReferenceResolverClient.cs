using System.Net.Http.Headers;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ItemMaster;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Infrastructure.ReferenceData;

/// <summary>
/// MOD-0290-FU04 S2 FIX1 item 1 — the item lists for the approval WORKER (no user request, no HttpContext). The
/// PlatformWorkflowVerifiedGskuResolverClient pattern: MDM's trusted service token for the tenant
/// (<see cref="IWorkflowVerifiedMarketServiceIdentityProvider"/>, audience TRUSTED_REFERENCE_DATA_CONSUMER, which Platform's
/// item-universal endpoint accepts beside a tenant JWT), the same resolver credential headers, and ONE refresh on a 401.
/// The send and the trust rule are the user-path client's own (<see cref="PlatformItemReferenceResolverClient.SendAsync"/>).
///
/// <para>Failure classes: Platform refusing the service token (401 after the refresh, 403) or Auth refusing to issue it is a
/// CONFIGURATION error with its own status (401 / 403) — the item rule makes it stuck, never a wait. An outage (5xx, a
/// timeout, Auth unreachable) stays an outage (503) and waits.</para>
/// </summary>
public sealed class PlatformWorkflowItemReferenceResolverClient : IWorkflowItemReferenceResolver
{
    private readonly IHttpClientFactory _clients;
    private readonly IWorkflowVerifiedMarketServiceIdentityProvider _identities;
    private readonly VerifiedGskuResolverOptions _options;
    private readonly ILogger<PlatformWorkflowItemReferenceResolverClient> _logger;
    private readonly bool _development;

    public PlatformWorkflowItemReferenceResolverClient(IHttpClientFactory clients,
        IWorkflowVerifiedMarketServiceIdentityProvider identities, IOptions<VerifiedGskuResolverOptions> options,
        ILogger<PlatformWorkflowItemReferenceResolverClient> logger, IHostEnvironment? environment = null)
    {
        _clients = clients;
        _identities = identities;
        _options = options.Value;
        _logger = logger;
        // No environment known: the strict rule (https only), as the user-path client.
        _development = environment?.IsDevelopment() == true;
    }

    public async Task<ItemReferenceResolveResult> ResolveLatestAsync(Guid tenantId,
        IReadOnlyList<ItemReferenceRequest> requests, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || !PlatformItemReferenceResolverClient.IsConfigured(_options, _development))
        {
            _logger.LogWarning(ItemReferenceProvider.NotConfiguredWarning);
            return ItemReferenceResolveResult.Fail(503, ItemReferenceResolveResult.ConfigurationInvalid);
        }
        var wanted = (requests ?? []).Where(request => request is not null).Distinct().ToList();
        if (wanted.Count is 0 or > PlatformItemReferenceResolverClient.MaximumSelections)
            return ItemReferenceResolveResult.Fail(503, "REFERENCE_BATCH_LIMIT_EXCEEDED");

        var client = _clients.CreateClient(nameof(PlatformWorkflowItemReferenceResolverClient));
        try
        {
            var identity = await _identities.GetAsync(tenantId, false, cancellationToken);
            var first = await PlatformItemReferenceResolverClient.SendAsync(client, _options, Bearer(identity), wanted,
                _logger, cancellationToken);
            if (first.RawStatus != 401) return Classified(first);

            // One refresh: a token Auth has since rotated is not a configuration error.
            identity = await _identities.GetAsync(tenantId, true, cancellationToken);
            return Classified(await PlatformItemReferenceResolverClient.SendAsync(client, _options, Bearer(identity), wanted,
                _logger, cancellationToken));
        }
        catch (WorkflowVerifiedMarketServiceIdentityException exception) when (exception.IsRetryable)
        {
            return ItemReferenceResolveResult.Fail(503, exception.ErrorCode.EndsWith("TIMEOUT", StringComparison.Ordinal)
                ? "REFERENCE_PROVIDER_TIMEOUT" : "REFERENCE_PROVIDER_UNAVAILABLE");
        }
        catch (WorkflowVerifiedMarketServiceIdentityException)
        {
            // Auth will not issue MDM's identity for this tenant: configuration, by name.
            return ItemReferenceResolveResult.Fail(401, ItemReferenceResolveResult.ServiceIdentityRefused);
        }
    }

    // Platform's refusal of the service token keeps its status (the user path folds every failure into 503).
    private static ItemReferenceResolveResult Classified((int RawStatus, ItemReferenceResolveResult Result) answer) =>
        answer.RawStatus switch
        {
            401 => ItemReferenceResolveResult.Fail(401, ItemReferenceResolveResult.Unauthenticated),
            403 => ItemReferenceResolveResult.Fail(403, ItemReferenceResolveResult.Forbidden),
            _ => answer.Result
        };

    private static AuthenticationHeaderValue Bearer(WorkflowVerifiedMarketServiceIdentity identity) =>
        new("Bearer", identity.AccessToken);
}
