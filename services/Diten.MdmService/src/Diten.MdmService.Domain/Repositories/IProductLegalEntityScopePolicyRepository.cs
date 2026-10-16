using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductLegalEntityScopePolicyRepository
{
    Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(
        Guid globalProductId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// MOD-0290-FU04 S4 FIX1 (R4): the policies of many Global Products in one read (a batch reader evaluates the scope of
    /// every product it touches without a read per product). Missing = no policy.
    /// </summary>
    async Task<IReadOnlyDictionary<Guid, ProductLegalEntityScopePolicy>> GetByGlobalProductIdsAsync(
        IReadOnlyCollection<Guid> globalProductIds,
        CancellationToken cancellationToken = default)
    {
        var policies = new Dictionary<Guid, ProductLegalEntityScopePolicy>();
        foreach (var id in globalProductIds.Distinct())
            if (await GetByGlobalProductIdAsync(id, cancellationToken) is { } policy)
                policies[id] = policy;
        return policies;
    }

    Task<ProductLegalEntityScopePolicy?> GetByCreationCommandIdAsync(
        Guid creationCommandId,
        CancellationToken cancellationToken = default);

    Task<ProductLegalEntityScopePolicyWriteResult> CreateAsync(
        ProductLegalEntityScopePolicy policy,
        CancellationToken cancellationToken = default);

    Task<ProductLegalEntityScopePolicyWriteResult> UpdateAsync(
        ProductLegalEntityScopePolicy policy,
        int expectedVersion,
        CancellationToken cancellationToken = default);

    Task<ProductLegalEntityScopePolicyWriteResult> ReplaceAsync(
        ProductLegalEntityScopeVerifiedWriterAuthority authority,
        ProductLegalEntityScopeWriterLease lease,
        ProductLegalEntityScopePolicy requestedPolicy,
        int expectedVersion,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ProductLegalEntityScopePolicyWriteResult(
            false,
            null,
            VersionConflict: true));

    Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(
        IReadOnlyCollection<Guid> globalProductIds,
        CancellationToken cancellationToken = default);
}

public sealed record ProductLegalEntityScopePolicyWriteResult(
    bool Succeeded,
    ProductLegalEntityScopePolicy? Policy,
    bool VersionConflict = false,
    bool WriteOutcomeAmbiguous = false,
    bool VerifiedZeroMutation = false,
    bool LeaseRetentionRequired = false,
    bool TransientRetriesExhausted = false,
    // WP-MDM-BL598-01: the collection's UNIQUE indexes are not (yet) in place — nothing was written; a coded 503.
    bool IndexesNotReady = false);
