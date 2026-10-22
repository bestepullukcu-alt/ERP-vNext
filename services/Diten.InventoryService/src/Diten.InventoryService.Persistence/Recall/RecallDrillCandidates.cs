using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Recall.Drills;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Persistence.Traceability;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// WP-0177-R5 — <see cref="IRecallDrillCandidates"/>: READ ONLY (Find / Distinct, nothing else). The ledger is read bound to the caller's
/// Tenant + LegalEntity; lots and genealogy are tenant-wide by the MOD-0174 contract (CT-D3), so a candidate is always a lot the COMPANY moved:
/// <list type="bullet">
/// <item>finished good = the company received it by <c>GOODS_RECEIPT_PROD</c> (pack §8.2 lot kind) and issued it by <c>GOODS_ISSUE_SALES</c>
/// posted on / after <c>since</c>;</item>
/// <item>raw material = the company received it by <c>GOODS_RECEIPT_PO</c> and it is the parent of a genealogy link (it has children).</item>
/// </list>
/// WP-MVP1-FU-CLEAN1 (O-R5-5): ONE lot kind per lot, at the source — a lot the company also received by <c>GOODS_RECEIPT_PROD</c> is a finished
/// good (pack §8.2), never a raw-material candidate as well; the two lists never share a lot.
/// Ordered by lot id (ordinal): the same data + the same seed gives the same pick.
/// </summary>
public sealed class RecallDrillCandidates : IRecallDrillCandidates
{
    private readonly IMongoCollection<InventoryTransaction> _transactions;
    private readonly IMongoCollection<Lot> _lots;
    private readonly IMongoCollection<LotGenealogyLink> _links;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public RecallDrillCandidates(IMongoDatabase database, ITenantContext tenant)
    {
        _transactions = database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions);
        _lots = database.GetCollection<Lot>(TraceCollections.Lots);
        _links = database.GetCollection<LotGenealogyLink>(TraceCollections.GenealogyLinks);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    private FilterDefinition<InventoryTransaction> Company
        => Builders<InventoryTransaction>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId) & Builders<InventoryTransaction>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
           & Builders<InventoryTransaction>.Filter.Ne(x => x.LotId, null);

    public async Task<IReadOnlyList<RecallDrillLot>> FinishedGoodLotsAsync(DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryTransaction>.Filter;
        var issued = await LotIdsAsync(Company & f.Eq(x => x.MovementType, MovementType.GoodsIssueSales) & f.Gte(x => x.PostingDate, since), cancellationToken);
        if (issued.Count == 0) return [];
        var produced = await LotIdsAsync(Company & f.Eq(x => x.MovementType, MovementType.GoodsReceiptProd) & f.In(x => x.LotId, issued), cancellationToken);
        return await NamedAsync(produced, cancellationToken);
    }

    public async Task<IReadOnlyList<RecallDrillLot>> RawMaterialLotsAsync(CancellationToken cancellationToken = default)
    {
        var l = Builders<LotGenealogyLink>.Filter;
        var parents = (await (await _links.DistinctAsync(x => x.ParentLotId, l.Eq(x => x.TenantId, (Guid?)_tenantId) & l.Eq(x => x.IsDeleted, false),
                cancellationToken: cancellationToken)).ToListAsync(cancellationToken))
            .Select(id => id.ToString("D")).ToList();
        if (parents.Count == 0) return [];
        var f = Builders<InventoryTransaction>.Filter;
        var received = await LotIdsAsync(Company & f.Eq(x => x.MovementType, MovementType.GoodsReceiptPo) & f.In(x => x.LotId, parents), cancellationToken);
        if (received.Count == 0) return [];
        // a produced lot is a finished good (pack §8.2 lot kind) — it is not offered as a raw material too (O-R5-5)
        var produced = (await LotIdsAsync(Company & f.Eq(x => x.MovementType, MovementType.GoodsReceiptProd) & f.In(x => x.LotId, received), cancellationToken))
            .ToHashSet(StringComparer.Ordinal);
        return await NamedAsync(received.Where(id => !produced.Contains(id)).ToList(), cancellationToken);
    }

    public async Task<RecallDrillLot?> LotAsync(Guid lotId, CancellationToken cancellationToken = default)
    {
        var f = Builders<Lot>.Filter;
        var lot = await _lots.Find(f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.Eq(x => x.IsDeleted, false) & f.Eq(x => x.Id, lotId)).FirstOrDefaultAsync(cancellationToken);
        return lot is null ? null : new RecallDrillLot(lot.Id.ToString("D"), lot.LotNumber);
    }

    private async Task<List<string>> LotIdsAsync(FilterDefinition<InventoryTransaction> filter, CancellationToken ct)
        => (await (await _transactions.DistinctAsync(x => x.LotId, filter, cancellationToken: ct)).ToListAsync(ct)).OfType<string>().ToList();

    private async Task<IReadOnlyList<RecallDrillLot>> NamedAsync(IReadOnlyCollection<string> lotIds, CancellationToken ct)
    {
        var ids = lotIds.Select(id => Guid.TryParseExact(id, "D", out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
        if (ids.Count == 0) return [];
        var f = Builders<Lot>.Filter;
        var lots = await _lots.Find(f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.Eq(x => x.IsDeleted, false) & f.In(x => x.Id, ids)).ToListAsync(ct);
        return lots.Select(l => new RecallDrillLot(l.Id.ToString("D"), l.LotNumber)).OrderBy(l => l.LotId, StringComparer.Ordinal).ToList();
    }
}
