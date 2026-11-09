using System.Globalization;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Rules;

/// <summary>One MVP-6 shipment line as the dispatch sync read it (SHIPMENT 3.4.0 <c>lines[] { skuId, lotId, fromLocationId, quantity }</c>).</summary>
public sealed record IssuedLine(Guid SkuId, string? LotId, Guid? FromLocationId, decimal Quantity);

/// <summary>A putaway the short dispatch opens from the Staging node: one per (SKU, lot) with a positive residue (§8.21).</summary>
public sealed record ShortDispatchPutback(Guid ItemId, Guid SkuId, SkuLevel Level, string? LotId, string? LotNumber, string BaseUomId, bool SerialControlled,
    decimal Quantity);

/// <summary>What the short-dispatch rule decided for one outbound: the put-backs and the unresolved SKU rows.</summary>
public sealed record ShortDispatchPlan(IReadOnlyList<ShortDispatchPutback> Putbacks, IReadOnlyList<OutboundShortDispatchUnresolved> Unresolved);

/// <summary>
/// The pure rules of the S2 pick → pack → ship flow (pack §8.18–§8.22) — no I/O: the picked / packed quantities DERIVED from the pick tasks and the
/// closed packages (never stored, §4.5), the <c>Picked</c> / <c>Packed</c> conditions, the "shipped" decision on an MVP-6 status and the
/// short-dispatch residue with its SKU-level resolvability (r24 / r25).
/// </summary>
public static class OutboundFlowRules
{
    public const string LotMissing = "LotMissing";
    public const string NotFromStaging = "NotFromStaging";
    public const string OverIssued = "OverIssued";

    /// <summary>The MVP-6 statuses that mean "dispatched" (r21: <c>ShipmentLifecycle.Allows</c> — <c>Exception</c> is entered only after dispatch).</summary>
    public static readonly IReadOnlyList<string> DispatchedStatuses = ["Dispatched", "InTransit", "Delivered", "Exception", "Closed"];

    /// <summary>§8.21: shipped = a dispatched status, or <c>Cancelled</c> with the dispatch trace (<c>stockIssued == true</c>); anything else = not seen.</summary>
    public static bool IsDispatched(string? status, bool? stockIssued)
        => status is not null && (DispatchedStatuses.Contains(status, StringComparer.Ordinal) || (status == "Cancelled" && stockIssued == true));

    /// <summary>§4.5 <c>PickedQuantity</c>: the Posted sum of the allocation's pick tasks (every task of the allocation, every status).</summary>
    public static IReadOnlyDictionary<int, decimal> PickedByAllocation(IEnumerable<WarehouseTask> pickTasks)
        => pickTasks.Where(t => t.AllocSeq is not null).GroupBy(t => t.AllocSeq!.Value).ToDictionary(g => g.Key, g => g.Sum(t => t.ConfirmedQuantity));

    /// <summary>§4.5 <c>PackedQuantity</c>: the sum of the allocation in CLOSED packages.</summary>
    public static IReadOnlyDictionary<int, decimal> PackedByAllocation(IEnumerable<HandlingUnit> packages)
        => packages.Where(p => p.Status == HandlingUnitStatus.Closed).SelectMany(p => p.Contents)
            .GroupBy(c => c.AllocSeq).ToDictionary(g => g.Key, g => g.Sum(c => c.Quantity));

    /// <summary>§8.20: the allocation's quantity in OPEN + CLOSED packages, the one package <paramref name="except"/> left out (its own new content).</summary>
    public static decimal InPackages(IEnumerable<HandlingUnit> packages, int allocSeq, Guid? except = null)
        => packages.Where(p => p.Status != HandlingUnitStatus.Cancelled && p.Id != except).SelectMany(p => p.Contents).Where(c => c.AllocSeq == allocSeq).Sum(c => c.Quantity);

    /// <summary>§8.18 <c>Picked</c>: every pick task terminal AND the picked total &gt; 0 (all short-closed with 0 → stays <c>Allocated</c>).</summary>
    public static bool IsPicked(IReadOnlyCollection<WarehouseTask> pickTasks)
        => pickTasks.Count > 0 && pickTasks.All(t => PutawayTaskRules.IsTerminal(t.Status)) && pickTasks.Sum(t => t.ConfirmedQuantity) > 0m;

