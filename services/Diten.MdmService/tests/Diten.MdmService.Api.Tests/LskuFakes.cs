using System.Collections.Concurrent;
using Diten.MdmService.Application.Contracts.ReferenceData;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-LSKU-ACCEPT-01 FIX1: Platform's verified market reference data, for the LSKU create and approval-time checks
/// (the two MDM resolvers are clients of another service; this is that service's answer). Every two-letter upper-case
/// market resolves, version 1, unless a test retires it (404 REFERENCE_MARKET_NOT_FOUND, the real client's answer).
/// </summary>
public sealed class FakeMarkets : IVerifiedMarketReferenceResolver, IWorkflowVerifiedMarketReferenceResolver
{
    public static readonly DateTimeOffset ResolvedAt = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid CatalogVersion = Guid.Parse("4c9a2b1e-0f5d-4b8e-9a77-3d2f1e6c5b40");

    /// <summary>Markets the provider answers "not found" for (retired since the LSKU was created).</summary>
    public ConcurrentDictionary<string, bool> Retired { get; } = new(StringComparer.Ordinal);

    public Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(string marketCode,
        CancellationToken cancellationToken = default) => Task.FromResult(Resolve(marketCode));

    public Task<VerifiedMarketReferenceResolveResult> ResolveLatestAsync(Guid tenantId, string marketCode,
        CancellationToken cancellationToken = default) => Task.FromResult(Resolve(marketCode));

    private VerifiedMarketReferenceResolveResult Resolve(string marketCode) =>
        Retired.ContainsKey(marketCode)
            ? new(false, 404, "REFERENCE_MARKET_NOT_FOUND", null)
            : new(true, 200, null, new("market", marketCode, CatalogVersion, 1, "LATEST", ResolvedAt));
}
