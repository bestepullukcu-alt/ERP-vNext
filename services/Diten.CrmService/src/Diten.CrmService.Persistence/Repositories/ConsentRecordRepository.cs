using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0164 FU02 consent persistence. Soft-delete aware and tenant scoped. No delete method exists: closing a record is
/// the soft archive lifecycle, so consent history (including withdrawals) stays readable forever.
/// EffectiveFrom / EffectiveTo / ArchivedAt (DateTimeOffset → BSON array) are never sorted server-side nor used as
/// index keys; ordering and window filtering happen in memory.
/// </summary>
public sealed class ConsentRecordRepository : IConsentRecordRepository
{
    public const string CollectionName = "consent_records";

    private readonly IMongoCollection<ConsentRecord> _collection;

    public ConsentRecordRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ConsentRecord>(CollectionName);
    }

    private static FilterDefinition<ConsentRecord> Tenant(Guid tenantId)
        => Builders<ConsentRecord>.Filter.Where(r => r.TenantId == tenantId && !r.IsDeleted);

    public async Task<ConsentRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<ConsentRecord>.Filter.Eq(r => r.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ConsentRecord>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // CreatedAt is a DateTimeOffset (BSON array) so it is not used as a server-side sort key; order in memory.
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderByDescending(r => r.CreatedAt).ToList();
    }

    /// <summary>
    /// Read-only evaluation seam. Filters what an index can serve — tenant, subject, channel — and leaves purpose,
    /// scope and the effective window to the in-memory engine. Archived rows are excluded here with an
    /// <c>ArchivedAt == null</c> equality filter (never <c>$ne</c>, which is unsupported in partial-index filters and
    /// has crash-looped services in this repo).
    /// </summary>
    public async Task<IReadOnlyList<ConsentRecord>> ListForEvaluationAsync(
        Guid tenantId, string subjectType, Guid subjectId, string channel, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<ConsentRecord>.Filter.Eq(r => r.SubjectType, subjectType)
                & Builders<ConsentRecord>.Filter.Eq(r => r.SubjectId, subjectId)
                & Builders<ConsentRecord>.Filter.Eq(r => r.Channel, channel)
                & Builders<ConsentRecord>.Filter.Eq(r => r.ArchivedAt, null))
            .ToListAsync(cancellationToken);

    public async Task<ConsentRecord?> FindByExternalReferenceAsync(
        Guid tenantId, string sourceSystem, string externalId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<ConsentRecord>.Filter.ElemMatch(
                    r => r.ExternalReferences,
                    Builders<ConsentExternalReference>.Filter.Eq(x => x.SourceSystem, sourceSystem)
                        & Builders<ConsentExternalReference>.Filter.Eq(x => x.ExternalId, externalId))
                & Builders<ConsentRecord>.Filter.Eq(r => r.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(ConsentRecord record, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(record, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ConsentRecord record, CancellationToken cancellationToken)
    {
        var filter = Builders<ConsentRecord>.Filter.Where(r => r.Id == record.Id && r.TenantId == record.TenantId);
        await _collection.ReplaceOneAsync(filter, record, cancellationToken: cancellationToken);
    }
}

/// <summary>
/// MOD-0164 FU02 preference persistence. Same rules as <see cref="ConsentRecordRepository"/>: tenant scoped,
/// soft-delete aware, no delete method, in-memory ordering for DateTimeOffset fields.
/// </summary>
public sealed class PreferenceRecordRepository : IPreferenceRecordRepository
{
    public const string CollectionName = "preference_records";

    private readonly IMongoCollection<PreferenceRecord> _collection;

    public PreferenceRecordRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<PreferenceRecord>(CollectionName);
    }

    private static FilterDefinition<PreferenceRecord> Tenant(Guid tenantId)
        => Builders<PreferenceRecord>.Filter.Where(r => r.TenantId == tenantId && !r.IsDeleted);

    public async Task<PreferenceRecord?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<PreferenceRecord>.Filter.Eq(r => r.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PreferenceRecord>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderByDescending(r => r.CreatedAt).ToList();
    }

    /// <summary>Read-only evaluation seam. The channel match is NOT pushed down: a preference may carry the <c>all</c>
    /// sentinel, so the channel decision belongs to the engine (a server-side channel equality filter would silently
    /// drop blanket restrictions and fail OPEN).</summary>
    public async Task<IReadOnlyList<PreferenceRecord>> ListForEvaluationAsync(
        Guid tenantId, string subjectType, Guid subjectId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<PreferenceRecord>.Filter.Eq(r => r.SubjectType, subjectType)
                & Builders<PreferenceRecord>.Filter.Eq(r => r.SubjectId, subjectId)
                & Builders<PreferenceRecord>.Filter.Eq(r => r.ArchivedAt, null))
            .ToListAsync(cancellationToken);

    public async Task<PreferenceRecord?> FindByExternalReferenceAsync(
        Guid tenantId, string sourceSystem, string externalId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<PreferenceRecord>.Filter.ElemMatch(
                    r => r.ExternalReferences,
                    Builders<ConsentExternalReference>.Filter.Eq(x => x.SourceSystem, sourceSystem)
                        & Builders<ConsentExternalReference>.Filter.Eq(x => x.ExternalId, externalId))
                & Builders<PreferenceRecord>.Filter.Eq(r => r.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(PreferenceRecord record, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(record, cancellationToken: cancellationToken);

    public async Task UpdateAsync(PreferenceRecord record, CancellationToken cancellationToken)
    {
        var filter = Builders<PreferenceRecord>.Filter.Where(r => r.Id == record.Id && r.TenantId == record.TenantId);
        await _collection.ReplaceOneAsync(filter, record, cancellationToken: cancellationToken);
    }
}
