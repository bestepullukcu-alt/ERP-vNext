using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Expiry.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Entities;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Persistence.Traceability;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Expiry;

/// <summary>
/// The FEFO candidate read of one company + warehouse + SKU (pack §8.2): the balance rows of registered lots with on-hand &gt; 0, summed per
/// lot into AVAILABLE vs. held, joined to the lots. ONE reader for the posting hook (KF-B, in the posting session) and the FEFO order read
/// (KF-F) — so both apply <see cref="FefoOrderRule"/> to the same set. Covered by the prefix of <c>ux_balance_dimension</c> (CT-F13).
/// </summary>
public sealed class FefoCandidateReader
{
    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly IMongoCollection<Lot> _lots;
    private readonly ITenantContext _tenant;

    public FefoCandidateReader(IMongoDatabase database, ITenantContext tenant)
    {
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<FefoCandidate>> ReadAsync(
        IClientSessionHandle? session, string warehouseId, Guid itemId, Guid skuId, SkuLevel level, CancellationToken ct)
    {
        var b = Builders<InventoryBalance>.Filter;
        var filter = b.Eq(x => x.TenantId, _tenant.TenantId) & b.Eq(x => x.LegalEntityId, _tenant.LegalEntityId) & b.Eq(x => x.IsDeleted, false)
                     & b.Eq(x => x.ItemId, itemId) & b.Eq(x => x.SkuId, skuId) & b.Eq(x => x.SkuLevel, level) & b.Eq(x => x.WarehouseId, warehouseId)
                     & b.Ne(x => x.LotId, null) & b.Gt(x => x.OnHand, 0m);
        var rows = session is null ? await _balances.Find(filter).ToListAsync(ct) : await _balances.Find(session, filter).ToListAsync(ct);
        var perLot = rows.GroupBy(r => r.LotId!, StringComparer.Ordinal)
            .Select(g => (LotId: g.Key, Available: g.Where(r => r.StockStatus == StockStatus.Available).Sum(r => r.OnHand),
                Held: g.Where(r => r.StockStatus != StockStatus.Available).Sum(r => r.OnHand)))
            .ToList();
        var lots = await LotsAsync(session, perLot.Select(p => p.LotId), ct);
        return perLot.Where(p => lots.ContainsKey(p.LotId)).Select(p => new FefoCandidate(lots[p.LotId], p.Available, p.Held)).ToList();
    }

    internal async Task<Dictionary<string, Lot>> LotsAsync(IClientSessionHandle? session, IEnumerable<string> lotIds, CancellationToken ct)
    {
        var ids = lotIds.Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<string, Lot>(StringComparer.Ordinal);
        var filter = Builders<Lot>.Filter.Eq(x => x.TenantId, _tenant.TenantId) & Builders<Lot>.Filter.In(x => x.Id, ids);
        var lots = session is null ? await _lots.Find(filter).ToListAsync(ct) : await _lots.Find(session, filter).ToListAsync(ct);
        return lots.ToDictionary(l => l.Id.ToString("D"), StringComparer.Ordinal);
    }
}

/// <summary>WP-0176-F4 — <see cref="IExpiryReadModel"/>: tenant + company scoped reads of balances, lots, snapshots and cases.</summary>
public sealed class ExpiryReadModel : IExpiryReadModel
{
    private static readonly ExpiryExceptionStatus[] Active = [ExpiryExceptionStatus.Open, ExpiryExceptionStatus.InReview];

    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly IMongoCollection<FefoIssueSnapshot> _snapshots;
    private readonly IMongoCollection<FefoExceptionCase> _cases;
    private readonly FefoCandidateReader _candidates;
    private readonly ITenantContext _tenant;

    public ExpiryReadModel(IMongoDatabase database, FefoCandidateReader candidates, ITenantContext tenant)
    {
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _snapshots = database.GetCollection<FefoIssueSnapshot>(ExpiryCollections.FefoIssueSnapshots);
        _cases = database.GetCollection<FefoExceptionCase>(ExpiryCollections.Exceptions);
        _candidates = candidates;
        _tenant = tenant;
    }

    private FilterDefinition<T> Scope<T>() where T : EntityBase
        => Builders<T>.Filter.Eq(x => x.TenantId, _tenant.TenantId) & Builders<T>.Filter.Eq(x => x.LegalEntityId, _tenant.LegalEntityId)
           & Builders<T>.Filter.Eq(x => x.IsDeleted, false);

