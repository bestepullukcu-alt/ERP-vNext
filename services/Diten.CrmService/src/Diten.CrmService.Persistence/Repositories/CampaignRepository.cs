using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;
using CampaignEntity = Diten.CrmService.Domain.Entities.Campaign;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0165 FU04 campaign persistence. Soft-delete aware and tenant scoped. No delete method exists: closing a campaign
/// is the soft archive lifecycle. StartDate / EndDate / ArchivedAt (DateTimeOffset → BSON array) are never sorted
/// server-side nor used as index keys; ordering happens in memory.
/// </summary>
public sealed class CampaignRepository : ICampaignRepository
{
    public const string CollectionName = "campaigns";

    private readonly IMongoCollection<CampaignEntity> _collection;

    public CampaignRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CampaignEntity>(CollectionName);
    }

    private static FilterDefinition<CampaignEntity> Tenant(Guid tenantId)
        => Builders<CampaignEntity>.Filter.Where(c => c.TenantId == tenantId && !c.IsDeleted);

    public async Task<CampaignEntity?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<CampaignEntity>.Filter.Eq(c => c.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CampaignEntity>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // CreatedAt is a DateTimeOffset (BSON array) so it is not used as a server-side sort key; order in memory.
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderByDescending(c => c.CreatedAt).ToList();
    }

    /// <summary>Duplicate-code guard. Archived rows are excluded with an <c>ArchivedAt == null</c> equality filter
    /// (never <c>$ne</c>, which is unsupported in partial-index filters and has crash-looped services here), so an
    /// archived code becomes reusable.</summary>
    public async Task<CampaignEntity?> GetActiveByCodeAsync(
        Guid tenantId, string campaignCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<CampaignEntity>.Filter.Eq(c => c.CampaignCode, campaignCode)
                & Builders<CampaignEntity>.Filter.Eq(c => c.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<CampaignEntity?> FindByExternalReferenceAsync(
        Guid tenantId, string sourceSystem, string externalId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<CampaignEntity>.Filter.ElemMatch(
                    c => c.ExternalReferences,
                    Builders<CampaignExternalReference>.Filter.Eq(x => x.SourceSystem, sourceSystem)
                        & Builders<CampaignExternalReference>.Filter.Eq(x => x.ExternalId, externalId))
                & Builders<CampaignEntity>.Filter.Eq(c => c.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(CampaignEntity campaign, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(campaign, cancellationToken: cancellationToken);

    public async Task UpdateAsync(CampaignEntity campaign, CancellationToken cancellationToken)
    {
        var filter = Builders<CampaignEntity>.Filter.Where(c => c.Id == campaign.Id && c.TenantId == campaign.TenantId);
        await _collection.ReplaceOneAsync(filter, campaign, cancellationToken: cancellationToken);
    }
}

/// <summary>
/// MOD-0165 FU04 campaign target persistence. Same rules as <see cref="CampaignRepository"/>: tenant scoped,
/// soft-delete aware, <b>no delete method</b>, in-memory ordering for DateTimeOffset fields. A snapshot only ever
/// inserts or replaces — nothing here can remove a target.
/// </summary>
public sealed class CampaignTargetRepository : ICampaignTargetRepository
{
    public const string CollectionName = "campaign_targets";

    private readonly IMongoCollection<CampaignTarget> _collection;

    public CampaignTargetRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CampaignTarget>(CollectionName);
    }

    private static FilterDefinition<CampaignTarget> Tenant(Guid tenantId)
        => Builders<CampaignTarget>.Filter.Where(t => t.TenantId == tenantId && !t.IsDeleted);

    public async Task<CampaignTarget?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<CampaignTarget>.Filter.Eq(t => t.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<CampaignTarget>> ListByCampaignAsync(
        Guid tenantId, Guid campaignId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<CampaignTarget>.Filter.Eq(t => t.CampaignId, campaignId))
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(t => t.CreatedAt).ToList();
    }

    /// <summary>
    /// Duplicate/idempotency lookup for the (campaign, targetType, targetId) triple. Archived rows are excluded with an
    /// equality filter on <c>ArchivedAt</c>, so an archived target does not block re-targeting the same person later.
    /// </summary>
    public async Task<CampaignTarget?> FindActiveByTargetAsync(
        Guid tenantId, Guid campaignId, string targetType, Guid targetId, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<CampaignTarget>.Filter.Eq(t => t.CampaignId, campaignId)
                & Builders<CampaignTarget>.Filter.Eq(t => t.TargetType, targetType)
                & Builders<CampaignTarget>.Filter.Eq(t => t.TargetId, targetId)
                & Builders<CampaignTarget>.Filter.Eq(t => t.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(CampaignTarget target, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(target, cancellationToken: cancellationToken);

    public async Task UpdateAsync(CampaignTarget target, CancellationToken cancellationToken)
    {
        var filter = Builders<CampaignTarget>.Filter.Where(t => t.Id == target.Id && t.TenantId == target.TenantId);
        await _collection.ReplaceOneAsync(filter, target, cancellationToken: cancellationToken);
    }
}
