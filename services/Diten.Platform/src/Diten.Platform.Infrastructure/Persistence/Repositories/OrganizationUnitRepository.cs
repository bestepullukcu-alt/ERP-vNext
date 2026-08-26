using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class OrganizationUnitRepository : TenantRepository<OrganizationUnit>, IOrganizationUnitRepository
{
    public OrganizationUnitRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.OrganizationUnits)
    {
    }

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<OrganizationUnit>>
        {
            ExecutionFilter,
            Builders<OrganizationUnit>.Filter.Eq(x => x.Code, code)
        };

        if (excludeId.HasValue)
        {
            filters.Add(Builders<OrganizationUnit>.Filter.Ne(x => x.Id, excludeId.Value));
        }

        return Collection.Find(Builders<OrganizationUnit>.Filter.And(filters)).AnyAsync(ct);
    }

    public async Task UpdateAsync(OrganizationUnit organizationUnit, CancellationToken ct = default)
    {
        organizationUnit.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<OrganizationUnit>.Filter.And(
            ExecutionFilter,
            Builders<OrganizationUnit>.Filter.Eq(x => x.Id, organizationUnit.Id));
        await Collection.ReplaceOneAsync(filter, organizationUnit, cancellationToken: ct);
    }

    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<OrganizationUnit>.Filter.And(
            ExecutionFilter,
            Builders<OrganizationUnit>.Filter.Eq(x => x.Id, id));
        var now = DateTimeOffset.UtcNow;
        var update = Builders<OrganizationUnit>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, now)
            .Set(x => x.UpdatedAt, now);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
