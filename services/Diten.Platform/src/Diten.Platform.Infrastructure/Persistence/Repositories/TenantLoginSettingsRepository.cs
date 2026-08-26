using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantLoginSettingsRepository : GlobalRepository<TenantLoginSettings>, ITenantLoginSettingsRepository
{
    public TenantLoginSettingsRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TenantLoginSettings) { }

    public async Task<TenantLoginSettings?> GetByTenantRefIdAsync(Guid tenantRefId, CancellationToken ct = default)
    {
        var filter = Builders<TenantLoginSettings>.Filter.And(
            ExecutionFilter,
            Builders<TenantLoginSettings>.Filter.Eq(x => x.TenantRefId, tenantRefId));

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task UpdateAsync(TenantLoginSettings settings, CancellationToken ct = default)
    {
        var filter = Builders<TenantLoginSettings>.Filter.And(
            ExecutionFilter,
            Builders<TenantLoginSettings>.Filter.Eq(x => x.Id, settings.Id));

        await Collection.ReplaceOneAsync(filter, settings, cancellationToken: ct);
    }
}
