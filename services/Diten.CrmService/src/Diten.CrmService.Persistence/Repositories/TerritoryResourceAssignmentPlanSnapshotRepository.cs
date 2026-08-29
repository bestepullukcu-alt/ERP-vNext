using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0151 FU04B plan baseline reads. Read-only by design: the single write path is the activation unit of work,
/// so there is deliberately no Update/Delete member here (pack §22.4 D-FU04B-2 immutability).
/// </summary>
public sealed class TerritoryResourceAssignmentPlanSnapshotRepository
    : ITerritoryResourceAssignmentPlanSnapshotRepository
{
    public const string CollectionName = "territory_resource_assignment_plan_snapshots";

    private readonly IMongoCollection<TerritoryResourceAssignmentPlanSnapshot> _collection;

    public TerritoryResourceAssignmentPlanSnapshotRepository(IMongoDatabase database)
        => _collection = database.GetCollection<TerritoryResourceAssignmentPlanSnapshot>(CollectionName);

    public async Task<TerritoryResourceAssignmentPlanSnapshot?> GetLatestAsync(
        Guid tenantId, Guid modelId, CancellationToken cancellationToken)
        => await _collection
            .Find(s => s.TenantId == tenantId && s.TerritoryModelId == modelId && !s.IsDeleted)
            .SortByDescending(s => s.SnapshotVersion)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TerritoryResourceAssignmentPlanSnapshot>> ListByModelAsync(
        Guid tenantId, Guid modelId, CancellationToken cancellationToken)
        => await _collection
            .Find(s => s.TenantId == tenantId && s.TerritoryModelId == modelId && !s.IsDeleted)
            .SortByDescending(s => s.SnapshotVersion)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TerritoryResourceAssignmentPlanSnapshot>> ListByResourceAsync(
        Guid tenantId, string resourceId, CancellationToken cancellationToken)
    {
        var filter = Builders<TerritoryResourceAssignmentPlanSnapshot>.Filter.And(
            Builders<TerritoryResourceAssignmentPlanSnapshot>.Filter.Eq(s => s.TenantId, tenantId),
            Builders<TerritoryResourceAssignmentPlanSnapshot>.Filter.Eq(s => s.IsDeleted, false),
            Builders<TerritoryResourceAssignmentPlanSnapshot>.Filter.ElemMatch(
                s => s.Lines,
                Builders<TerritoryResourceAssignmentPlanSnapshotLine>.Filter.Eq(l => l.ResourceId, resourceId)));

        return await _collection.Find(filter)
            .SortByDescending(s => s.SnapshotVersion)
            .ToListAsync(cancellationToken);
    }
}
