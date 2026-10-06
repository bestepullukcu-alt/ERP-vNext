using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence.Repositories;

/// <summary>
/// Goods Receipt (GRN) Mongo repository (MOD-0142). HER sorgu Tenant + LegalEntity + IsDeleted=false ile filtrelenir
/// (multi-tenancy.md hard rules; cross-tenant/LE → boş sonuç → handler 404). Soft-delete zorunlu; hard delete YOK.
/// State geçişi (reverse) optimistic concurrency: Version filtresi + increment. GRN hiçbir stok balance PERSIST
/// ETMEZ — yalnız mal-kabul dokümanı + INVENTORY transaction referansı (no-shadow-stock; envanter SoR MOD-0173).
/// </summary>
public sealed class GrnRepository : IGrnRepository
{
    private readonly IMongoCollection<GoodsReceipt> _goodsReceipts;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public GrnRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _goodsReceipts = database.GetCollection<GoodsReceipt>(GrnIndexConfiguration.GrnCollectionName);
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    public async Task<IReadOnlyList<GoodsReceipt>> GetAllAsync(string? poId = null, GrnStatus? status = null, CancellationToken cancellationToken = default)
    {
        var filter = TenantFilter();
        if (!string.IsNullOrWhiteSpace(poId))
        {
            filter &= Builders<GoodsReceipt>.Filter.Eq(x => x.PoId, poId);
        }
        if (status.HasValue)
        {
            filter &= Builders<GoodsReceipt>.Filter.Eq(x => x.Status, status.Value);
        }
        return await _goodsReceipts.Find(filter).SortBy(x => x.GrnId).ToListAsync(cancellationToken);
    }

    public async Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoodsReceipt>.Filter.And(
            TenantFilter(),
            Builders<GoodsReceipt>.Filter.Eq(x => x.Id, id));
        return await _goodsReceipts.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GoodsReceipt?> GetByGrnIdAsync(string grnId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoodsReceipt>.Filter.And(
            TenantFilter(),
            Builders<GoodsReceipt>.Filter.Eq(x => x.GrnId, grnId));
        return await _goodsReceipts.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GoodsReceipt?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoodsReceipt>.Filter.And(
            TenantFilter(),
            Builders<GoodsReceipt>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return await _goodsReceipts.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GoodsReceipt> CreateAsync(GoodsReceipt entity, CancellationToken cancellationToken = default)
    {
        // Tenant + LE server-side set edilir; asla payload'dan.
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _goodsReceipts.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(GoodsReceipt entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        // Tenant + LE server-side set edilir. Cross-LE update filtre dışı kalır → ModifiedCount=0.
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var filter = Builders<GoodsReceipt>.Filter.And(
            TenantFilter(),
            Builders<GoodsReceipt>.Filter.Eq(x => x.Id, entity.Id),
            Builders<GoodsReceipt>.Filter.Eq(x => x.Version, expectedVersion));

        entity.Version = expectedVersion + 1;
        var result = await _goodsReceipts.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoodsReceipt>.Filter.And(
            TenantFilter(),
            Builders<GoodsReceipt>.Filter.Eq(x => x.Id, id));

        var update = Builders<GoodsReceipt>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _goodsReceipts.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var filter = Builders<GoodsReceipt>.Filter.And(
            TenantFilter(),
            Builders<GoodsReceipt>.Filter.In(x => x.Id, ids));

        var update = Builders<GoodsReceipt>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _goodsReceipts.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    /// <summary>Tenant + LegalEntity + IsDeleted=false — her sorgunun zorunlu giriş filtresi.</summary>
    private FilterDefinition<GoodsReceipt> TenantFilter()
        => Builders<GoodsReceipt>.Filter.And(
            Builders<GoodsReceipt>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<GoodsReceipt>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId),
            Builders<GoodsReceipt>.Filter.Eq(x => x.IsDeleted, false));
}
