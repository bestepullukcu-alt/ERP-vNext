using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence.Repositories;

/// <summary>
/// Sourcing (RFx + Bid) Mongo repository (MOD-0145). HER sorgu Tenant + LegalEntity + IsDeleted=false ile filtrelenir
/// (multi-tenancy.md hard rules; cross-tenant/LE → boş sonuç → handler 404). Soft-delete zorunlu; hard delete YOK.
/// State geçişleri (publish/award) optimistic concurrency: Version filtresi + increment.
/// </summary>
public sealed class RfxRepository : IRfxRepository
{
    private readonly IMongoCollection<RfxEvent> _rfx;
    private readonly IMongoCollection<Bid> _bids;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public RfxRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _rfx = database.GetCollection<RfxEvent>(SourcingIndexConfiguration.RfxCollectionName);
        _bids = database.GetCollection<Bid>(SourcingIndexConfiguration.BidCollectionName);
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    // ── RFx events ───────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RfxEvent>> GetAllAsync(RfxStatus? status = null, CancellationToken cancellationToken = default)
    {
        var filter = RfxTenantFilter();
        if (status.HasValue)
        {
            filter &= Builders<RfxEvent>.Filter.Eq(x => x.Status, status.Value);
        }
        return await _rfx.Find(filter).SortBy(x => x.RfxId).ToListAsync(cancellationToken);
    }

    public async Task<RfxEvent?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RfxEvent>.Filter.And(
            RfxTenantFilter(),
            Builders<RfxEvent>.Filter.Eq(x => x.Id, id));
        return await _rfx.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RfxEvent?> GetByRfxIdAsync(string rfxId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RfxEvent>.Filter.And(
            RfxTenantFilter(),
            Builders<RfxEvent>.Filter.Eq(x => x.RfxId, rfxId));
        return await _rfx.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RfxEvent?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RfxEvent>.Filter.And(
            RfxTenantFilter(),
            Builders<RfxEvent>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return await _rfx.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RfxEvent> CreateAsync(RfxEvent entity, CancellationToken cancellationToken = default)
    {
        // Tenant + LE server-side set edilir; asla payload'dan.
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _rfx.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateRfxAsync(RfxEvent entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        // Tenant + LE server-side set edilir. Cross-LE update filtre dışı kalır → ModifiedCount=0.
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var filter = Builders<RfxEvent>.Filter.And(
            RfxTenantFilter(),
            Builders<RfxEvent>.Filter.Eq(x => x.Id, entity.Id),
            Builders<RfxEvent>.Filter.Eq(x => x.Version, expectedVersion));

        entity.Version = expectedVersion + 1;
        var result = await _rfx.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RfxEvent>.Filter.And(
            RfxTenantFilter(),
            Builders<RfxEvent>.Filter.Eq(x => x.Id, id));

        var update = Builders<RfxEvent>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _rfx.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RfxEvent>.Filter.And(
            RfxTenantFilter(),
            Builders<RfxEvent>.Filter.In(x => x.Id, ids));

        var update = Builders<RfxEvent>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _rfx.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    // ── Bids (RFx alt kaynağı) ─────────────────────────────────────────────────

    public async Task<Bid> CreateBidAsync(Bid entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _bids.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyList<Bid>> GetBidsByRfxIdAsync(string rfxId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Bid>.Filter.And(
            BidTenantFilter(),
            Builders<Bid>.Filter.Eq(x => x.RfxId, rfxId));
        return await _bids.Find(filter).SortBy(x => x.BidId).ToListAsync(cancellationToken);
    }

    public async Task<Bid?> GetBidByBidIdAsync(string bidId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Bid>.Filter.And(
            BidTenantFilter(),
            Builders<Bid>.Filter.Eq(x => x.BidId, bidId));
        return await _bids.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Bid?> GetBidByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Bid>.Filter.And(
            BidTenantFilter(),
            Builders<Bid>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return await _bids.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>Tenant + LegalEntity + IsDeleted=false — RFx sorgularının zorunlu giriş filtresi.</summary>
    private FilterDefinition<RfxEvent> RfxTenantFilter()
        => Builders<RfxEvent>.Filter.And(
            Builders<RfxEvent>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<RfxEvent>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId),
            Builders<RfxEvent>.Filter.Eq(x => x.IsDeleted, false));

    /// <summary>Tenant + LegalEntity + IsDeleted=false — Bid sorgularının zorunlu giriş filtresi.</summary>
    private FilterDefinition<Bid> BidTenantFilter()
        => Builders<Bid>.Filter.And(
            Builders<Bid>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<Bid>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId),
            Builders<Bid>.Filter.Eq(x => x.IsDeleted, false));
}
