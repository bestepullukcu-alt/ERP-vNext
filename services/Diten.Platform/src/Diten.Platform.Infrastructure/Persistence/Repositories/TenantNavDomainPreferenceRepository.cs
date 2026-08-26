using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantNavDomainPreferenceRepository : GlobalRepository<TenantNavDomainPreference>, ITenantNavDomainPreferenceRepository
{
    public TenantNavDomainPreferenceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, PlatformCollections.TenantNavDomainPreferences)
    {
    }

    public async Task<IReadOnlyList<TenantNavDomainPreference>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var filter = Builders<TenantNavDomainPreference>.Filter.And(
            ExecutionFilter, // IsDeleted == false
            Builders<TenantNavDomainPreference>.Filter.Eq(x => x.TenantId, tenantId));

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task ReplaceForTenantAsync(Guid tenantId, IReadOnlyCollection<TenantNavDomainPreference> items, CancellationToken ct = default)
    {
        // Full-set replace: hard-delete the tenant's existing rows, then insert the new set (keeps the unique
        // (TenantId, DomainCode) partial index clean for a wholesale overwrite).
        await Collection.DeleteManyAsync(
            Builders<TenantNavDomainPreference>.Filter.Eq(x => x.TenantId, tenantId),
            ct);

        var rows = items?
            .Where(i => i is not null && !string.IsNullOrWhiteSpace(i.DomainCode))
            .ToList()
            ?? [];

        foreach (var row in rows)
        {
            row.TenantId = tenantId;
        }

        if (rows.Count > 0)
        {
            await Collection.InsertManyAsync(rows, cancellationToken: ct);
        }
    }
}
