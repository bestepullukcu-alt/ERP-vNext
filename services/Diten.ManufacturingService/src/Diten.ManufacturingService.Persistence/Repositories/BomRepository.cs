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

    public async Task<BomListPage> ListAsync(Guid tenantId, Guid legalEntityId, BomListFilter filter, CancellationToken ct)
    {
        var scope = Scope(tenantId, legalEntityId);
        var query = scope;
        if (filter.ItemId is { } itemId)
        {
            query &= F.Eq(b => b.ItemId, itemId);
        }

        if (filter.Statuses.Count > 0)
        {
            query &= F.In(b => b.Status, filter.Statuses);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            var text = F.Regex(b => b.Description, new MongoDB.Bson.BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(term), "i"));
            query &= Guid.TryParse(term, out var searchedItem) ? text | F.Eq(b => b.ItemId, searchedItem) : text;
        }

        var sort = Builders<BomVersion>.Sort;
        SortDefinition<BomVersion> primary = filter.OrderBy switch
        {
            BomListOrder.ItemId => filter.Descending ? sort.Descending(b => b.ItemId) : sort.Ascending(b => b.ItemId),
            BomListOrder.Version => filter.Descending ? sort.Descending(b => b.RevisionNo) : sort.Ascending(b => b.RevisionNo),
            BomListOrder.Status => filter.Descending ? sort.Descending(b => b.Status) : sort.Ascending(b => b.Status),
            BomListOrder.Description => filter.Descending ? sort.Descending(b => b.Description) : sort.Ascending(b => b.Description),
            BomListOrder.EffectiveFrom => filter.Descending ? sort.Descending(b => b.EffectiveFrom) : sort.Ascending(b => b.EffectiveFrom),
            _ => filter.Descending ? sort.Descending(b => b.UpdatedAt) : sort.Ascending(b => b.UpdatedAt)
        };
        // A stable tiebreak: the same query always pages the same way.
        var order = sort.Combine(primary, sort.Ascending(b => b.Id));

        return await Guard(async () =>
        {
            var total = await _boms.CountDocumentsAsync(scope, cancellationToken: ct);
            var filtered = await _boms.CountDocumentsAsync(query, cancellationToken: ct);
            var find = _boms.Find(query).Sort(order).Skip(filter.Skip);
            if (filter.Take is { } take)
            {
                find = find.Limit(take);
            }

            return new BomListPage(await find.ToListAsync(ct), total, filtered);
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
