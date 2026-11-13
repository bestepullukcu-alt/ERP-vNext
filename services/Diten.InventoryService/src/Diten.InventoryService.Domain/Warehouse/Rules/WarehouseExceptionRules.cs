using System.Globalization;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Rules;

/// <summary>
/// One derived warehouse exception (pack §8.23; WMS draft §9 <c>WarehouseException</c>) — never stored. <see cref="ExceptionId"/> is
/// <c>{kind}:{source key}</c>, the source key being the kind's row unit (r25, son bakış 03 F-7): <c>{taskId}:{seq}</c> for the three posting kinds,
/// <c>{outboundId}</c> for <c>DispatchNotSeen</c> / <c>AllocationStuck</c> / <c>AllocationOrphanSuspected</c>, <c>{outboundId}:{allocSeq}</c> for
/// <c>ReservationReleasePending</c> / <c>ReservedLotUnderRecall</c> and <c>{outboundId}:{skuId}</c> for <c>ShortDispatchUnresolved</c>.
/// <see cref="Details"/> holds domain values (decimal, date, id, text, lists); the read model turns them into wire text.
/// </summary>
public sealed record WarehouseExceptionRecord(
    WarehouseExceptionKind Kind,
    string SourceKey,
    Guid WarehouseId,
    Guid? TaskId,
    Guid? OutboundId,
    string? Code,
    DateTimeOffset OpenedAtUtc,
    IReadOnlyList<KeyValuePair<string, object?>> Details)
{
    public string ExceptionId => $"{Kind}:{SourceKey}";
}

/// <summary>One lot of a Staging difference (§8.23 <c>stagingDifference[] { lotId?, onHand, bound, difference }</c>); only lots with a difference ≠ 0.</summary>
public sealed record StagingLotDifference(string? LotId, decimal OnHand, decimal Bound, decimal Difference);

/// <summary>A balance row at a Staging node (every stock status), as the difference reads it.</summary>
public sealed record StagingBalance(Guid SkuId, string? LotId, decimal OnHand);

/// <summary>
/// The Staging difference of one (node, SKU): <see cref="Differences"/> (empty = closed) when it was computed, or null with <see cref="DeferredBy"/>
/// when it was not (the row stays listed — the cautious direction, §8.23 F-1 (b)).
/// </summary>
public sealed record StagingDifferenceResult(IReadOnlyList<StagingLotDifference>? Differences, StagingDifferenceDeferral? DeferredBy)
{
    /// <summary>Listed while not computed, or while any lot's difference is ≠ 0 (the sign is not looked at).</summary>
    public bool Listed => DeferredBy is not null || Differences is { Count: > 0 };
}

/// <summary>
/// What the Staging difference reads for the nodes of the unresolved rows (pack §8.23): the balances at EXACTLY each node (no subtree — F-2), the
/// outbounds whose <c>StagingLocationId</c> is one of the nodes (every status; the rule picks), their picked quantities per allocation (derived from
/// the pick tasks), the Open / InProgress tasks whose source is one of the nodes, and every task with a Pending confirmation.
/// </summary>
public sealed record StagingInputs(
    IReadOnlyDictionary<Guid, IReadOnlyList<StagingBalance>> BalancesByNode,
    IReadOnlyList<OutboundShipment> NodeOutbounds,
    IReadOnlyDictionary<Guid, IReadOnlyDictionary<int, decimal>> PickedByOutbound,
    IReadOnlyList<WarehouseTask> OpenTasksFromNodes,
    IReadOnlyList<WarehouseTask> TasksWithPending);

/// <summary>The two S3 time thresholds of the outbound kinds (§8.23): <c>DispatchNotSeenHours</c> and 2 × <c>AllocationLeaseSeconds</c>.</summary>
public sealed record OutboundExceptionThresholds(TimeSpan DispatchNotSeen, TimeSpan AllocationStuck);

