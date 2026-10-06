using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.ManufacturingService.Persistence.Repositories;

/// <summary>
/// BOM okuma deposu. HER sorgu <see cref="Scope"/>'tan geçer: TenantId + LegalEntityId + IsDeleted=false.
/// Başka kiracının/LE'nin kaydı "yok" döner (çağıran 404). Yazma yok — <see cref="BomHistoryJournal"/>.
/// </summary>
public sealed class BomRepository(IMongoDatabase database) : IBomRepository
{
    private readonly IMongoCollection<BomVersion> _boms = database.GetCollection<BomVersion>(BomCollections.Boms);
    private static readonly FilterDefinitionBuilder<BomVersion> F = Builders<BomVersion>.Filter;

    internal static FilterDefinition<BomVersion> Scope(Guid tenantId, Guid legalEntityId, bool includeDeleted = false)
    {
        if (tenantId == Guid.Empty || legalEntityId == Guid.Empty)
        {
            throw new InvalidOperationException("Tenant and legal-entity scope are required for every BOM query.");
        }

        var scope = F.Eq(b => b.TenantId, tenantId) & F.Eq(b => b.LegalEntityId, legalEntityId);
        return includeDeleted ? scope : scope & F.Eq(b => b.IsDeleted, false);
    }

    public async Task<BomVersion?> GetByIdAsync(Guid tenantId, Guid legalEntityId, Guid bomVersionId, CancellationToken ct) =>
        await Guard(() => _boms.Find(Scope(tenantId, legalEntityId) & F.Eq(b => b.Id, bomVersionId)).FirstOrDefaultAsync(ct));

    public async Task<BomVersion?> GetEffectiveAtAsync(Guid tenantId, Guid legalEntityId, Guid itemId, DateTimeOffset at, CancellationToken ct)
    {
        var filter = Scope(tenantId, legalEntityId)
            & F.Eq(b => b.ItemId, itemId)
            & F.In(b => b.Status, [BomStatus.Effective, BomStatus.Superseded])
            & F.Lte(b => b.EffectiveFrom, at)
            & (F.Eq(b => b.EffectiveTo, null) | F.Gt(b => b.EffectiveTo, at));
        return await Guard(() => _boms.Find(filter).SortByDescending(b => b.EffectiveFrom).FirstOrDefaultAsync(ct));
    }

    public async Task<BomVersion?> GetCurrentEffectiveAsync(Guid tenantId, Guid legalEntityId, Guid itemId, CancellationToken ct) =>
        await Guard(() => _boms.Find(Scope(tenantId, legalEntityId) & F.Eq(b => b.ItemId, itemId) & F.Eq(b => b.Status, BomStatus.Effective)).FirstOrDefaultAsync(ct));

    public async Task<IReadOnlyList<BomVersion>> GetCurrentEffectiveForItemsAsync(Guid tenantId, Guid legalEntityId, IReadOnlyCollection<Guid> itemIds, CancellationToken ct) =>
        itemIds.Count == 0
            ? []
            : await Guard(() => _boms.Find(Scope(tenantId, legalEntityId) & F.In(b => b.ItemId, itemIds) & F.Eq(b => b.Status, BomStatus.Effective)).ToListAsync(ct));

    public async Task<int> GetNextRevisionNoAsync(Guid tenantId, Guid legalEntityId, Guid itemId, CancellationToken ct)
    {
        // Silinmiş taslaklar dahil: bir revizyon numarası hiçbir zaman ikinci bir sürüme verilmez.
        var last = await Guard(() => _boms.Find(Scope(tenantId, legalEntityId, includeDeleted: true) & F.Eq(b => b.ItemId, itemId))
            .SortByDescending(b => b.RevisionNo).Project(b => b.RevisionNo).FirstOrDefaultAsync(ct));
        return last + 1;
    }

    public async Task<(IReadOnlyList<BomVersion> Items, long Total)> ListAsync(Guid tenantId, Guid legalEntityId, BomListFilter filter, CancellationToken ct)
    {
        var query = Scope(tenantId, legalEntityId);
        if (filter.ItemId is { } itemId)
        {
            query &= F.Eq(b => b.ItemId, itemId);
        }

        if (filter.Status is { } status)
        {
            query &= F.Eq(b => b.Status, status);
        }

        return await Guard(async () =>
        {
            var total = await _boms.CountDocumentsAsync(query, cancellationToken: ct);
            var items = await _boms.Find(query)
                .Sort(Builders<BomVersion>.Sort.Descending(b => b.UpdatedAt).Descending(b => b.CreatedAt).Descending(b => b.RevisionNo))
                .Skip((filter.Page - 1) * filter.PageSize)
                .Limit(filter.PageSize)
                .ToListAsync(ct);
            return ((IReadOnlyList<BomVersion>)items, total);
        });
    }

    private static async Task<T> Guard<T>(Func<Task<T>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            throw new BomPersistenceUnavailableException("BOM read failed.", ex);
        }
    }
}

internal static class BomCollections
{
    public const string Boms = "mfg_boms";
    public const string History = "mfg_bom_history";
}
