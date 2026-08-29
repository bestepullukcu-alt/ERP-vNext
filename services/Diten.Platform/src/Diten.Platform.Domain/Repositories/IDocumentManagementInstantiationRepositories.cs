using Diten.Platform.Domain.Entities.DocumentManagement;

namespace Diten.Platform.Domain.Repositories;

public interface ICollectionInstanceRepository
{
    Task<CollectionInstance> CreateAsync(CollectionInstance instance, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionInstance>> CreateManyAsync(IReadOnlyList<CollectionInstance> instances, CancellationToken ct = default);
    Task<CollectionInstance?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CollectionInstance?> GetByInstanceKeyAsync(string instanceKey, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionInstance>> GetAllForTenantAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CollectionInstance>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionInstance>> GetByBaselineAndCompanyAsync(Guid baselineReleaseId, Guid companyId, string? instanceToken, CancellationToken ct = default);
    Task<IReadOnlyList<CollectionInstance>> GetCorporateAsync(
        Guid? baselineReleaseId,
        Guid? corporateOwnerId,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<CollectionInstance>>([]);
    Task<IReadOnlyList<CollectionInstance>> CreateCorporateTreeIfAbsentAsync(
        Guid baselineReleaseId,
        Guid corporateOwnerId,
        IReadOnlyList<CollectionInstance> instances,
        CancellationToken ct = default) =>
        Task.FromResult(instances);
    Task<long> ArchiveManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
    Task<long> ReactivateManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
}

public interface ICorporateCollectionProvisioningOperationRepository
{
    Task<CorporateCollectionInstanceProvisioningOperation> CreateOrGetAsync(
        CorporateCollectionInstanceProvisioningOperation operation,
        CancellationToken ct = default);
    Task<CorporateCollectionInstanceProvisioningOperation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CorporateCollectionInstanceProvisioningOperation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default);
    Task<bool> UpdateAsync(CorporateCollectionInstanceProvisioningOperation operation, CancellationToken ct = default);
}

public interface IInstantiationOperationRepository
{
    Task<InstantiationOperation> CreateAsync(InstantiationOperation operation, CancellationToken ct = default);
    Task<InstantiationOperation?> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default);
}

public interface IInstantiationOutcomeRepository
{
    Task<IReadOnlyList<InstantiationOutcome>> CreateManyAsync(IReadOnlyList<InstantiationOutcome> outcomes, CancellationToken ct = default);
    Task<IReadOnlyList<InstantiationOutcome>> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default);
    Task<IReadOnlyList<InstantiationOutcome>> GetRetryableFailedByOperationIdAsync(Guid operationId, CancellationToken ct = default);
}
