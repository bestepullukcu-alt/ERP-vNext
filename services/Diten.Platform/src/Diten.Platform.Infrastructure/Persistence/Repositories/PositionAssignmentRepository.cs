using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class PositionAssignmentRepository : TenantRepository<PositionAssignment>, IPositionAssignmentRepository
{
    public PositionAssignmentRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.PositionAssignments)
    {
    }

    public Task<bool> HasOverlapAsync(
        Guid positionId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid? excludeId = null,
        CancellationToken ct = default)
    {
        var requestedEnd = effectiveTo ?? DateTimeOffset.MaxValue;
        var filters = new List<FilterDefinition<PositionAssignment>>
        {
            ExecutionFilter,
            Builders<PositionAssignment>.Filter.Eq(x => x.PositionId, positionId),
            Builders<PositionAssignment>.Filter.Lt(x => x.EffectiveFrom, requestedEnd),
            Builders<PositionAssignment>.Filter.Or(
                Builders<PositionAssignment>.Filter.Eq(x => x.EffectiveTo, null),
                Builders<PositionAssignment>.Filter.Gt(x => x.EffectiveTo, effectiveFrom))
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<PositionAssignment>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<PositionAssignment>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateAsync(PositionAssignment assignment, CancellationToken ct = default)
    {
        assignment.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<PositionAssignment>.Filter.And(
            ExecutionFilter,
            Builders<PositionAssignment>.Filter.Eq(x => x.Id, assignment.Id));
        await Collection.ReplaceOneAsync(filter, assignment, cancellationToken: ct);
    }

    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<PositionAssignment>.Filter.And(
            ExecutionFilter,
            Builders<PositionAssignment>.Filter.Eq(x => x.Id, id));
        var now = DateTimeOffset.UtcNow;
        var update = Builders<PositionAssignment>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
