using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Trace;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// WP-0177-R6 — the panel's ledger read (pack §8.9): the window's outbound movements + reversals, ALWAYS bound to the caller's Tenant +
/// LegalEntity (W-0173-02). One query; served by <c>ix_tenant_le_item_postingdate</c>'s Tenant + LegalEntity prefix and the posting date.
/// </summary>
public sealed class RecallDashboardSource : IRecallDashboardSource
{
    private readonly IMongoCollection<InventoryTransaction> _transactions;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public RecallDashboardSource(IMongoDatabase database, ITenantContext tenant)
    {
        _transactions = database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    public async Task<IReadOnlyList<TraceTransaction>> OutboundInWindowAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryTransaction>.Filter;
        var types = TraceRules.Outbound.Append(MovementType.Reversal).ToList();
        var rows = await _transactions.Find(f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId)
                                            & f.Gte(x => x.PostingDate, from) & f.Lte(x => x.PostingDate, to) & f.In(x => x.MovementType, types))
            .ToListAsync(cancellationToken);
        return rows.Select(t => new TraceTransaction(
            t.Id.ToString("D"), t.TransactionNumber, t.MovementType, t.LotId, t.ItemId, t.SkuId, t.SkuLevel, t.BaseQuantity, t.BaseUomId,
            // the panel never reads the source module / document (no KPI decides on them)
            string.Empty, null, t.PostingDate, t.ReversesTransactionId?.ToString("D"), t.SerialIds ?? [])).ToList();
    }
}
