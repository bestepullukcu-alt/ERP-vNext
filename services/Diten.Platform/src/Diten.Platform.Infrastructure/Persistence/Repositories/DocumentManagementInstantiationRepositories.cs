using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class CollectionInstanceRepository : TenantRepository<CollectionInstance>, ICollectionInstanceRepository
{
    public CollectionInstanceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementCollectionInstances)
    {
    }

    public async Task<IReadOnlyList<CollectionInstance>> CreateManyAsync(IReadOnlyList<CollectionInstance> instances, CancellationToken ct = default)
    {
        if (instances.Count == 0)
        {
            return [];
        }

        foreach (var instance in instances)
        {
            typeof(CollectionInstance).GetProperty(nameof(CollectionInstance.TenantId))?.SetValue(instance, TenantContext.TenantId);
        }

        await Collection.InsertManyAsync(instances, cancellationToken: ct);
        return instances;
    }

    public Task<CollectionInstance?> GetByInstanceKeyAsync(string instanceKey, CancellationToken ct = default)
    {
        var filter = Builders<CollectionInstance>.Filter.And(
            ExecutionFilter,
            Builders<CollectionInstance>.Filter.Eq(x => x.InstanceKey, instanceKey));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<IReadOnlyList<CollectionInstance>> GetAllForTenantAsync(CancellationToken ct = default)
    {
        return await Collection.Find(ExecutionFilter).SortBy(x => x.CompanyId).ThenBy(x => x.FullPath).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CollectionInstance>> GetByCompanyAsync(Guid companyId, CancellationToken ct = default)
    {
        var filter = Builders<CollectionInstance>.Filter.And(
            ExecutionFilter,
            Builders<CollectionInstance>.Filter.Eq(x => x.CompanyId, companyId));
        return await Collection.Find(filter).SortBy(x => x.FullPath).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CollectionInstance>> GetByBaselineAndCompanyAsync(
        Guid baselineReleaseId,
        Guid companyId,
        string? instanceToken,
        CancellationToken ct = default)
    {
        var filter = Builders<CollectionInstance>.Filter.And(
            ExecutionFilter,
            Builders<CollectionInstance>.Filter.Eq(x => x.BaselineReleaseId, baselineReleaseId),
            Builders<CollectionInstance>.Filter.Eq(x => x.CompanyId, companyId));
        if (!string.IsNullOrWhiteSpace(instanceToken))
        {
            filter &= Builders<CollectionInstance>.Filter.Eq(x => x.InstanceToken, instanceToken.Trim());
        }

        return await Collection.Find(filter).SortBy(x => x.FullPath).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CollectionInstance>> GetCorporateAsync(
        Guid? baselineReleaseId,
        Guid? corporateOwnerId,
        CancellationToken ct = default)
    {
        var filter = Builders<CollectionInstance>.Filter.And(
            ExecutionFilter,
            Builders<CollectionInstance>.Filter.Eq(x => x.CollectionScopeType, CollectionScopeType.Corporate));
        if (baselineReleaseId is { } baseline)
        {
            filter &= Builders<CollectionInstance>.Filter.Eq(x => x.BaselineReleaseId, baseline);
        }
        if (corporateOwnerId is { } owner)
        {
            filter &= Builders<CollectionInstance>.Filter.Eq(x => x.ScopeOwnerId, owner);
        }

        return await Collection.Find(filter).SortBy(x => x.FullPath).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CollectionInstance>> CreateCorporateTreeIfAbsentAsync(
        Guid baselineReleaseId,
        Guid corporateOwnerId,
        IReadOnlyList<CollectionInstance> instances,
        CancellationToken ct = default)
    {
        foreach (var instance in instances)
        {
            typeof(CollectionInstance).GetProperty(nameof(CollectionInstance.TenantId))?.SetValue(instance, TenantContext.TenantId);
            var filter = Builders<CollectionInstance>.Filter.And(
                ExecutionFilter,
                Builders<CollectionInstance>.Filter.Eq(x => x.CollectionScopeType, CollectionScopeType.Corporate),
                Builders<CollectionInstance>.Filter.Eq(x => x.ScopeOwnerId, corporateOwnerId),
                Builders<CollectionInstance>.Filter.Eq(x => x.BaselineReleaseId, baselineReleaseId),
                Builders<CollectionInstance>.Filter.Eq(x => x.CanonicalId, instance.CanonicalId),
                Builders<CollectionInstance>.Filter.Ne(x => x.InstanceStatus, CollectionInstanceStatus.Archived));
            await Collection.ReplaceOneAsync(filter, instance, new ReplaceOptions { IsUpsert = true }, ct);
        }

        return await GetCorporateAsync(baselineReleaseId, corporateOwnerId, ct);
    }

    // Soft archive (no hard delete): flips InstanceStatus to Archived for the given ids, tenant-scoped.
    public async Task<long> ArchiveManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        var filter = Builders<CollectionInstance>.Filter.And(
            ExecutionFilter,
            Builders<CollectionInstance>.Filter.In(x => x.Id, ids));
        var update = Builders<CollectionInstance>.Update
            .Set(x => x.InstanceStatus, CollectionInstanceStatus.Archived)
            .Set(x => x.LastChangeAt, DateTimeOffset.UtcNow)
            .Inc(x => x.VersionToken, 1);
        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount;
    }

    // Restore (un-archive): flips InstanceStatus back to Active for the given ids, tenant-scoped.
    public async Task<long> ReactivateManyAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
        {
            return 0;
        }

        var filter = Builders<CollectionInstance>.Filter.And(
            ExecutionFilter,
            Builders<CollectionInstance>.Filter.In(x => x.Id, ids));
        var update = Builders<CollectionInstance>.Update
            .Set(x => x.InstanceStatus, CollectionInstanceStatus.Active)
            .Set(x => x.LastChangeAt, DateTimeOffset.UtcNow)
            .Inc(x => x.VersionToken, 1);
        var result = await Collection.UpdateManyAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount;
    }
}

public sealed class CorporateCollectionProvisioningOperationRepository
    : TenantRepository<CorporateCollectionInstanceProvisioningOperation>, ICorporateCollectionProvisioningOperationRepository
{
    public CorporateCollectionProvisioningOperationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementCorporateCollectionProvisioningOperations)
    {
    }

    public async Task<CorporateCollectionInstanceProvisioningOperation> CreateOrGetAsync(
        CorporateCollectionInstanceProvisioningOperation operation,
        CancellationToken ct = default)
    {
        typeof(CorporateCollectionInstanceProvisioningOperation)
            .GetProperty(nameof(CorporateCollectionInstanceProvisioningOperation.TenantId))
            ?.SetValue(operation, TenantContext.TenantId);
        var filter = Builders<CorporateCollectionInstanceProvisioningOperation>.Filter.And(
            ExecutionFilter,
            Builders<CorporateCollectionInstanceProvisioningOperation>.Filter.Eq(x => x.IdempotencyKey, operation.IdempotencyKey));
        var update = Builders<CorporateCollectionInstanceProvisioningOperation>.Update
            .SetOnInsert(x => x.Id, operation.Id)
            .SetOnInsert(x => x.TenantId, TenantContext.TenantId)
            .SetOnInsert(x => x.IdempotencyKey, operation.IdempotencyKey)
            .SetOnInsert(x => x.BaselineReleaseId, operation.BaselineReleaseId)
            .SetOnInsert(x => x.CorporateOwnerId, operation.CorporateOwnerId)
            .SetOnInsert(x => x.ScopeType, CollectionScopeType.Corporate)
            .SetOnInsert(x => x.ScopeOwnerId, operation.ScopeOwnerId)
            .SetOnInsert(x => x.Status, CorporateCollectionProvisioningStatus.Pending)
            .SetOnInsert(x => x.AttemptCount, operation.AttemptCount)
            .SetOnInsert(x => x.LastAttemptAt, operation.LastAttemptAt)
            .SetOnInsert(x => x.CorrelationId, operation.CorrelationId)
            .SetOnInsert(x => x.DisplayName, operation.DisplayName)
            .SetOnInsert(x => x.Description, operation.Description)
            .SetOnInsert(x => x.IsDeleted, false);
        return await Collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<CorporateCollectionInstanceProvisioningOperation>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            },
            ct);
    }

    public Task<CorporateCollectionInstanceProvisioningOperation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<CorporateCollectionInstanceProvisioningOperation>.Filter.And(
            ExecutionFilter,
            Builders<CorporateCollectionInstanceProvisioningOperation>.Filter.Eq(x => x.Id, id));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public Task<CorporateCollectionInstanceProvisioningOperation?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken ct = default)
    {
        var filter = Builders<CorporateCollectionInstanceProvisioningOperation>.Filter.And(
            ExecutionFilter,
            Builders<CorporateCollectionInstanceProvisioningOperation>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }

    public async Task<bool> UpdateAsync(CorporateCollectionInstanceProvisioningOperation operation, CancellationToken ct = default)
    {
        var filter = Builders<CorporateCollectionInstanceProvisioningOperation>.Filter.And(
            ExecutionFilter,
            Builders<CorporateCollectionInstanceProvisioningOperation>.Filter.Eq(x => x.Id, operation.Id));
        var result = await Collection.ReplaceOneAsync(filter, operation, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }
}

public sealed class InstantiationOperationRepository : TenantRepository<InstantiationOperation>, IInstantiationOperationRepository
{
    public InstantiationOperationRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementInstantiationOperations)
    {
    }

    public Task<InstantiationOperation?> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default)
    {
        var filter = Builders<InstantiationOperation>.Filter.And(
            ExecutionFilter,
            Builders<InstantiationOperation>.Filter.Eq(x => x.OperationId, operationId));
        return Collection.Find(filter).FirstOrDefaultAsync(ct)!;
    }
}

