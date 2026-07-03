using Diten.Platform.Common.Persistence;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantNavPreferenceRepository : GlobalRepository<TenantNavPreference>, ITenantNavPreferenceRepository
{
    public TenantNavPreferenceRepository(IPlatformDbContext dbContext, ITenantContext tenantContext)
        : base(dbContext.Database, tenantContext, "tenant_nav_preferences")
    {
    }

    public async Task<IReadOnlyList<TenantNavPreference>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        var filter = Builders<TenantNavPreference>.Filter.And(
            ExecutionFilter, // IsDeleted == false
            Builders<TenantNavPreference>.Filter.Eq(x => x.TenantId, tenantId));

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task ReplaceForTenantAsync(Guid tenantId, IReadOnlyCollection<TenantNavPreference> items, CancellationToken ct = default)
    {
        // Full-set replace: hard-delete the tenant's existing rows, then insert the new set. Hard delete (rather
        // than soft) keeps the unique (TenantId, ModuleCode) partial index clean and avoids accumulating cruft for
        // what is a wholesale overwrite.
        await Collection.DeleteManyAsync(
            Builders<TenantNavPreference>.Filter.Eq(x => x.TenantId, tenantId),
            ct);

        var rows = items?
            .Where(i => i is not null && !string.IsNullOrWhiteSpace(i.ModuleCode))
            .ToList()
            ?? [];

        foreach (var row in rows)
        {
            row.TenantId = tenantId; // authoritative: the row always belongs to this tenant
        }

        if (rows.Count > 0)
        {
            await Collection.InsertManyAsync(rows, cancellationToken: ct);
        }
    }
}