    /// <summary>§8.18 <c>Packed</c>: every picked allocation fully in closed packages (<c>PackedQuantity == PickedQuantity</c>).</summary>
    public static bool IsPacked(OutboundShipment outbound, IReadOnlyDictionary<int, decimal> picked, IReadOnlyDictionary<int, decimal> packed)
        => outbound.AllAllocations().All(a => picked.GetValueOrDefault(a.AllocSeq) == packed.GetValueOrDefault(a.AllocSeq));

    /// <summary>
    /// §8.21 short dispatch (r23 / r24 / r25). Per SKU: residue = Σ picked − Σ issued, per lot. The SKU is resolvable by tasks only when EVERY one
    /// of its shipment lines (a) names a lot (lot-tracked SKU), (b) issued from the outbound's Staging node, and (c) no lot issued more than it was
    /// picked; then one Staging putaway per lot with residue &gt; 0. Otherwise no task for the SKU at all (not even its resolvable-looking lots) and
    /// ONE unresolved row (<c>reason</c> = the first condition that failed; <c>lotId</c> only when the SKU's picked quantity sits on one lot).
    /// </summary>
    public static ShortDispatchPlan ShortDispatch(OutboundShipment outbound, IReadOnlyDictionary<int, decimal> picked, IReadOnlyList<IssuedLine> issued)
    {
        var putbacks = new List<ShortDispatchPutback>();
        var unresolved = new List<OutboundShortDispatchUnresolved>();
        var allocations = outbound.Lines.SelectMany(l => l.Allocations.Select(a => (Line: l, Allocation: a, Picked: picked.GetValueOrDefault(a.AllocSeq)))).ToList();
        var skus = allocations.Select(x => x.Line.SkuId).Distinct().ToList();
        foreach (var sku in skus)
        {
            var mine = allocations.Where(x => x.Line.SkuId == sku).ToList();
            var lines = issued.Where(i => i.SkuId == sku).ToList();
            var lotTracked = mine.Any(x => x.Allocation.LotId is not null);
            var pickedByLot = mine.GroupBy(x => x.Allocation.LotId ?? string.Empty).ToDictionary(g => g.Key, g => g.Sum(x => x.Picked), StringComparer.Ordinal);
            var issuedByLot = lines.GroupBy(i => i.LotId ?? string.Empty).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity), StringComparer.Ordinal);

            string? reason = null;
            if (lotTracked && lines.Any(i => i.LotId is null)) reason = LotMissing;
            else if (lines.Any(i => i.FromLocationId != outbound.StagingLocationId)) reason = NotFromStaging;
            else if (issuedByLot.Any(kv => kv.Value > pickedByLot.GetValueOrDefault(kv.Key))) reason = OverIssued;

            if (reason is not null)
            {
                var pickedLots = mine.Where(x => x.Picked > 0m).Select(x => x.Allocation.LotId).Distinct().ToList();
                unresolved.Add(new OutboundShortDispatchUnresolved
                {
                    SkuId = sku, LotId = pickedLots.Count == 1 ? pickedLots[0] : null, Expected = mine.Sum(x => x.Picked), Issued = lines.Sum(i => i.Quantity),
                    Reason = reason
                });
                continue;
            }

            foreach (var (lot, quantity) in pickedByLot)
            {
                var residue = quantity - issuedByLot.GetValueOrDefault(lot);
                if (residue <= 0m) continue;
                var first = mine.First(x => (x.Allocation.LotId ?? string.Empty) == lot);
                putbacks.Add(new ShortDispatchPutback(first.Line.ItemId, sku, first.Line.SkuLevel, first.Allocation.LotId, first.Allocation.LotNumber, first.Line.BaseUomId,
                    first.Line.SerialControlled, residue));
            }
        }

        return new ShortDispatchPlan(putbacks, unresolved);
    }

    /// <summary>The pick task posting key (WMS draft §6.2): <c>wms:pck:{taskId}:{seq}</c> — the same on every attempt.</summary>
    public static string PickPostingKey(Guid taskId, int seq) => string.Create(CultureInfo.InvariantCulture, $"wms:pck:{taskId:D}:{seq}");

    /// <summary>§12: weight &gt; 0 with at most 3 fraction digits.</summary>
    public static bool IsWeight(decimal value) => value > 0m && decimal.Round(value, 3) == value;

    /// <summary>§12: <c>^\d{1,4}x\d{1,4}x\d{1,4}$</c>.</summary>
    public static bool IsDimensions(string value)
        => System.Text.RegularExpressions.Regex.IsMatch(value, @"^\d{1,4}x\d{1,4}x\d{1,4}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
