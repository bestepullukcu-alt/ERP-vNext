using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class FirstGskuIdentityWorkflowTenantPartitionDiscoveryRepository
    : IFirstGskuIdentityWorkflowTenantPartitionDiscovery
{
    private readonly IMongoCollection<FirstGskuIdentityWorkflowOperation> _operations;

    public FirstGskuIdentityWorkflowTenantPartitionDiscoveryRepository(IMongoDatabase database) =>
        _operations = database.GetCollection<FirstGskuIdentityWorkflowOperation>(
            FirstGskuIdentityWorkflowOperationRepository.CollectionName);

    public async Task<FirstGskuIdentityWorkflowTenantPartitionPage> DiscoverAsync(
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
            FirstGskuIdentityWorkflowCheckpoint.Completed,
            FirstGskuIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            FirstGskuIdentityWorkflowCheckpoint.ManualReconciliationRequired
        };
        var filter = Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.IsDeleted, false)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(
                         item => item.TemporalStorageVersion,
                         FirstGskuIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Nin(item => item.Checkpoint, terminal)
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.NextAttemptAtUtcTicksV1, null),
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.NextAttemptAtUtcTicksV1, nowUtcTicks))
                     & Builders<FirstGskuIdentityWorkflowOperation>.Filter.Or(
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Eq(item => item.LeaseOwner, null),
                         Builders<FirstGskuIdentityWorkflowOperation>.Filter.Lte(
                             item => item.LeaseUntilUtcTicksV1, nowUtcTicks));
        if (afterTenantId.HasValue)
        {
            filter &= Builders<FirstGskuIdentityWorkflowOperation>.Filter.Gt(
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
}
