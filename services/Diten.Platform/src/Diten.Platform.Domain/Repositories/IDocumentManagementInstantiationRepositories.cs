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
    Task<long> ArchiveManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
    Task<long> ReactivateManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);
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
