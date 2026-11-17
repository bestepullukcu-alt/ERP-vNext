using System.Text.RegularExpressions;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.WavePlanning.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Domain.WavePlanning.Rules;
using Diten.InventoryService.Persistence.Warehouse;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.WavePlanning;

/// <summary>
/// MOD-0180's reads of MOD-0178's outbounds and of LOCATION (pack §2.3 rule 2, AC-16): <c>Find</c> ONLY, through the collection constants —
/// no write of any kind and none of MOD-0178's repositories. Every read starts from the caller's Tenant + LegalEntity. A Mongo failure is
/// <see cref="WaveInventoryUnavailableException"/> (503, nothing written).
/// </summary>
public sealed class WaveInventoryReader : IWaveInventoryReader
{
    private readonly IMongoCollection<WaveOutboundDocument> _outbounds;
    private readonly IMongoCollection<Location> _locations;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public WaveInventoryReader(IMongoDatabase database, ITenantContext tenant)
    {
        _outbounds = database.GetCollection<WaveOutboundDocument>(WarehouseCollections.OutboundShipments);
        _locations = database.GetCollection<Location>(InventoryCollections.Locations);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    private FilterDefinition<T> Company<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _legalEntityId)
           & Builders<T>.Filter.Eq(x => x.IsDeleted, false);

    public async Task<WaveWarehouse?> WarehouseAsync(Guid warehouseId, CancellationToken cancellationToken = default)
    {
        var f = Builders<Location>.Filter;
        var node = await Read(() => _locations.Find(Company<Location>() & f.Eq(x => x.Id, warehouseId) & f.Eq(x => x.Type, LocationType.Warehouse)
                                                    & f.Eq(x => x.Status, LocationStatus.Active)).FirstOrDefaultAsync(cancellationToken));
        return node is null ? null : new WaveWarehouse(node.Id, node.Code);
    }

    public async Task<IReadOnlyList<WaveOutbound>> OutboundsAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken cancellationToken = default)
    {
        if (outboundIds.Count == 0) return [];
        var documents = await Read(() => _outbounds.Find(Company<WaveOutboundDocument>() & Builders<WaveOutboundDocument>.Filter.In(x => x.Id, outboundIds))
            .ToListAsync(cancellationToken));
        return documents.Select(Map).ToList();
    }

    public async Task<WaveCandidatePage> CandidatesAsync(WaveCandidateQuery query, CancellationToken cancellationToken = default)
    {
        var f = Builders<WaveOutboundDocument>.Filter;
        var eligible = Deferred();
        if (query.ExcludedOutboundIds.Count > 0) eligible &= f.Nin(x => x.Id, query.ExcludedOutboundIds);
        var state = query.IncludeAllocating ? f.Or(eligible, f.Eq(x => x.Status, WaveRules.AllocatingOutboundStatus)) : eligible;
        var filter = Company<WaveOutboundDocument>() & f.Eq(x => x.WarehouseId, query.WarehouseId) & state;
        if (query.Search is not null)
        {
            var text = Contains(query.Search);
            filter &= f.Or(f.Regex(x => x.OutboundNumber, text), f.Regex(x => x.OrderRef, text));
        }

        if (query.ShipTo is not null)
        {
            var text = Contains(query.ShipTo);
            filter &= f.Or(f.Regex(x => x.ShipTo.Name, text), f.Regex(x => x.ShipTo.City, text), f.Regex(x => x.ShipTo.PostalCode, text),
                f.Regex(x => x.ShipTo.Country, text));
        }

        var total = await Read(() => _outbounds.Find(filter).CountDocumentsAsync(cancellationToken));
        var documents = await Read(() => _outbounds.Find(filter)
            .Sort(Builders<WaveOutboundDocument>.Sort.Ascending(x => x.CreatedAt).Ascending(x => x.OutboundNumber).Ascending(x => x.Id))
            .Skip((query.Page - 1) * query.PageSize).Limit(query.PageSize).ToListAsync(cancellationToken));
        return new WaveCandidatePage(documents.Select(Map).ToList(), total);
    }

    public async Task<long> DeferredCountAsync(Guid warehouseId, CancellationToken cancellationToken = default)
        => await Read(() => _outbounds.Find(Company<WaveOutboundDocument>() & Builders<WaveOutboundDocument>.Filter.Eq(x => x.WarehouseId, warehouseId) & Deferred())
            .CountDocumentsAsync(cancellationToken));

    public async Task<IReadOnlyList<WaveBin>> BinsAsync(IReadOnlyCollection<Guid> binIds, CancellationToken cancellationToken = default)
    {
        if (binIds.Count == 0) return [];
        var f = Builders<Location>.Filter;
        var bins = await Read(() => _locations.Find(Company<Location>() & f.In(x => x.Id, binIds)).ToListAsync(cancellationToken));
        var ancestorIds = bins.SelectMany(b => b.AncestorIds).Distinct().ToList();
        var ancestors = ancestorIds.Count == 0
            ? new Dictionary<Guid, Location>()
            : (await Read(() => _locations.Find(Company<Location>() & f.In(x => x.Id, ancestorIds)).ToListAsync(cancellationToken))).ToDictionary(a => a.Id);
        return bins.Select(b =>
        {
            var path = b.AncestorIds.Select(id => ancestors.GetValueOrDefault(id)).Where(a => a is not null).Select(a => a!).ToList();
            var zone = path.LastOrDefault(a => a.Type == LocationType.Zone) ?? path.LastOrDefault(a => a.Type == LocationType.StorageLocation);
            return new WaveBin(b.Id, b.Code, zone?.Code ?? string.Empty);
        }).ToList();
    }

    /// <summary>Pack §8.2 rule 2 / §4.5: <c>Status = Allocated</c> AND <c>PickIssuance.State = Deferred</c> — both, because a cancelled outbound keeps its mark.</summary>
    private static FilterDefinition<WaveOutboundDocument> Deferred()
        => Builders<WaveOutboundDocument>.Filter.Eq(x => x.Status, WaveRules.EligibleOutboundStatus)
           & Builders<WaveOutboundDocument>.Filter.Eq(x => x.PickIssuance!.State, WaveRules.DeferredPickIssuance);

    private static BsonRegularExpression Contains(string text) => new(Regex.Escape(text), "i");

    private static WaveOutbound Map(WaveOutboundDocument d)
    {
        var allocations = d.Lines.SelectMany(l => l.Allocations).ToList();
        var bins = allocations.SelectMany(a => a.PlannedBins).Select(b => b.LocationId).ToList();
        return new WaveOutbound(d.Id, d.OutboundNumber, d.WarehouseId, d.OrderRef, new WaveShipTo(d.ShipTo.Name, d.ShipTo.Country, d.ShipTo.City, d.ShipTo.PostalCode),
            d.Status, d.PickIssuance?.State, d.Lines.Count, allocations.Count, bins.Count, bins, d.CreatedAt);
    }

    private static async Task<T> Read<T>(Func<Task<T>> read)
    {
        try
        {
            return await read();
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            throw new WaveInventoryUnavailableException("The warehouse data could not be read.", ex);
        }
    }
}
