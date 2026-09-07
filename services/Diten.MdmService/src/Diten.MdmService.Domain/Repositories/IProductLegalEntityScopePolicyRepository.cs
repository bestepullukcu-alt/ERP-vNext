using Diten.MdmService.Domain.Entities;

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

    Task<IReadOnlyList<Guid>> GetConfiguredGlobalProductIdsAsync(
        IReadOnlyCollection<Guid> globalProductIds,
        CancellationToken cancellationToken = default);
}

public sealed record ProductLegalEntityScopePolicyWriteResult(
    bool Succeeded,
    ProductLegalEntityScopePolicy? Policy,
    bool VersionConflict = false,
    bool WriteOutcomeAmbiguous = false);
