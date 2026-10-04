using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductLegalEntityScopePolicyRepository
{
    Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(
        Guid globalProductId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The policies of several products in one read (ABB FIX2 A3: the Task Center projection evaluates a page of items
    /// without one read per product). Same rule as <see cref="GetByGlobalProductIdAsync"/>, product by product.
    /// </summary>
    async Task<IReadOnlyList<ProductLegalEntityScopePolicy>> GetByGlobalProductIdsAsync(
        IReadOnlyCollection<Guid> globalProductIds,
        CancellationToken cancellationToken = default)
    {
        var found = new List<ProductLegalEntityScopePolicy>();
        foreach (var id in globalProductIds)
        {
            if (await GetByGlobalProductIdAsync(id, cancellationToken) is { } policy)
            {
                found.Add(policy);
            }
        }

        return found;
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
    bool LeaseRetentionRequired = false);
