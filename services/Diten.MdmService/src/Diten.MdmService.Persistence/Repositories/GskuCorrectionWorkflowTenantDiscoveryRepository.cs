using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence.Repositories;

public sealed class GskuCorrectionWorkflowTenantDiscoveryRepository(IMongoDatabase database)
    : IGskuCorrectionWorkflowTenantDiscoveryRepository
{
    private readonly IMongoCollection<GskuCorrectionWorkflowOperation> rows =
        database.GetCollection<GskuCorrectionWorkflowOperation>(GskuCorrectionWorkflowOperationRepository.CollectionName);
    public async Task<GlobalProductIdentityWorkflowTenantPartitionPage> DiscoverAsync(Guid? afterTenantId,
        int limit, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        var filter = Builders<GskuCorrectionWorkflowOperation>.Filter.Eq(x => x.IsDeleted, false);
        if (afterTenantId.HasValue) filter &= Builders<GskuCorrectionWorkflowOperation>.Filter.Gt(x => x.TenantId, afterTenantId);
        var result = await rows.Aggregate().Match(filter).AppendStage<BsonDocument>(new BsonDocument("$group",
                new BsonDocument("_id", $"${nameof(GskuCorrectionWorkflowOperation.TenantId)}")))
            .Sort(new BsonDocumentSortDefinition<BsonDocument>(new BsonDocument("_id", 1))).Limit(limit)
            .ToListAsync(cancellationToken);
        var ids = result.Select(x => x["_id"].AsGuid).ToArray();
        return new(ids, ids.Length == limit ? ids[^1] : null);
    }
}