/// <summary>
/// The pure S3 derivation (pack §8.23; WMS draft §9) — no I/O. A record exists while its source condition holds and drops out of the list when it no
/// longer does (<c>status</c> is always <c>Open</c> in v1; no acknowledge / resolve, FU-0178-S3-ACT).
/// </summary>
public static class WarehouseExceptionRules
{
    /// <summary>The outbound statuses whose allocation lot under recall is listed (r22, tur 2 F-4: <c>Allocating</c> is internal — not listed).</summary>
    public static readonly IReadOnlyList<OutboundStatus> RecallStatuses = [OutboundStatus.Allocated, OutboundStatus.Picked, OutboundStatus.Packed, OutboundStatus.ReadyToShip];

    /// <summary>The outbound statuses that never bind picked stock at the Staging node any more (the "bound quantity" leaves them out).</summary>
    public static readonly IReadOnlyList<OutboundStatus> UnboundStatuses = [OutboundStatus.Shipped, OutboundStatus.Cancelled, OutboundStatus.AllocationFailed];

    /// <summary>
    /// The three posting kinds of one task (putaway and pick alike): an UNRESOLVED Refused confirmation (no <c>Resolution</c> — independent reviews
    /// 15 F-1 / F-2), a Pending confirmation older than <paramref name="longPending"/> (the badge's own threshold, §8.3 step 5) and an IntegrityError
    /// confirmation — one record per confirmation, key <c>{taskId}:{seq}</c>.
    /// </summary>
    public static IEnumerable<WarehouseExceptionRecord> OfTask(WarehouseTask task, DateTimeOffset now, TimeSpan longPending)
    {
        foreach (var c in task.Confirmations.OrderBy(c => c.Seq))
        {
            WarehouseExceptionKind? kind = c.Status switch
            {
                ConfirmationStatus.Refused when PutawayTaskRules.IsUnresolvedRefused(c) => WarehouseExceptionKind.PostingRefused,
                ConfirmationStatus.Pending when now - c.ConfirmedAtUtc > longPending => WarehouseExceptionKind.PostingPendingLong,
                ConfirmationStatus.IntegrityError => WarehouseExceptionKind.PostingIntegrityError,
                _ => null
            };
            if (kind is not { } k) continue;
            var opened = k switch
            {
                WarehouseExceptionKind.PostingRefused => c.RefusedAtUtc ?? c.LastAttemptAtUtc ?? c.ConfirmedAtUtc,
                WarehouseExceptionKind.PostingIntegrityError => c.LastAttemptAtUtc ?? c.ConfirmedAtUtc,
                _ => c.ConfirmedAtUtc
            };
            yield return new WarehouseExceptionRecord(k, $"{task.Id:D}:{c.Seq.ToString(CultureInfo.InvariantCulture)}", task.WarehouseId, task.Id, task.OutboundId,
                k == WarehouseExceptionKind.PostingPendingLong ? null : c.ErrorCode, opened,
            [
                new("taskNumber", task.Number), new("taskKind", task.Kind.ToString()), new("seq", c.Seq), new("quantity", c.Quantity),
                new("confirmedAtUtc", c.ConfirmedAtUtc), new("outboundNumber", task.OutboundNumber)
            ]);
        }
    }

