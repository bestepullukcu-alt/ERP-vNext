using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class TenantAuditPreferenceRepository : ITenantAuditPreferenceRepository
{
    private readonly IMongoCollection<TenantAuditPreference> _collection;

    public TenantAuditPreferenceRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TenantAuditPreference>(AuditCollectionNames.TenantAuditPreferences);
    }

    public async Task<TenantAuditPreference?> GetByTenantAndCategoryAsync(Guid tenantId, AuditCategory category, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        }

        if (category == AuditCategory.Unknown)
        {
            throw new ArgumentException("Audit category is required.", nameof(category));
        }

        var filter = Builders<TenantAuditPreference>.Filter.And(
            Builders<TenantAuditPreference>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TenantAuditPreference>.Filter.Eq(x => x.Category, category),
            Builders<TenantAuditPreference>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task UpsertAsync(TenantAuditPreference preference, AuditEventRetentionPolicy policy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(preference);
        ArgumentNullException.ThrowIfNull(policy);
        preference.ValidateAgainst(policy);
        if (preference.IsDeleted)
        {
            throw new InvalidOperationException("Tenant audit preference upsert cannot target a soft-deleted record.");
        }

        var filter = Builders<TenantAuditPreference>.Filter.And(
            Builders<TenantAuditPreference>.Filter.Eq(x => x.TenantId, preference.TenantId),
            Builders<TenantAuditPreference>.Filter.Eq(x => x.Category, preference.Category),
            Builders<TenantAuditPreference>.Filter.Eq(x => x.IsDeleted, false));

        await _collection.ReplaceOneAsync(filter, preference, new ReplaceOptions { IsUpsert = true }, ct);
    }
}
