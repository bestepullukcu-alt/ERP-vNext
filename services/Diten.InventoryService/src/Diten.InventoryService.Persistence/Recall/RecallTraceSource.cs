using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Trace;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Persistence.Traceability;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// The in-process reads of the MOD-0177 trace engine (pack §8.2) — READ ONLY. Lots and genealogy come through the MOD-0174 lot store
/// (tenant-wide by its own contract, CT-D3); the ledger collections are read directly, always bound to the caller's Tenant + LegalEntity
/// (CT-R8) — the single exception is <see cref="OtherCompanyActivityAsync"/>, which answers a COUNT for the other companies and nothing
/// else. Every member takes a whole batch: one query per call.
/// </summary>
public sealed class RecallTraceSource : IRecallTraceSource
{
    private readonly ILotRepository _lots;
    private readonly IProductMasterClient _productMaster;
    private readonly IMongoCollection<InventoryTransaction> _transactions;
    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly IMongoCollection<SerialNumber> _serials;
    private readonly IMongoCollection<MovementLotLink> _typedLinks;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public RecallTraceSource(IMongoDatabase database, ITenantContext tenant, ILotRepository lots, IProductMasterClient productMaster)
    {
        _lots = lots;
        _productMaster = productMaster;
        _transactions = database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions);
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _serials = database.GetCollection<SerialNumber>(TraceCollections.Serials);
        _typedLinks = database.GetCollection<MovementLotLink>(TraceCollections.MovementLotLinks);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    private FilterDefinition<InventoryTransaction> Company
        => Builders<InventoryTransaction>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId) & Builders<InventoryTransaction>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId);

    public async Task<IReadOnlyList<TraceLot>> LotsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => lotIds.Count == 0
            ? []
            : (await _lots.FindManyAsync(lotIds, cancellationToken))
                .Select(l => new TraceLot(l.Id, l.LotNumber, l.ItemId, l.SkuId, l.SkuLevel, l.TrackingMode == LotTrackingMode.LotAndSerial)).ToList();

    public async Task<IReadOnlyList<TraceLink>> ParentLinksAsync(IReadOnlyCollection<Guid> childLotIds, CancellationToken cancellationToken = default)
        => Links(await _lots.ParentLinksAsync(childLotIds, cancellationToken));

    public async Task<IReadOnlyList<TraceLink>> ChildLinksAsync(IReadOnlyCollection<Guid> parentLotIds, CancellationToken cancellationToken = default)
        => Links(await _lots.ChildLinksAsync(parentLotIds, cancellationToken));

    public async Task<IReadOnlyDictionary<Guid, TraceTransaction>> FirstInboundAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
    {
        if (lotIds.Count == 0) return new Dictionary<Guid, TraceTransaction>();
        var f = Builders<InventoryTransaction>.Filter;
        var rows = await _transactions.Find(Company & f.In(x => x.LotId, lotIds.Select(Id)) & f.In(x => x.MovementType, TraceRules.Inbound))
            .Sort(Builders<InventoryTransaction>.Sort.Ascending(x => x.TransactionDate).Ascending(x => x.CreatedAt).Ascending(x => x.Id))
            .ToListAsync(cancellationToken);
        var first = new Dictionary<Guid, TraceTransaction>();
        foreach (var row in rows)
        {
            if (Guid.TryParseExact(row.LotId, "D", out var lot) && !first.ContainsKey(lot)) first[lot] = Map(row);
        }

        return first;
    }

    public async Task<IReadOnlyList<TraceTransaction>> TransactionsOfLotsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
        => lotIds.Count == 0
            ? []
            : (await _transactions.Find(Company & Builders<InventoryTransaction>.Filter.In(x => x.LotId, lotIds.Select(Id))).ToListAsync(cancellationToken))
                .Select(Map).ToList();

    public async Task<IReadOnlyList<TraceBalance>> BalancesAsync(IReadOnlyCollection<Guid> itemIds, IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
    {
        if (lotIds.Count == 0) return [];
        var f = Builders<InventoryBalance>.Filter;
        var rows = await _balances.Find(f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
                                        & f.In(x => x.ItemId, itemIds) & f.In(x => x.LotId, lotIds.Select(Id)) & f.Gt(x => x.OnHand, 0m))
            .ToListAsync(cancellationToken);
        return rows.Select(b => new TraceBalance(b.LotId!, b.ItemId, b.SkuId, b.WarehouseId, b.LocationId, b.StockStatus, b.OnHand, b.BaseUomId)).ToList();
    }

    public async Task<IReadOnlyList<TraceTransaction>> TransactionsOfDocumentAsync(string sourceModule, string sourceDocumentId, CancellationToken cancellationToken = default)
        => (await _transactions.Find(Company & Builders<InventoryTransaction>.Filter.Eq(x => x.SourceModule, sourceModule)
                                     & Builders<InventoryTransaction>.Filter.Eq(x => x.SourceDocumentId, sourceDocumentId)).ToListAsync(cancellationToken))
            .Select(Map).ToList();

    public async Task<IReadOnlyList<TraceTransaction>> OutboundOfSkuAsync(Guid itemId, Guid skuId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryTransaction>.Filter;
        var types = TraceRules.Outbound.Append(MovementType.Reversal).ToList();
        return (await _transactions.Find(Company & f.Eq(x => x.ItemId, itemId) & f.Gte(x => x.PostingDate, from) & f.Lte(x => x.PostingDate, to)
                                         & f.Eq(x => x.SkuId, skuId) & f.In(x => x.MovementType, types)).ToListAsync(cancellationToken))
            .Select(Map).ToList();
    }

    public async Task<IReadOnlyDictionary<string, Guid>> TypedLotLinksAsync(IReadOnlyCollection<string> transactionIds, CancellationToken cancellationToken = default)
    {
        if (transactionIds.Count == 0) return new Dictionary<string, Guid>();
        var f = Builders<MovementLotLink>.Filter;
        var links = await _typedLinks.Find(f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.In(x => x.TransactionId, transactionIds)).ToListAsync(cancellationToken);
        return links.GroupBy(l => l.TransactionId, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().LotId, StringComparer.Ordinal);
    }

    public async Task<long> OtherCompanyActivityAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
    {
        if (lotIds.Count == 0) return 0;
        var f = Builders<InventoryTransaction>.Filter;
        return await _transactions.CountDocumentsAsync(f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.Ne(x => x.LegalEntityId, (Guid?)_legalEntityId)
                                                       & f.In(x => x.LotId, lotIds.Select(Id)), cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<TraceSerial>> SerialsAsync(IReadOnlyCollection<Guid> lotIds, CancellationToken cancellationToken = default)
    {
        if (lotIds.Count == 0) return [];
        var f = Builders<SerialNumber>.Filter;
        var rows = await _serials.Find(f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.Eq(x => x.IsDeleted, false) & f.In(x => x.LotId, lotIds.Select(l => (Guid?)l)))
            .ToListAsync(cancellationToken);
        return rows.Select(s => new TraceSerial(s.LotId!.Value, s.Number, s.WarehouseId, s.LocationId, s.Status == SerialStatus.Active)).ToList();
    }

    public async Task<bool?> LotControlledAsync(Guid skuId, SkuLevel level, CancellationToken cancellationToken = default)
    {
        try
        {
            return (await _productMaster.GetSkuAsync(skuId, level, cancellationToken))?.LotControlled;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null; // PRODUCT-MASTER unreachable: unknown → treated as lot-controlled by the engine (conservative)
        }
    }

    private static string Id(Guid id) => id.ToString("D");

    private static IReadOnlyList<TraceLink> Links(IReadOnlyList<LotGenealogyLink> links)
        => links.Select(l => new TraceLink(l.ParentLotId, l.ChildLotId, l.TransformationReference, l.QuantityConsumed, l.UomId)).ToList();

    private static TraceTransaction Map(InventoryTransaction t) => new(
        t.Id.ToString("D"), t.TransactionNumber, t.MovementType, t.LotId, t.ItemId, t.SkuId, t.SkuLevel, t.BaseQuantity, t.BaseUomId, t.SourceModule,
        t.SourceDocumentId, t.PostingDate, t.ReversesTransactionId?.ToString("D"), t.SerialIds ?? []);
}
