using System.Globalization;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Rules;

/// <summary>A lot (or, lotless, the warehouse) the plan may take from: its FEFO rank among the FREE lots and its allocatable quantity.</summary>
public sealed record AllocationSource(string? LotId, string? LotNumber, int? FefoRank, decimal Allocatable);

/// <summary>One bin of the pick plan: its location code (the plan's order), and what it can still give (§8.17 step 4 "kullanılabilir").</summary>
public sealed record PlanBin(Guid LocationId, string Code, decimal Usable, decimal OnHand);

/// <summary>
/// The pure rules of the S2 allocation (pack §8.17 steps 2–4, §8.18) — no I/O: the greedy fill of a line over its sources in FEFO order, the
/// bin distribution in location-code order, the conservative remaining of an open task, the deterministic reservation key and the
/// visibility of the internal statuses.
/// </summary>
public static class OutboundAllocationRules
{
    /// <summary>FU03 batch limit (FU03 §8.7, Q-06): the allocations of one outbound go in ONE batch of at most 100 lines.</summary>
    public const int MaxAllocations = 100;

    public const int MaxLines = 100;

    public const int MaxOrderLineIdLength = 100;

    public const int MaxOrderRefLength = 100;

    public const int MaxNoteLength = 1000;

    /// <summary>The FEFO bypass reason bounds (pack §8.17 step 3; the lower bound is the hook's, the upper INVENTORY's parser's — r24).</summary>
    public const int MinReasonLength = 10;

    public const int MaxReasonLength = 1000;

    /// <summary>FU03 Q-07 (pack §8.10): the reservation type of every 0178 reservation.</summary>
    public const string ReservationType = "WAREHOUSE_OUTBOUND";

    /// <summary>The six WAREHOUSE-OUTBOUND 1.0.0 statuses — the only ones a reader ever sees (§8.18; internal states → 404 / absent).</summary>
    public static readonly IReadOnlyList<OutboundStatus> VisibleStatuses =
        [OutboundStatus.Allocated, OutboundStatus.Picked, OutboundStatus.Packed, OutboundStatus.ReadyToShip, OutboundStatus.Shipped, OutboundStatus.Cancelled];

    public static bool IsVisible(OutboundStatus status) => VisibleStatuses.Contains(status);

    /// <summary><c>wms:out:{outboundId}:{allocSeq}</c> (§4.5) — deterministic: recovery re-sends the SAME keys (AC-71 (ii)).</summary>
    public static string ReservationKey(Guid outboundId, int allocSeq)
        => string.Create(CultureInfo.InvariantCulture, $"wms:out:{outboundId:D}:{allocSeq}");

    /// <summary>
    /// §8.17 step 4 (r22, round 2 F-3 b): an open task's remaining for the plan is CONSERVATIVE — planned − Posted; a Pending confirmation is
    /// not subtracted (its movement may not have committed, then the bin's AVAILABLE still holds it). Never below 0.
    /// </summary>
    public static decimal OpenTaskRemaining(WarehouseTask task) => Math.Max(0m, task.PlannedQuantity - task.ConfirmedQuantity);

    /// <summary>
    /// The greedy fill of one line (§8.17 step 2): the sources in their given order, each giving min(rest, allocatable); returns the taken
    /// (source, quantity) pairs. The caller passes the sources already in FEFO order with what earlier lines took subtracted.
    /// </summary>
    public static IReadOnlyList<(AllocationSource Source, decimal Quantity)> Fill(decimal requested, IEnumerable<AllocationSource> sources)
    {
        var taken = new List<(AllocationSource, decimal)>();
        var rest = requested;
        foreach (var source in sources)
        {
            if (rest <= 0m) break;
            if (source.Allocatable <= 0m) continue;
            var q = Math.Min(rest, source.Allocatable);
            taken.Add((source, q));
            rest -= q;
        }

        return taken;
    }

    /// <summary>
    /// §8.17 step 4: the quantity over the bins in LOCATION-CODE order, each giving min(rest, usable); null = the bins cannot carry it
    /// (<c>BinCapacityShort</c>). A serial SKU (§8.16) takes a bin only WHOLE: a bin it would take partly → <paramref name="serialPartial"/>.
    /// </summary>
    public static IReadOnlyList<(PlanBin Bin, decimal Quantity)>? Distribute(decimal quantity, IEnumerable<PlanBin> bins, bool serial, out bool serialPartial)
    {
        serialPartial = false;
        var plan = new List<(PlanBin, decimal)>();
        var rest = quantity;
        foreach (var bin in bins.OrderBy(b => b.Code, StringComparer.Ordinal).ThenBy(b => b.LocationId))
        {
            if (rest <= 0m) break;
            if (bin.Usable <= 0m) continue;
            var q = Math.Min(rest, bin.Usable);
            if (serial && q != bin.OnHand) serialPartial = true;
            plan.Add((bin, q));
            rest -= q;
        }

        return rest > 0m ? null : plan;
    }

    /// <summary>A FEFO bypass reason as written: trimmed; blank = none.</summary>
    public static string? Reason(string? raw) => string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
}
