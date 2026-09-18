using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence.Repositories;

/// <summary>
/// Requisition Mongo repository (MOD-0141). HER sorgu Tenant + LegalEntity + IsDeleted=false ile filtrelenir
/// (multi-tenancy.md hard rules; cross-tenant/LE → boş sonuç → handler 404). Soft-delete zorunlu; hard delete YOK.
/// State geçişi (submit) optimistic concurrency: Version filtresi + increment.
/// </summary>
public sealed class RequisitionRepository : IRequisitionRepository
{
    private readonly IMongoCollection<Requisition> _requisitions;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public RequisitionRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _requisitions = database.GetCollection<Requisition>(RequisitionPoIndexConfiguration.RequisitionCollectionName);
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    public async Task<IReadOnlyList<Requisition>> GetAllAsync(RequisitionStatus? status = null, CancellationToken cancellationToken = default)
    {
        var filter = TenantFilter();
        if (status.HasValue)
        {
            filter &= Builders<Requisition>.Filter.Eq(x => x.Status, status.Value);
        }
        return await _requisitions.Find(filter).SortBy(x => x.RequisitionId).ToListAsync(cancellationToken);
    }

    public async Task<Requisition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Requisition>.Filter.And(
            TenantFilter(),
            Builders<Requisition>.Filter.Eq(x => x.Id, id));
        return await _requisitions.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Requisition?> GetByRequisitionIdAsync(string requisitionId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Requisition>.Filter.And(
            TenantFilter(),
            Builders<Requisition>.Filter.Eq(x => x.RequisitionId, requisitionId));
        return await _requisitions.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Requisition?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Requisition>.Filter.And(
            TenantFilter(),
            Builders<Requisition>.Filter.Eq(x => x.IdempotencyKey, idempotencyKey));
        return await _requisitions.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Requisition> CreateAsync(Requisition entity, CancellationToken cancellationToken = default)
    {
        // Tenant + LE server-side set edilir; asla payload'dan.
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _requisitions.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(Requisition entity, int expectedVersion, CancellationToken cancellationToken = default)
    {
        // Tenant + LE server-side set edilir. Cross-LE update filtre dışı kalır → ModifiedCount=0.
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var filter = Builders<Requisition>.Filter.And(
            TenantFilter(),
            Builders<Requisition>.Filter.Eq(x => x.Id, entity.Id),
            Builders<Requisition>.Filter.Eq(x => x.Version, expectedVersion));

        entity.Version = expectedVersion + 1;
        var result = await _requisitions.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Requisition>.Filter.And(
            TenantFilter(),
            Builders<Requisition>.Filter.Eq(x => x.Id, id));

        var update = Builders<Requisition>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _requisitions.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Requisition>.Filter.And(
            TenantFilter(),
            Builders<Requisition>.Filter.In(x => x.Id, ids));

        var update = Builders<Requisition>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _requisitions.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return (int)result.ModifiedCount;
    }

    /// <summary>Tenant + LegalEntity + IsDeleted=false — her sorgunun zorunlu giriş filtresi.</summary>
    private FilterDefinition<Requisition> TenantFilter()
        => Builders<Requisition>.Filter.And(
            Builders<Requisition>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<Requisition>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId),
            Builders<Requisition>.Filter.Eq(x => x.IsDeleted, false));
}
