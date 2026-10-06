using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GlobalProductIdentityWorkflowTenantPartitionDiscoveryRepository
    : IGlobalProductIdentityWorkflowTenantPartitionDiscovery
{
    private readonly IMongoCollection<GlobalProductIdentityWorkflowOperation> _operations;

    public GlobalProductIdentityWorkflowTenantPartitionDiscoveryRepository(IMongoDatabase database) =>
        _operations = database.GetCollection<GlobalProductIdentityWorkflowOperation>(
            GlobalProductIdentityWorkflowOperationRepository.CollectionName);

    public async Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        long nowUtcTicks,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (afterTenantId == Guid.Empty || nowUtcTicks <= 0 || limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var terminal = new[]
        {
            GlobalProductIdentityWorkflowCheckpoint.Completed,
            GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            GlobalProductIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
            GlobalProductIdentityWorkflowCheckpoint.Superseded
        };
        var filter = Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.IsDeleted, false)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         GlobalProductIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Nin(item => item.Checkpoint, terminal)
                     // AwaitingMakerReplay is the maker's to continue — EXCEPT a withdrawal an earlier version
                     // parked there: its approval exists at the engine, so the worker must keep reading it.
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Ne(
                             item => item.Checkpoint, GlobalProductIdentityWorkflowCheckpoint.AwaitingMakerReplay),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Ne(
                             item => item.WithdrawalCommandId, null))
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Or(
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<GlobalProductIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (afterTenantId.HasValue)
        {
            filter &= Builders<GlobalProductIdentityWorkflowOperation>.Filter.Gt(
                item => item.TenantId, afterTenantId.Value);
        }

        // Stream a bounded number of due rows in tenant order instead of grouping the entire
        // cross-tenant collection. A page may contain fewer tenants than requested when one tenant
        // owns many due operations; the tenant cursor still advances monotonically and the tenant-
        // scoped repository processes every operation for that partition.
        var candidates = await _operations.Find(filter)
            .SortBy(item => item.TenantId)
            .ThenBy(item => item.NextAttemptAtUtcTicksV1)
            .ThenBy(item => item.OperationId)
            .Project(item => item.TenantId)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var page = candidates.Distinct().ToArray();
        return new(page, candidates.Count == limit ? candidates[^1] : null);
    }

    // BL-552 flow 3: tenants holding a submit stuck for a person (manual reconciliation), in tenant order — the same
    // bounded stream as above; the counts themselves are made by the tenant-scoped repository.
    public async Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverStuckAsync(
        Guid? afterTenantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (afterTenantId == Guid.Empty || limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
        var filter = Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(item => item.IsDeleted, false)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.Checkpoint, GlobalProductIdentityWorkflowCheckpoint.ManualReconciliationRequired)
                     & Builders<GlobalProductIdentityWorkflowOperation>.Filter.Eq(
                         item => item.RecoveryDisposition, ProductIdentityWorkflowRecoveryDisposition.ManualReconciliationRequired);
        if (afterTenantId.HasValue)
        {
            filter &= Builders<GlobalProductIdentityWorkflowOperation>.Filter.Gt(item => item.TenantId, afterTenantId.Value);
        }
        var candidates = await _operations.Find(filter)
            .SortBy(item => item.TenantId)
            .Project(item => item.TenantId)
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var page = candidates.Distinct().ToArray();
        return new(page, candidates.Count == limit ? candidates[^1] : null);
    }
}
