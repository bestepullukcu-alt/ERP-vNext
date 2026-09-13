using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Domain.Repositories;

public interface IProductLegalEntityScopePolicyRepository
{
    Task<ProductLegalEntityScopePolicy?> GetByGlobalProductIdAsync(
        Guid globalProductId,
        CancellationToken cancellationToken = default);

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
