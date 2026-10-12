using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>
/// MOD-0174 serial store — read side (slice P1). Serials are tenant-wide like lots (CT-D7); every query starts from
/// Tenant + IsDeleted = false. Writing serials is the ledger posting's (slice P2); there is no delete.
/// </summary>
public sealed class SerialRepository : ISerialRepository
{
    private readonly IMongoCollection<SerialNumber> _serials;
    private readonly Guid _tenantId;

    public SerialRepository(IMongoDatabase database, ITenantContext tenantContext)
    {
        _serials = database.GetCollection<SerialNumber>(TraceCollections.Serials);
        _tenantId = tenantContext.TenantId;
    }

    public async Task<SerialNumber?> FindAsync(Guid serialId, CancellationToken cancellationToken = default)
        => await _serials.Find(Scope() & Builders<SerialNumber>.Filter.Eq(x => x.Id, serialId)).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SerialNumber>> FindManyAsync(IReadOnlyCollection<Guid> serialIds, CancellationToken cancellationToken = default)
        => serialIds.Count == 0
            ? []
            : await _serials.Find(Scope() & Builders<SerialNumber>.Filter.In(x => x.Id, serialIds)).ToListAsync(cancellationToken);

    public async Task<SerialPage> ListAsync(SerialListQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<SerialNumber>.Filter;
        var filter = Scope();
        if (query.Search is { } search) filter &= f.Regex(x => x.Number, new MongoDB.Bson.BsonRegularExpression(Regex.Escape(search), "i"));
        if (query.SkuId.HasValue) filter &= f.Eq(x => x.SkuId, query.SkuId.Value);
        if (query.SkuLevel.HasValue) filter &= f.Eq(x => x.SkuLevel, query.SkuLevel.Value);
        if (query.LotId.HasValue) filter &= f.Eq(x => x.LotId, query.LotId.Value);
        if (query.StoredStatus.HasValue) filter &= f.Eq(x => x.Status, query.StoredStatus.Value);
        if (query.InsideLotIds is { } inside) filter &= f.In(x => x.LotId, inside.Select(id => (Guid?)id));
        if (query.OutsideLotIds is { Count: > 0 } outside) filter &= f.Nin(x => x.LotId, outside.Select(id => (Guid?)id));

        var total = await _serials.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _serials.Find(filter)
            .Sort(Builders<SerialNumber>.Sort.Ascending(x => x.Number).Ascending(x => x.Id))
            .Skip((query.Page - 1) * query.PageSize)
            .Limit(query.PageSize)
            .ToListAsync(cancellationToken);
        return new SerialPage(items, total);
    }

    private FilterDefinition<SerialNumber> Scope()
        => Builders<SerialNumber>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId) & Builders<SerialNumber>.Filter.Eq(x => x.IsDeleted, false);
}
