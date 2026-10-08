using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;

namespace Diten.InventoryService.Application.Features.Quarantine.Services;

/// <summary>
/// The facts a MOD-0175 rule needs, read IN-PROCESS through the owners' ports (pack §2 "Consumes", §8): the bucket balance
/// from the MOD-0173 ledger repository, the lot (status, CoA, dates) from the MOD-0174 lot repository, a location from the
/// MOD-0173-FU02 location repository, and the 0175 settings / block reasons. All reads are scoped to the caller's tenant and
/// company by those repositories.
/// </summary>
public sealed class QuarantineFacts
{
    private readonly IInventoryLedgerRepository _ledger;
    private readonly ILotRepository _lots;
    private readonly ILocationRepository _locations;
    private readonly IQuarantineRepository _quarantine;
    private readonly QuarantineSupport _support;

    public QuarantineFacts(
        IInventoryLedgerRepository ledger,
        ILotRepository lots,
        ILocationRepository locations,
        IQuarantineRepository quarantine,
        QuarantineSupport support)
    {
        _ledger = ledger;
        _lots = lots;
        _locations = locations;
        _quarantine = quarantine;
        _support = support;
    }

    /// <summary>The registered lot a ledger lot id names, or null (typed lot text / no lot / unknown).</summary>
    public async Task<Lot?> LotAsync(string? lotId, CancellationToken ct)
        => LotPostingRules.IsRegisteredLotForm(lotId, out var id) ? await _lots.FindAsync(id, ct) : null;

    public async Task<IReadOnlyDictionary<string, Lot>> LotsAsync(IEnumerable<string?> lotIds, CancellationToken ct)
    {
        var ids = lotIds.Select(l => LotPostingRules.IsRegisteredLotForm(l, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, Lot>();
        }

        var lots = await _lots.FindManyAsync(ids, ct);
        return lots.ToDictionary(l => l.Id.ToString("D"), StringComparer.Ordinal);
    }

    /// <summary>
    /// V8 (CT-D4): the lot is Blocked or Expired — a LOT STATUS rule (only destruction), not the availability hold: a retest-held
    /// lot (KQ-H) is not V8-held, its quarantine holds are decided normally (a release sets the new retest date).
    /// </summary>
    public static bool IsHeld(Lot? lot) => lot is not null && lot.Status is LotStatus.Blocked or LotStatus.Expired;

    /// <summary>On-hand of exactly one ledger bucket (the lot is matched exactly; no lot = the lot-less row).</summary>
    public async Task<(decimal OnHand, string? BaseUomId)> BucketAsync(
        Guid itemId, Guid skuId, SkuLevel level, string warehouseId, string locationId, string? lotId, StockStatus status, CancellationToken ct)
    {
        var page = await _ledger.QueryBalancesAsync(new BalanceQuery(itemId, skuId, level, warehouseId, locationId, lotId, status, 1, 200), ct);
        var rows = page.Rows.Where(r => string.Equals(r.LotId, lotId, StringComparison.Ordinal)).ToList();
        return (rows.Sum(r => r.OnHand), rows.Select(r => r.BaseUomId).FirstOrDefault());
    }

    public async Task<IReadOnlySet<string>> ActiveBlockReasonsAsync(CancellationToken ct)
        => (await _quarantine.BlockReasonsAsync(ct)).Where(r => r.IsActive).Select(r => r.Code).ToHashSet(StringComparer.Ordinal);

    /// <summary>The company's settings, or the defaults (KQ-D 10 days, KQ-C four eyes on, CT-Q10 lot gate on) at version 0.</summary>
    public async Task<QuarantineSettings> SettingsAsync(CancellationToken ct)
        => await _quarantine.FindSettingsAsync(ct) ?? new QuarantineSettings
        {
            Id = Guid.NewGuid(),
            TenantId = _support.TenantId,
            LegalEntityId = _support.LegalEntityId,
            Version = 0
        };

    /// <summary>KQ-E / V13: an Active stockable location of this company under the named warehouse.</summary>
    public async Task<bool> IsStockableLocationOfWarehouseAsync(string locationId, string warehouseId, CancellationToken ct)
    {
        if (!QuarantineSupport.TryParseId(locationId, out var id) || !QuarantineSupport.TryParseId(warehouseId, out var warehouse))
        {
            return false;
        }

        var location = await _locations.FindAsync(id, ct);
        return location is not null
               && location.Status == LocationStatus.Active
               && location.IsStockable
               && location.AncestorIds.Contains(warehouse);
    }

    public static DecisionState StateOf(IEnumerable<QuarantineDecisionEvent> events)
    {
        var last = events.OrderBy(e => e.Sequence).LastOrDefault();
        return last?.Kind switch
        {
            null => DecisionState.Draft,
            DecisionEventKind.Submitted => DecisionState.Submitted,
            DecisionEventKind.WorkflowStarted => DecisionState.PendingApproval,
            DecisionEventKind.StartFailed => DecisionState.StartFailed,
            DecisionEventKind.Approved => DecisionState.Approved,
            DecisionEventKind.Rejected => DecisionState.Rejected,
            DecisionEventKind.Cancelled => DecisionState.Cancelled,
            DecisionEventKind.TimedOut => DecisionState.TimedOut,
            DecisionEventKind.Applied => DecisionState.Applied,
            DecisionEventKind.ApplyFailed => DecisionState.ApplyFailed,
            DecisionEventKind.FourEyesViolation => DecisionState.FourEyesViolation,
            DecisionEventKind.Withdrawn => DecisionState.Withdrawn,
            _ => throw new ArgumentOutOfRangeException(nameof(events), last.Kind, "Unknown decision event.")
        };
    }
}
