using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class PositionRepository : TenantRepository<Position>, IPositionRepository
{
    public PositionRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "positions")
    {
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<Position>>
        {
            ExecutionFilter,
            Builders<Position>.Filter.Eq(x => x.Code, code)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<Position>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<Position>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateAsync(Position position, CancellationToken ct = default)
    {
        position.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<Position>.Filter.And(
            ExecutionFilter,
            Builders<Position>.Filter.Eq(x => x.Id, position.Id));
        await Collection.ReplaceOneAsync(filter, position, cancellationToken: ct);
    }

    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<Position>.Filter.And(
            ExecutionFilter,
            Builders<Position>.Filter.Eq(x => x.Id, id));
        var now = DateTimeOffset.UtcNow;
        var update = Builders<Position>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