    /// <summary>
    /// The six outbound kinds of one outbound (§8.23). <paramref name="recalledLots"/> = the lots under recall among the outbound's allocation lots;
    /// <paramref name="staging"/> computes the Staging difference of (node, SKU) for a stored <c>ShortDispatchUnresolved</c> row.
    /// </summary>
    public static IEnumerable<WarehouseExceptionRecord> OfOutbound(OutboundShipment o, DateTimeOffset now, OutboundExceptionThresholds thresholds,
        IReadOnlySet<string> recalledLots, Func<Guid, Guid, StagingDifferenceResult> staging)
    {
        var id = o.Id.ToString("D");

        // DispatchNotSeen — ReadyToShip and ReadyAtUtc older than DispatchNotSeenHours (a 1-hour ReadyToShip is not listed)
        if (o.Status == OutboundStatus.ReadyToShip && o.ReadyAtUtc is { } ready && now - ready > thresholds.DispatchNotSeen)
            yield return Outbound(o, WarehouseExceptionKind.DispatchNotSeen, id, ready, [new("readyAtUtc", ready)]);

        // AllocationStuck — Allocating and now − CreatedAtUtc > 2 × AllocationLeaseSeconds (the maintenance did not complete it in one round at least)
        if (o.Status == OutboundStatus.Allocating && now - o.CreatedAt > thresholds.AllocationStuck)
            yield return Outbound(o, WarehouseExceptionKind.AllocationStuck, id, o.CreatedAt, [new("createdAtUtc", o.CreatedAt)]);

        // ReservationReleasePending — a Cancelled / Shipped outbound's allocation whose release waits (AllocationFailed's are the orphan kind: no double row)
        if (o.Status is OutboundStatus.Cancelled or OutboundStatus.Shipped)
        {
            foreach (var a in o.AllAllocations().Where(a => a.ReservationReleaseState == ReservationReleaseState.Pending))
            {
                yield return Outbound(o, WarehouseExceptionKind.ReservationReleasePending, $"{id}:{a.AllocSeq.ToString(CultureInfo.InvariantCulture)}", o.UpdatedAt ?? o.CreatedAt,
                    [new("allocSeq", a.AllocSeq), new("lotId", a.LotId), new("lotNumber", a.LotNumber), new("reservationId", a.ReservationId), new("outboundStatus", o.Status.ToString())]);
            }
        }

        // AllocationOrphanSuspected — AllocationFailed with kind KeyReused while any allocation is not Released (BatchRefused is never listed: no reservation)
        if (o.Status == OutboundStatus.AllocationFailed && o.AllocationFailure is { Kind: AllocationFailureKind.KeyReused } failure)
        {
            var notReleased = o.AllAllocations().Where(a => a.ReservationReleaseState != ReservationReleaseState.Released).ToList();
            if (notReleased.Count > 0)
            {
                yield return Outbound(o, WarehouseExceptionKind.AllocationOrphanSuspected, id, failure.AtUtc,
                [
                    new("reservationKeys", o.AllAllocations().Select(a => a.ReservationKey).ToList()), new("pending", notReleased.Count), new("atUtc", failure.AtUtc)
                ]);
            }
        }

        // ReservedLotUnderRecall — an Allocated / Picked / Packed / ReadyToShip outbound's allocation lot is under recall
        if (RecallStatuses.Contains(o.Status))
        {
            foreach (var a in o.AllAllocations().Where(a => a.LotId is not null && recalledLots.Contains(a.LotId)))
            {
                yield return Outbound(o, WarehouseExceptionKind.ReservedLotUnderRecall, $"{id}:{a.AllocSeq.ToString(CultureInfo.InvariantCulture)}", o.UpdatedAt ?? o.CreatedAt,
                    [new("allocSeq", a.AllocSeq), new("lotId", a.LotId), new("lotNumber", a.LotNumber), new("outboundStatus", o.Status.ToString())]);
            }
        }

        // ShortDispatchUnresolved — a Shipped outbound's stored row (one per SKU) while the Staging difference is open or not computable
        if (o.Status == OutboundStatus.Shipped)
        {
            foreach (var row in o.ShortDispatchUnresolved)
            {
                var difference = staging(o.StagingLocationId, row.SkuId);
                if (!difference.Listed) continue;
                yield return Outbound(o, WarehouseExceptionKind.ShortDispatchUnresolved, $"{id}:{row.SkuId:D}", o.Shipment?.ObservedAtUtc ?? o.UpdatedAt ?? o.CreatedAt,
                [
                    new("skuId", row.SkuId), new("skuLevel", o.Lines.FirstOrDefault(l => l.SkuId == row.SkuId) is { } line ? WireNames.ToWire(line.SkuLevel) : null),
                    new("lotId", row.LotId), new("expected", row.Expected), new("issued", row.Issued), new("reason", row.Reason),
                    new("stagingLocationId", o.StagingLocationId), new("stagingDifference", difference.Differences),
                    new("differenceDeferredBy", difference.DeferredBy?.ToString())
                ]);
            }
        }
    }

