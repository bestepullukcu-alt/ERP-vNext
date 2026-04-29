using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantDomainRepository : GlobalRepository<TenantDomain>, ITenantDomainRepository
{
    public TenantDomainRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "tenant_domains") { }

    public async Task<TenantDomain?> GetByDomainNameAsync(string domainName, CancellationToken ct = default)
    {
        var filter = Builders<TenantDomain>.Filter.And(
            ExecutionFilter,
            Builders<TenantDomain>.Filter.Eq(x => x.DomainName, domainName.ToLowerInvariant()));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TenantDomain>> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var filter = Builders<TenantDomain>.Filter.And(
            ExecutionFilter,
            Builders<TenantDomain>.Filter.Eq(x => x.TenantId, tenantId));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<TenantDomain?> GetPrimaryByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        var filter = Builders<TenantDomain>.Filter.And(
            ExecutionFilter,
            Builders<TenantDomain>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantDomain>.Filter.Eq(x => x.IsPrimary, true));
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task UpdateAsync(TenantDomain domain, CancellationToken ct = default)
    {
        var filter = Builders<TenantDomain>.Filter.And(
            ExecutionFilter,
            Builders<TenantDomain>.Filter.Eq(x => x.Id, domain.Id));
        await Collection.ReplaceOneAsync(filter, domain, cancellationToken: ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var filter = Builders<TenantDomain>.Filter.And(
            ExecutionFilter,
            Builders<TenantDomain>.Filter.Eq(x => x.Id, id));
        var update = Builders<TenantDomain>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