    public async Task<IReadOnlyList<LotStockRow>> LotStockAsync(string? warehouseId, Guid? skuId, CancellationToken cancellationToken = default)
    {
        var b = Builders<InventoryBalance>.Filter;
        var filter = Scope<InventoryBalance>() & b.Ne(x => x.LotId, null) & b.Gt(x => x.OnHand, 0m);
        if (warehouseId is not null) filter &= b.Eq(x => x.WarehouseId, warehouseId);
        if (skuId is not null) filter &= b.Eq(x => x.SkuId, skuId.Value);
        var rows = await _balances.Find(filter).ToListAsync(cancellationToken);
        var lots = await _candidates.LotsAsync(null, rows.Select(r => r.LotId!), cancellationToken);
        return rows.Where(r => r.WarehouseId is not null && lots.ContainsKey(r.LotId!))
            .GroupBy(r => (LotId: r.LotId!, WarehouseId: r.WarehouseId!))
            .Select(g => new LotStockRow(
                lots[g.Key.LotId],
                g.Key.WarehouseId,
                g.First().BaseUomId,
                g.Where(r => r.StockStatus == StockStatus.Available).Sum(r => r.OnHand),
                g.Where(r => r.StockStatus != StockStatus.Available).Sum(r => r.OnHand),
                g.Where(r => r.StockStatus != StockStatus.Available).Select(r => r.StockStatus).Distinct().ToList()))
            .ToList();
    }

    public Task<IReadOnlyList<FefoCandidate>> FefoCandidatesAsync(string warehouseId, Guid itemId, Guid skuId, SkuLevel skuLevel, CancellationToken cancellationToken = default)
        => _candidates.ReadAsync(null, warehouseId, itemId, skuId, skuLevel, cancellationToken);

    public async Task<(int Issues, int Compliant)> FefoIssuesAsync(DateTimeOffset from, DateTimeOffset to, string? warehouseId, CancellationToken cancellationToken = default)
    {
        var s = Builders<FefoIssueSnapshot>.Filter;
        var filter = Scope<FefoIssueSnapshot>() & s.Gte(x => x.PostedAtUtc, from) & s.Lt(x => x.PostedAtUtc, to);
        if (warehouseId is not null) filter &= s.Eq(x => x.WarehouseId, warehouseId);
        var issues = (int)await _snapshots.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var compliant = (int)await _snapshots.CountDocumentsAsync(filter & s.Eq(x => x.Compliant, true), cancellationToken: cancellationToken);
        return (issues, compliant);
    }

    public async Task<ExpiryExceptionFigures> ExceptionFiguresAsync(DateTimeOffset now, DateTimeOffset periodFrom, string? warehouseId, CancellationToken cancellationToken = default)
    {
        var c = Builders<FefoExceptionCase>.Filter;
        var scope = Scope<FefoExceptionCase>();
        if (warehouseId is not null) scope &= c.Eq(x => x.WarehouseId, warehouseId);
        var active = scope & c.In(x => x.Status, Active);
        var open = (int)await _cases.CountDocumentsAsync(active, cancellationToken: cancellationToken);
        var late = (int)await _cases.CountDocumentsAsync(active & c.Lt(x => x.DueAtUtc, now), cancellationToken: cancellationToken);
        var resolved = await _cases.Find(scope & c.Ne(x => x.ResolvedAtUtc, null) & c.Gte(x => x.ResolvedAtUtc, periodFrom) & c.Lte(x => x.ResolvedAtUtc, now))
            .Project(x => new { x.OpenedAtUtc, x.ResolvedAtUtc }).ToListAsync(cancellationToken);
        double? avg = resolved.Count == 0 ? null : resolved.Average(r => (r.ResolvedAtUtc!.Value - r.OpenedAtUtc).TotalHours);
        return new ExpiryExceptionFigures(open, late, avg);
    }

    public async Task<IReadOnlyDictionary<(string LotId, string? WarehouseId), Guid>> ActiveCasesAsync(CancellationToken cancellationToken = default)
    {
        var cases = await _cases.Find(Scope<FefoExceptionCase>() & Builders<FefoExceptionCase>.Filter.In(x => x.Status, Active))
            .Project(x => new { x.Id, x.LotId, x.WarehouseId, x.Type, x.OpenedAtUtc }).ToListAsync(cancellationToken);
        return cases.GroupBy(x => (x.LotId, x.WarehouseId))
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Type == ExpiryExceptionType.ExpiredOnHand ? 0 : 1).ThenBy(x => x.OpenedAtUtc).First().Id);
    }
}
