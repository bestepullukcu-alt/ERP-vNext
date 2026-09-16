using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence.Repositories;

/// <summary>
/// Purchase Order Mongo repository (MOD-0141). HER sorgu Tenant + LegalEntity + IsDeleted=false ile filtrelenir
/// (multi-tenancy.md hard rules; cross-tenant/LE → boş sonuç → handler 404). Soft-delete zorunlu; hard delete YOK.
/// State geçişi (approve) optimistic concurrency: Version filtresi + increment.
/// </summary>
public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly IMongoCollection<PurchaseOrder> _purchaseOrders;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public PurchaseOrderRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _purchaseOrders = database.GetCollection<PurchaseOrder>(RequisitionPoIndexConfiguration.PurchaseOrderCollectionName);
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    public async Task<IReadOnlyList<PurchaseOrder>> GetAllAsync(string? supplierId = null, PoStatus? status = null, CancellationToken cancellationToken = default)
    {
        var filter = TenantFilter();
        if (!string.IsNullOrWhiteSpace(supplierId))
        {
            filter &= Builders<PurchaseOrder>.Filter.Eq(x => x.SupplierId, supplierId);
        }
        if (status.HasValue)
        {
            filter &= Builders<PurchaseOrder>.Filter.Eq(x => x.Status, status.Value);
        }
        return await _purchaseOrders.Find(filter).SortBy(x => x.PoId).ToListAsync(cancellationToken);
    }

    public async Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PurchaseOrder>.Filter.And(
            TenantFilter(),
            Builders<PurchaseOrder>.Filter.Eq(x => x.Id, id));
        return await _purchaseOrders.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PurchaseOrder?> GetByPoIdAsync(string poId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PurchaseOrder>.Filter.And(
            TenantFilter(),
            Builders<PurchaseOrder>.Filter.Eq(x => x.PoId, poId));
        return await _purchaseOrders.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PurchaseOrder?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PurchaseOrder>.Filter.And(
            TenantFilter(),
            Builders<PurchaseOrder>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return await _purchaseOrders.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<PurchaseOrder> CreateAsync(PurchaseOrder entity, CancellationToken cancellationToken = default)
    {
        // Tenant + LE server-side set edilir; asla payload'dan.
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _purchaseOrders.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(PurchaseOrder entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        // Tenant + LE server-side set edilir. Cross-LE update filtre dışı kalır → ModifiedCount=0.
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var filter = Builders<PurchaseOrder>.Filter.And(
            TenantFilter(),
            Builders<PurchaseOrder>.Filter.Eq(x => x.Id, entity.Id),
            Builders<PurchaseOrder>.Filter.Eq(x => x.Version, expectedVersion));

        entity.Version = expectedVersion + 1;
        var result = await _purchaseOrders.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PurchaseOrder>.Filter.And(
            TenantFilter(),
            Builders<PurchaseOrder>.Filter.Eq(x => x.Id, id));

        var update = Builders<PurchaseOrder>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _purchaseOrders.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var filter = Builders<PurchaseOrder>.Filter.And(
            TenantFilter(),
            Builders<PurchaseOrder>.Filter.In(x => x.Id, ids));

        var update = Builders<PurchaseOrder>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _purchaseOrders.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    /// <summary>Tenant + LegalEntity + IsDeleted=false — her sorgunun zorunlu giriş filtresi.</summary>
    private FilterDefinition<PurchaseOrder> TenantFilter()
        => Builders<PurchaseOrder>.Filter.And(
            Builders<PurchaseOrder>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<PurchaseOrder>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId),
            Builders<PurchaseOrder>.Filter.Eq(x => x.IsDeleted, false));
}
