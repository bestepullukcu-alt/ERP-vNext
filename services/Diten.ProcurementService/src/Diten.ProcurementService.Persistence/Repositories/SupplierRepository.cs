using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence.Repositories;

/// <summary>
/// Supplier Mongo repository. HER sorgu Tenant + LegalEntity + IsDeleted=false ile filtrelenir
/// (multi-tenancy.md hard rules; cross-tenant/LE → boş sonuç → handler 404). Soft-delete zorunlu; hard delete YOK.
/// </summary>
public sealed class SupplierRepository : ISupplierRepository
{
    private readonly IMongoCollection<Supplier> _collection;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public SupplierRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _collection = database.GetCollection<Supplier>(SupplierIndexConfiguration.CollectionName);
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    public async Task<IReadOnlyList<Supplier>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(TenantFilter()).SortBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Supplier>.Filter.And(
            TenantFilter(),
            Builders<Supplier>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Supplier> CreateAsync(Supplier entity, CancellationToken cancellationToken = default)
    {
        // Tenant + LE server-side set edilir; asla payload'dan.
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.IsDeleted = false;
        await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(Supplier entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId = _tenantId;
        entity.LegalEntityId = _legalEntityId;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        var filter = Builders<Supplier>.Filter.And(
            TenantFilter(),
            Builders<Supplier>.Filter.Eq(x => x.Id, entity.Id));
        var result = await _collection.ReplaceOneAsync(filter, entity, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Supplier>.Filter.And(
            TenantFilter(),
            Builders<Supplier>.Filter.Eq(x => x.Id, id));

        var update = Builders<Supplier>.Update
            .Set(x => x.IsDeleted, true)
            .Set(x => x.DeletedAt, DateTimeOffset.UtcNow)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<bool> ExistsByTaxIdAsync(string taxId, Guid? excludeId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<Supplier>.Filter.And(
            TenantFilter(),
            Builders<Supplier>.Filter.Eq(x => x.TaxId, taxId));
        if (excludeId.HasValue)
        {
            filter &= Builders<Supplier>.Filter.Ne(x => x.Id, excludeId.Value);
        }
        return await _collection.Find(filter).AnyAsync(cancellationToken);
    }

    /// <summary>Tenant + LegalEntity + IsDeleted=false — her sorgunun zorunlu giriş filtresi.</summary>
    private FilterDefinition<Supplier> TenantFilter()
    {
        return Builders<Supplier>.Filter.And(
            Builders<Supplier>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId),
            Builders<Supplier>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId),
            Builders<Supplier>.Filter.Eq(x => x.IsDeleted, false));
    }
}
