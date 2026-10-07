using Diten.MdmService.Application.Contracts.ReferenceData;

namespace Diten.MdmService.Infrastructure.ReferenceData;

// MOD-0290-FU04 S1: Platform has no provider for the three item lists yet. Its verified resolver answers only the
// code-owned GSKU catalog (`pack-applicability`, `uom`), which this pack must not change; the item lists are the
// MOD-0048-FU01 named step "Universal Item Reference Lists for MOD-0290-FU04" (not authorized). Until that provider
// exists the item answers as an outage — fail-closed, never a hard-coded list (FU01: no cache, no fallback).
public sealed class UnavailableItemReferenceResolver : IItemReferenceResolver
{
    public const string ProviderNotAvailable = "ITEM_REFERENCE_PROVIDER_NOT_AVAILABLE";

    public Task<ItemReferenceResolveResult> ResolveLatestAsync(
        IReadOnlyList<ItemReferenceRequest> requests,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ItemReferenceResolveResult.Fail(503, ProviderNotAvailable));
}
