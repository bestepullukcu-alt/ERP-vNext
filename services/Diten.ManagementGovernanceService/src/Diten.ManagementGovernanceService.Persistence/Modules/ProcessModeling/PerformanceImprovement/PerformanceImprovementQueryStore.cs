using Diten.ManagementGovernanceService.Application.Modules.ProcessModeling.PerformanceImprovement;
using Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling.PerformanceImprovement;

public sealed class PerformanceImprovementQueryStore(PerformanceImprovementMongoPersistence.Context context)
    : PerformanceImprovementContract.IQueryStore
{
    public async Task<IReadOnlyList<PerformanceImprovementContract.OpportunityDto>> ListAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) return [];
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("TenantId", G(tenantId)),
            Builders<BsonDocument>.Filter.Eq("IsDeleted", false));
        var rows = await context.Collection(PerformanceImprovementMongoPersistence.Opportunities).Find(filter)
            .Sort(new BsonDocument { ["PriorityRank"] = 1, ["CreatedAtUtc"] = -1, ["_id"] = 1 })
            .ToListAsync(cancellationToken);
        var result = new List<PerformanceImprovementContract.OpportunityDto>(rows.Count);
        foreach (var row in rows) result.Add(await MapAsync(tenantId, row, cancellationToken));
        return result;
    }

    public async Task<PerformanceImprovementContract.OpportunityDto?> GetAsync(
        Guid tenantId, Guid opportunityId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty || opportunityId == Guid.Empty) return null;
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("TenantId", G(tenantId)),
            Builders<BsonDocument>.Filter.Eq("_id", G(opportunityId)),
            Builders<BsonDocument>.Filter.Eq("IsDeleted", false));
        var row = await context.Collection(PerformanceImprovementMongoPersistence.Opportunities)
            .Find(filter).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : await MapAsync(tenantId, row, cancellationToken);
    }

    private async Task<PerformanceImprovementContract.OpportunityDto> MapAsync(
        Guid tenantId, BsonDocument row, CancellationToken cancellationToken)
    {
        var id = ProcessModelingMongoGuidPolicy.Read(row["_id"]);
        var outcomes = await context.Collection(PerformanceImprovementMongoPersistence.Outcomes).Find(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("TenantId", G(tenantId)),
                Builders<BsonDocument>.Filter.Eq("ProcessImprovementOpportunityId", G(id)),
                Builders<BsonDocument>.Filter.Eq("IsDeleted", false)))
            .Sort(new BsonDocument { ["ObservedAtUtc"] = 1, ["_id"] = 1 })
            .ToListAsync(cancellationToken);
        return new(id, ProcessModelingMongoGuidPolicy.Read(row["ProcessModelVersionId"]), row["Title"].AsString,
            row["ProblemStatement"].AsString, NullableString(row, "ExpectedOutcome"), NullableInt(row, "PriorityRank"),
            row["LifecycleState"].AsString, row["Version"].AsInt32, Date(row["CreatedAtUtc"]),
            !row.TryGetValue("UpdatedAtUtc", out var updated) || updated.IsBsonNull ? null : Date(updated),
            outcomes.Select(x => new PerformanceImprovementContract.OutcomeDto(
                ProcessModelingMongoGuidPolicy.Read(x["_id"]), x["Summary"].AsString, Date(x["ObservedAtUtc"]))).ToArray());
    }

    private static MongoDB.Bson.BsonBinaryData G(Guid value) => ProcessModelingMongoGuidPolicy.Write(value);
    private static DateTime Date(BsonValue value) => value.AsBsonDateTime.ToUniversalTime();
    private static string? NullableString(BsonDocument row, string name) =>
        !row.TryGetValue(name, out var value) || value.IsBsonNull ? null : value.AsString;
    private static int? NullableInt(BsonDocument row, string name) =>
        !row.TryGetValue(name, out var value) || value.IsBsonNull ? null : value.AsInt32;
}