    /// <summary>
    /// The Staging difference of (<paramref name="node"/>, <paramref name="skuId"/>) — r25 (son bakış 03 F-1 / F-2). Not computed (deferred) while a
    /// task of the SKU has a Pending confirmation from or into the node, or while an outbound of the node with a line of the SKU is <c>ReadyToShip</c>
    /// (MVP-6 may already have issued it). Otherwise per (SKU, lot) — a SKU without lots is one bucket — at EXACTLY the node: <c>OnHand(lot)</c>
    /// (every stock status) − bound(lot), bound = the picked quantity of the node's outbounds that are not Shipped / Cancelled / AllocationFailed +
    /// the remainder of the open Cancellation / ShortDispatch putaways from the node. Only the lots with a difference ≠ 0 are returned.
    /// </summary>
    public static StagingDifferenceResult StagingDifference(Guid node, Guid skuId, StagingInputs inputs)
    {
        if (inputs.TasksWithPending.Any(t => t.SkuId == skuId
                                             && t.Confirmations.Any(c => c.Status == ConfirmationStatus.Pending && (t.FromLocationId == node || c.ToLocationId == node))))
            return new StagingDifferenceResult(null, StagingDifferenceDeferral.PendingConfirmation);
        if (inputs.NodeOutbounds.Any(o => o.Status == OutboundStatus.ReadyToShip && o.StagingLocationId == node && o.Lines.Any(l => l.SkuId == skuId)))
            return new StagingDifferenceResult(null, StagingDifferenceDeferral.UnsyncedReadyToShip);

        var onHand = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var row in inputs.BalancesByNode.GetValueOrDefault(node) ?? [])
        {
            if (row.SkuId != skuId) continue;
            onHand[row.LotId ?? string.Empty] = onHand.GetValueOrDefault(row.LotId ?? string.Empty) + row.OnHand;
        }

        var bound = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var o in inputs.NodeOutbounds.Where(o => o.StagingLocationId == node && !UnboundStatuses.Contains(o.Status)))
        {
            var picked = inputs.PickedByOutbound.GetValueOrDefault(o.Id) ?? new Dictionary<int, decimal>();
            foreach (var a in o.Lines.Where(l => l.SkuId == skuId).SelectMany(l => l.Allocations))
                bound[a.LotId ?? string.Empty] = bound.GetValueOrDefault(a.LotId ?? string.Empty) + picked.GetValueOrDefault(a.AllocSeq);
        }

        foreach (var t in inputs.OpenTasksFromNodes.Where(t => t.Kind == WarehouseTaskKind.Putaway && t.FromLocationId == node && t.SkuId == skuId
                                                                && t.Origin is WarehouseTaskOrigin.Cancellation or WarehouseTaskOrigin.ShortDispatch
                                                                && t.Status is WarehouseTaskStatus.Open or WarehouseTaskStatus.InProgress))
            bound[t.LotId ?? string.Empty] = bound.GetValueOrDefault(t.LotId ?? string.Empty) + (t.PlannedQuantity - t.ConfirmedQuantity);

        var differences = onHand.Keys.Union(bound.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal)
            .Select(lot => new StagingLotDifference(lot.Length == 0 ? null : lot, onHand.GetValueOrDefault(lot), bound.GetValueOrDefault(lot),
                onHand.GetValueOrDefault(lot) - bound.GetValueOrDefault(lot)))
            .Where(d => d.Difference != 0m)
            .ToList();
        return new StagingDifferenceResult(differences, null);
    }

    private static WarehouseExceptionRecord Outbound(OutboundShipment o, WarehouseExceptionKind kind, string key, DateTimeOffset opened,
        List<KeyValuePair<string, object?>> details)
    {
        details.Insert(0, new KeyValuePair<string, object?>("outboundNumber", o.OutboundNumber));
        return new WarehouseExceptionRecord(kind, key, o.WarehouseId, null, o.Id, null, opened, details);
    }
}