public sealed class InstantiationOutcomeRepository : TenantRepository<InstantiationOutcome>, IInstantiationOutcomeRepository
{
    public InstantiationOutcomeRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.DocumentManagementInstantiationOutcomes)
    {
    }

    public async Task<IReadOnlyList<InstantiationOutcome>> CreateManyAsync(IReadOnlyList<InstantiationOutcome> outcomes, CancellationToken ct = default)
    {
        if (outcomes.Count == 0)
        {
            return [];
        }

        foreach (var outcome in outcomes)
        {
            typeof(InstantiationOutcome).GetProperty(nameof(InstantiationOutcome.TenantId))?.SetValue(outcome, TenantContext.TenantId);
        }

        await Collection.InsertManyAsync(outcomes, cancellationToken: ct);
        return outcomes;
    }

    public async Task<IReadOnlyList<InstantiationOutcome>> GetByOperationIdAsync(Guid operationId, CancellationToken ct = default)
    {
        var filter = Builders<InstantiationOutcome>.Filter.And(
            ExecutionFilter,
            Builders<InstantiationOutcome>.Filter.Eq(x => x.OperationId, operationId));
        return await Collection.Find(filter).SortBy(x => x.NodeKey).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<InstantiationOutcome>> GetRetryableFailedByOperationIdAsync(Guid operationId, CancellationToken ct = default)
    {
        var filter = Builders<InstantiationOutcome>.Filter.And(
            ExecutionFilter,
            Builders<InstantiationOutcome>.Filter.Eq(x => x.OperationId, operationId),
            Builders<InstantiationOutcome>.Filter.Eq(x => x.Status, InstantiationOutcomeStatus.Failed),
            Builders<InstantiationOutcome>.Filter.Eq(x => x.Retryable, true));
        return await Collection.Find(filter).SortBy(x => x.NodeKey).ToListAsync(ct);
    }
}
