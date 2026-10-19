using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence;

/// <summary>
/// WP-MVP1-FU-CLEAN1 (FU-EVIDENCE-AUDIT-NAMES) — the ONE <see cref="IAuditActorNames"/> of the service (it replaced the Expiry and the Recall
/// copies of WP-MVP1-FIX-LIVE2), over the central audit queue (<see cref="CentralAuditOutboxRecord.CollectionName"/>), read as BSON (the
/// queue's own class map stores ids as strings): tenant = the caller's, correlation id and actor id both in the asked pairs; a row counts
/// only for the pair it names. The first non-empty <c>ActorDisplayName</c> of a pair wins. Read only.
/// </summary>
public sealed class CentralAuditActorNames : IAuditActorNames
{
    private readonly IMongoCollection<BsonDocument> _outbox;
    private readonly ITenantContext _tenant;

    public CentralAuditActorNames(IMongoDatabase database, ITenantContext tenant)
    {
        _outbox = database.GetCollection<BsonDocument>(InventoryCollections.CentralAuditOutbox);
        _tenant = tenant;
    }

    public async Task<IReadOnlyDictionary<(Guid CorrelationId, Guid ActorId), string>> NamesAsync(
        IReadOnlyCollection<(Guid CorrelationId, Guid ActorId)> keys, CancellationToken cancellationToken = default)
    {
        var names = new Dictionary<(Guid, Guid), string>();
        if (keys.Count == 0 || !_tenant.IsResolved) return names;
        var asked = keys.ToHashSet();
        var f = Builders<BsonDocument>.Filter;
        var filter = f.Eq(nameof(CentralAuditOutboxRecord.TenantId), _tenant.TenantId.ToString("D"))
                     & f.In(nameof(CentralAuditOutboxRecord.CorrelationId), asked.Select(k => k.CorrelationId.ToString("D")).Distinct())
                     & f.In(nameof(CentralAuditOutboxRecord.ActorId), asked.Select(k => k.ActorId.ToString("D")).Distinct())
                     & f.Type(nameof(CentralAuditOutboxRecord.ActorDisplayName), BsonType.String);
        var rows = await _outbox.Find(filter)
            .Project(Builders<BsonDocument>.Projection.Include(nameof(CentralAuditOutboxRecord.CorrelationId))
                .Include(nameof(CentralAuditOutboxRecord.ActorId)).Include(nameof(CentralAuditOutboxRecord.ActorDisplayName)))
            .ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            if (!Guid.TryParse(row.GetValue(nameof(CentralAuditOutboxRecord.CorrelationId), BsonNull.Value).ToString(), out var correlation)
                || !Guid.TryParse(row.GetValue(nameof(CentralAuditOutboxRecord.ActorId), BsonNull.Value).ToString(), out var actor)
                || !asked.Contains((correlation, actor)))
            {
                continue;
            }

            var name = row.GetValue(nameof(CentralAuditOutboxRecord.ActorDisplayName), BsonNull.Value);
            if (name.IsString && !string.IsNullOrWhiteSpace(name.AsString)) names.TryAdd((correlation, actor), name.AsString.Trim());
        }

        return names;
    }
}
