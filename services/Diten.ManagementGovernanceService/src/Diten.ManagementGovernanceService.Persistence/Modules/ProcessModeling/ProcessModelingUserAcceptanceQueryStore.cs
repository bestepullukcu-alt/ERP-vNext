using Diten.ManagementGovernanceService.Application.Features.ProcessModelingUserAcceptance;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

public sealed class ProcessModelingUserAcceptanceQueryStore(ProcessModelingMongoContext context) : IProcessModelUserAcceptanceQueries
{
    public async Task<IReadOnlyList<ProcessModelSummary>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty) return [];
        var rows = await context.Collection("mg_process_models").Find(Visible(tenantId))
            .Sort(new BsonDocument { ["ModelCode"] = 1, ["_id"] = 1 }).ToListAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task<ProcessModelSummary?> GetAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || id == Guid.Empty) return null;
        var row = await context.Collection("mg_process_models").Find(Builders<BsonDocument>.Filter.And(
            Visible(tenantId), Builders<BsonDocument>.Filter.Eq("_id", ProcessModelingMongoGuidPolicy.Write(id))))
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : Map(row);
    }

    private static FilterDefinition<BsonDocument> Visible(Guid tenantId) => Builders<BsonDocument>.Filter.And(
        Builders<BsonDocument>.Filter.Eq("TenantId", ProcessModelingMongoGuidPolicy.Write(tenantId)),
        Builders<BsonDocument>.Filter.Eq("IsDeleted", false));

    private static ProcessModelSummary Map(BsonDocument row) => new(
        ProcessModelingMongoGuidPolicy.Read(row["_id"]), ProcessModelingMongoGuidPolicy.Read(row["ProcessDefinitionId"]),
        row["ModelCode"].AsString, row["Name"].AsString,
        row.TryGetValue("Description", out var description) && !description.IsBsonNull ? description.AsString : null,
        row["LatestRevisionNumber"].AsInt32, NullableGuid(row, "PublishedVersionId"), NullableGuid(row, "OpenVersionId"), row["Version"].AsInt32);

    private static Guid? NullableGuid(BsonDocument row, string name) =>
        row.TryGetValue(name, out var value) && value.BsonType == BsonType.Binary ? ProcessModelingMongoGuidPolicy.Read(value) : null;
}
