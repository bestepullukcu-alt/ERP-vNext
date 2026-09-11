using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class FinishedGoodIdentityWorkflowTenantPartitionDiscoveryRepository
    : IFinishedGoodIdentityWorkflowTenantPartitionDiscovery
{
    private readonly IMongoCollection<BsonDocument> _operations;

    public FinishedGoodIdentityWorkflowTenantPartitionDiscoveryRepository(IMongoDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _operations = database.GetCollection<BsonDocument>(
            FinishedGoodIdentityWorkflowOperationRepository.CollectionName);
    }

    public async Task<FinishedGoodIdentityWorkflowTenantPartitionPage> DiscoverAsync(
        Guid? afterTenantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (afterTenantId == Guid.Empty || limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        var terminal = new[]
        {
            FinishedGoodIdentityWorkflowCheckpoint.Completed,
            FinishedGoodIdentityWorkflowCheckpoint.AwaitingMakerReplay,
            FinishedGoodIdentityWorkflowCheckpoint.ManualReconciliationRequired,
            FinishedGoodIdentityWorkflowCheckpoint.AbandonedBeforeWorkflowStart,
            FinishedGoodIdentityWorkflowCheckpoint.Superseded
        };
        var filter = Builders<BsonDocument>.Filter.Eq(
                         nameof(FinishedGoodIdentityWorkflowOperation.IsDeleted), false)
                     & Builders<BsonDocument>.Filter.Eq(
                         nameof(FinishedGoodIdentityWorkflowOperation.TemporalStorageVersion),
                         FinishedGoodIdentityWorkflowOperation.CurrentTemporalStorageVersion)
                     & new BsonDocumentFilterDefinition<BsonDocument>(new BsonDocument(
                         nameof(FinishedGoodIdentityWorkflowOperation.Checkpoint),
                         new BsonDocument("$nin", new BsonArray(terminal.Select(
                             checkpoint => new BsonInt32((int)checkpoint))))))
                     & FinishedGoodIdentityWorkflowOperationRepository.ServerEvaluatedRecoveryEligibilityFilter();
        if (afterTenantId.HasValue)
        {
            filter &= Builders<BsonDocument>.Filter.Gt(
                nameof(FinishedGoodIdentityWorkflowOperation.TenantId),
                new BsonBinaryData(afterTenantId.Value, GuidRepresentation.Standard));
        }

        // Candidate operations are bounded before materialization so malformed or stale admission snapshots
        // fail closed rather than yielding a tenant partition. Operation contents are never returned.
        var candidates = await _operations.Find(filter)
            .Sort(Builders<BsonDocument>.Sort
                .Ascending(nameof(FinishedGoodIdentityWorkflowOperation.TenantId))
                .Ascending(nameof(FinishedGoodIdentityWorkflowOperation.NextAttemptAtUtcTicksV1))
                .Ascending(nameof(FinishedGoodIdentityWorkflowOperation.OperationId)))
            .Limit(limit)
            .ToListAsync(cancellationToken);
        var tenantIds = candidates
            .Select(FinishedGoodIdentityWorkflowOperationRepository.DeserializeAndValidate)
            .Select(item => item.TenantId)
            .Distinct()
            .ToArray();
        return new(
            tenantIds,
            candidates.Count == limit ? tenantIds[^1] : null);
    }
}
