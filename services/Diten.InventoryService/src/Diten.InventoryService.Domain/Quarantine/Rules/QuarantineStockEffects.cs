using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;

namespace Diten.InventoryService.Domain.Quarantine.Rules;

/// <summary>One ledger movement a decision or a no-workflow action posts (pack §8 stock-effect table).</summary>
public sealed record PlannedMovement(
    int LineNo,
    MovementType MovementType,
    StockStatus? FromStockStatus,
    StockStatus? ToStockStatus,
    string FromWarehouseId,
    string FromLocationId,
    string? ToWarehouseId,
    string? ToLocationId,
    decimal Quantity,
    IReadOnlyList<string>? SerialIds = null);

/// <summary>
/// MOD-0175 pack §8 stock-effect table — the ONLY place where a quarantine decision becomes ledger movements. Every value
/// is an INVENTORY-BUNDLE <see cref="MovementType"/> / <see cref="StockStatus"/> (DEC-INV-08: 0175 owns no status and no
/// movement type; KQ-E: "Numune / Referans" is a LOCATION_TRANSFER that keeps BLOCKED, never a status).
/// <list type="table">
/// <item>Karantinaya Al (manual): STATUS_TRANSFER AVAILABLE → QUARANTINE.</item>
/// <item>Stoğu bloke et (manual): STATUS_TRANSFER AVAILABLE → BLOCKED.</item>
/// <item>Release / Reject / Block line: STATUS_TRANSFER bucket status → AVAILABLE / BLOCKED / BLOCKED; partial = one per line.</item>
/// <item>Destroy: SCRAP from BLOCKED. Return to supplier: SUPPLIER_RETURN from BLOCKED.</item>
/// <item>Rework: STATUS_TRANSFER BLOCKED → QUARANTINE. Sample: LOCATION_TRANSFER BLOCKED → sample location (status kept).</item>
/// <item>Unblock: STATUS_TRANSFER BLOCKED → AVAILABLE.</item>
/// </list>
/// </summary>
public static class QuarantineStockEffects
{
    /// <summary>
    /// FU-0175-Q1-SER / ASSUMPTION-0175-Q2-01: the serials of a serial-tracked record are split over the parts of a decision in
    /// their stored (intake) order — the first <c>quantity</c> serials go to the first part, the next to the second. The split is
    /// written on the draft, so the decision (the signed subject) never changes afterwards. Null when the record has no serials.
    /// </summary>
    public static IReadOnlyList<List<string>?> SplitSerials(IReadOnlyList<string>? serials, IReadOnlyList<decimal> quantities)
    {
        if (serials is null || serials.Count == 0)
        {
            return quantities.Select(_ => (List<string>?)null).ToList();
        }

        var parts = new List<List<string>?>();
        var taken = 0;
        foreach (var quantity in quantities)
        {
            var count = (int)quantity;
            parts.Add(serials.Skip(taken).Take(count).ToList());
            taken += count;
        }

        return parts;
    }

    /// <summary>The stock statuses a hold sits in (intake observer + V7).</summary>
    public static readonly IReadOnlyList<StockStatus> HoldStatuses = [StockStatus.Quarantine, StockStatus.QualityInspection];

    /// <summary>Target stock status of a hold-decision line.</summary>
    public static readonly IReadOnlyDictionary<LineDecision, StockStatus> LineTarget = new Dictionary<LineDecision, StockStatus>
    {
        [LineDecision.Release] = StockStatus.Available,
        [LineDecision.Reject] = StockStatus.Blocked,
        [LineDecision.Block] = StockStatus.Blocked
    };

    /// <summary>Movement type of a disposition of BLOCKED stock.</summary>
    public static readonly IReadOnlyDictionary<DispositionKind, MovementType> DispositionMovement = new Dictionary<DispositionKind, MovementType>
    {
        [DispositionKind.Destroy] = MovementType.Scrap,
        [DispositionKind.ReturnToSupplier] = MovementType.SupplierReturn,
        [DispositionKind.Rework] = MovementType.StatusTransfer,
        [DispositionKind.Sample] = MovementType.LocationTransfer
    };

    public static PlannedMovement ManualQuarantine(string warehouseId, string locationId, decimal quantity, int lineNo)
        => StatusTransfer(lineNo, StockStatus.Available, StockStatus.Quarantine, warehouseId, locationId, quantity);

    public static PlannedMovement ManualBlock(string warehouseId, string locationId, decimal quantity, int lineNo)
        => StatusTransfer(lineNo, StockStatus.Available, StockStatus.Blocked, warehouseId, locationId, quantity);

    /// <summary>The movements an approved decision posts, in line order.</summary>
    public static IReadOnlyList<PlannedMovement> Plan(QuarantineDecision decision) => decision.Kind switch
    {
        DecisionKind.HoldDecision => decision.Lines
            .OrderBy(l => l.LineNo)
            .Select(l => StatusTransfer(l.LineNo, decision.FromStockStatus, LineTarget[l.Decision], decision.WarehouseId, decision.LocationId, l.Quantity) with { SerialIds = l.SerialIds })
            .ToList(),
        DecisionKind.Disposition => [Disposition(decision)],
        DecisionKind.Unblock =>
            [StatusTransfer(1, StockStatus.Blocked, StockStatus.Available, decision.WarehouseId, decision.LocationId, decision.AvailableQuantityAtDecision) with { SerialIds = decision.SerialIds }],
        // KQ-I lot release moves no stock (slice Q4).
        DecisionKind.LotRelease => [],
        _ => throw new ArgumentOutOfRangeException(nameof(decision), decision.Kind, "Unknown decision kind.")
    };

    private static PlannedMovement Disposition(QuarantineDecision decision)
    {
        var d = decision.Disposition ?? throw new InvalidOperationException("A disposition decision carries its disposition.");
        var type = DispositionMovement[d.Kind];
        return DispositionMovementOf(decision, d, type) with { SerialIds = d.SerialIds };
    }

    private static PlannedMovement DispositionMovementOf(QuarantineDecision decision, DecisionDisposition d, MovementType type)
    {
        return d.Kind switch
        {
            DispositionKind.Destroy or DispositionKind.ReturnToSupplier =>
                new PlannedMovement(1, type, StockStatus.Blocked, null, decision.WarehouseId, decision.LocationId, null, null, d.Quantity),
            DispositionKind.Rework =>
                StatusTransfer(1, StockStatus.Blocked, StockStatus.Quarantine, decision.WarehouseId, decision.LocationId, d.Quantity),
            DispositionKind.Sample =>
                new PlannedMovement(1, type, StockStatus.Blocked, StockStatus.Blocked, decision.WarehouseId, decision.LocationId,
                    decision.WarehouseId, d.SampleLocationId ?? throw new InvalidOperationException("Sample disposition without a sample location (V13)."),
                    d.Quantity),
            _ => throw new ArgumentOutOfRangeException(nameof(decision), d.Kind, "Unknown disposition kind.")
        };
    }

    private static PlannedMovement StatusTransfer(int lineNo, StockStatus from, StockStatus to, string warehouseId, string locationId, decimal quantity)
        => new(lineNo, MovementType.StatusTransfer, from, to, warehouseId, locationId, warehouseId, locationId, quantity);
}
