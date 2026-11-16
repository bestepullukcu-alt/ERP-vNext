using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.OutboundShipments.Commands;
using Diten.InventoryService.Application.Features.OutboundShipments.Services;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Expiry.Enums;
using Diten.InventoryService.Domain.Expiry.Rules;
using Diten.InventoryService.Domain.Recall.Trace;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Handlers.CommandHandlers;

/// <summary>The shared parts of the DL-5 outbound commands: the key replay on the outbound timeline, the put-back numbers, the conflict answer.</summary>
public abstract class OutboundFlowHandler
{
    protected OutboundFlowHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads, WarehouseSupport support)
    {
        Outbounds = outbounds;
        Reads = reads;
        Support = support;
    }

    protected IOutboundShipmentRepository Outbounds { get; }

    protected OutboundShipmentReads Reads { get; }

    protected WarehouseSupport Support { get; }

    /// <summary>null = not a replay; otherwise 200 + the current outbound (another body → 400 IDEMPOTENCY_KEY_REUSED).</summary>
    protected async Task<Response<OutboundShipmentViewDto>?> ReplayAsync(Guid outboundId, string op, string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Outbounds.FindKeyEventAsync(outboundId, Support.UserId, op, key, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return WarehouseSupport.KeyReused<OutboundShipmentViewDto>();
        var current = await Reads.FindVisibleAsync(outboundId, ct);
        return current is null ? OutboundShipmentReads.UnknownOutbound<OutboundShipmentViewDto>() : Response<OutboundShipmentViewDto>.Success(await Reads.ViewAsync(current, ct));
    }

    protected static Response<T> Transition<T>(OutboundShipment o, string what)
        => WarehouseSupport.Fail<T>(WarehouseErrorCodes.InvalidTransition, $"A {o.Status} outbound cannot {what}.", 409,
            new Dictionary<string, object?> { ["status"] = o.Status.ToString() });

    /// <summary>One (SKU, lot) bucket per put-back (§8.21 / §8.22): the picked quantity of the outbound's allocations on Staging.</summary>
    protected static IReadOnlyList<ShortDispatchPutback> PickedBuckets(OutboundShipment o, IReadOnlyDictionary<int, decimal> picked)
        => o.Lines.SelectMany(l => l.Allocations.Select(a => (Line: l, Allocation: a, Quantity: picked.GetValueOrDefault(a.AllocSeq))))
            .Where(x => x.Quantity > 0m)
            .GroupBy(x => (x.Line.SkuId, x.Line.SkuLevel, x.Allocation.LotId))
            .Select(g => new ShortDispatchPutback(g.First().Line.ItemId, g.Key.SkuId, g.Key.SkuLevel, g.Key.LotId, g.First().Allocation.LotNumber, g.First().Line.BaseUomId,
                g.First().Line.SerialControlled, g.Sum(x => x.Quantity)))
            .ToList();

    protected static IEnumerable<string> ReleaseStates(OutboundShipment o)
        => o.AllAllocations().Select(a => $"{a.AllocSeq}:{a.ReservationReleaseState}");
}

/// <summary>
/// <c>cancelOutboundShipment</c> (pack §8.22; AC-32, AC-33 (v), AC-72, AC-77). Order: reason (400 REASON_REQUIRED); the key (replay); the outbound
/// (404); <c>ReadyToShip</c> / <c>Shipped</c> → 409 WMS_OUTBOUND_CANCEL_NOT_ALLOWED (Q-M6-7), <c>Cancelled</c> → 409 WMS_INVALID_TRANSITION; If-Match;
/// a Pending pick confirmation → 409 WMS_CONFIRMATION_PENDING; an IntegrityError / a Refused confirmation whose key IS committed (the S1 closing
/// pre-look, row 20) → 409 WMS_POSTING_INTEGRITY_ERROR. Transaction A: outbound <c>Cancelled</c>, open pick tasks <c>Cancelled</c>, packages
/// <c>Cancelled</c>, EVERY allocation <c>ReservationReleaseState = Pending</c>, one <c>Origin = Cancellation</c> putaway task per (SKU, lot) bucket
/// moved to Staging (planned = the picked quantity), the rows. Then — outside the transaction, in order — the release rule per allocation; a failed
/// release leaves the allocation <c>Pending</c> for the maintenance. 200 (the release state per allocation in the answer).
/// </summary>
public sealed class CancelOutboundShipmentHandler : OutboundFlowHandler, IRequestHandler<CancelOutboundShipmentCommand, Response<OutboundShipmentViewDto>>
{
    private readonly PutawayPostingEngine _engine;
    private readonly IWarehouseRecordCodeAllocator _numbers;
    private readonly OutboundReservationReleaser _releaser;

    public CancelOutboundShipmentHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads, WarehouseSupport support, PutawayPostingEngine engine,
        IWarehouseRecordCodeAllocator numbers, OutboundReservationReleaser releaser)
        : base(outbounds, reads, support)
    {
        _engine = engine;
        _numbers = numbers;
        _releaser = releaser;
    }

    public async Task<Response<OutboundShipmentViewDto>> Handle(CancelOutboundShipmentCommand request, CancellationToken ct)
    {
        var reason = WarehouseSupport.Trimmed(request.Reason);
        if (!PutawayTaskRules.IsReason(reason)) return PutawayTaskSupport.ReasonRequired<OutboundShipmentViewDto>("reason");
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpCancelOutboundShipment), ("outboundId", request.OutboundId.ToString("D")), ("reason", reason));
        if (await ReplayAsync(request.OutboundId, WarehouseContract.OpCancelOutboundShipment, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var outbound = await Reads.FindVisibleAsync(request.OutboundId, ct);
        if (outbound is null) return OutboundShipmentReads.UnknownOutbound<OutboundShipmentViewDto>();
        if (outbound.Status is OutboundStatus.ReadyToShip or OutboundStatus.Shipped)
        {
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(WarehouseErrorCodes.OutboundCancelNotAllowed,
                "A ready-to-ship outbound is not cancelled in v1 (the source cancel is the next contract version).", 409,
                new Dictionary<string, object?> { ["status"] = outbound.Status.ToString() });
        }

        if (outbound.Status == OutboundStatus.Cancelled) return Transition<OutboundShipmentViewDto>(outbound, "be cancelled");
        if (outbound.Version != request.ExpectedVersion) return WarehouseSupport.Conflict<OutboundShipmentViewDto>();

        var tasks = await Outbounds.PickTasksAsync(outbound.Id, ct);
        if (tasks.Any(PutawayTaskRules.HasPending))
        {
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(WarehouseErrorCodes.ConfirmationPending,
                "A pick confirmation is waiting for the stock answer; cancel after it.", 409);
        }

        if (tasks.Any(PutawayTaskRules.HasIntegrityError)) return PutawayTaskSupport.IntegrityLocked<OutboundShipmentViewDto>();
        foreach (var task in tasks)
        {
            foreach (var c in task.Confirmations.Where(c => c.Status == ConfirmationStatus.Refused).ToList())
            {
                if ((await _engine.LookupAsync(task, c, ct))?.Kind != PostingResultKind.Posted) continue;
                await _engine.MarkDivergedAsync(task, c.Seq, ct);
                return PutawayTaskSupport.IntegrityLocked<OutboundShipmentViewDto>(new Dictionary<string, object?> { ["taskId"] = task.Id.ToString("D"), ["seq"] = c.Seq });
            }
        }

        var picked = OutboundFlowRules.PickedByAllocation(tasks);
        var buckets = PickedBuckets(outbound, picked);
        var numbers = new List<string>();
        try
        {
            foreach (var _ in buckets) numbers.Add(await _numbers.NextAsync(outbound.CompanyCodePrefix, WarehouseRecordCodeRules.PutawayTask, ct));
        }
        catch (WarehouseRecordCodeUnavailableException)
        {
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(WarehouseErrorCodes.RecordCodeUnavailable, "The put-back task numbers could not be issued; nothing was written.", 503);
        }

        var packages = await Outbounds.PackagesAsync(outbound.Id, ct);
        var now = Support.Now();
        var copy = outbound.Copy();
        copy.Status = OutboundStatus.Cancelled;
        copy.CancelReason = reason;
        copy.Version = outbound.Version + 1;
        copy.UpdatedAt = now;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        foreach (var a in copy.AllAllocations()) a.ReservationReleaseState = ReservationReleaseState.Pending;

        var taskWrites = new List<(WarehouseTask Copy, int Expected)>();
        var taskEvents = new List<WarehouseTaskEvent>();
        foreach (var task in tasks.Where(PutawayTaskRules.IsOpen))
        {
            var t = task.Copy();
            t.Status = WarehouseTaskStatus.Cancelled;
            t.CancelReason = reason;
            t.OpenSerialSlot = null;
            t.Version = task.Version + 1;
            t.UpdatedAt = now;
            t.UpdatedBy = Support.UserId;
            t.UpdatedByName = Support.ActorName;
            taskEvents.Add(TaskEvent(t, WarehouseTaskEventKind.Cancelled, now, new Dictionary<string, string?> { ["cancelReason"] = reason, ["outboundId"] = outbound.Id.ToString("D") }));
            taskWrites.Add((t, task.Version));
        }

        var packageWrites = new List<(HandlingUnit Copy, int Expected)>();
        var events = new List<OutboundShipmentEvent>();
        foreach (var p in packages.Where(p => p.Status != HandlingUnitStatus.Cancelled))
        {
            var pc = p.Copy();
            pc.Status = HandlingUnitStatus.Cancelled;
            pc.Version = p.Version + 1;
            pc.UpdatedAt = now;
            pc.UpdatedBy = Support.UserId;
            packageWrites.Add((pc, p.Version));
        }

        var putbacks = buckets.Select((b, i) => OutboundFlowReads.Putback(copy, WarehouseTaskOrigin.Cancellation, b, numbers[i], Support, now)).ToList();
        var putbackEvents = putbacks.Select(t => OutboundFlowReads.PutbackCreated(t, copy, Support, now)).ToList();
        events.Add(Reads.NewEvent(copy, OutboundEventKind.Cancelled, now, new Dictionary<string, string?>
        {
            ["cancelReason"] = reason, ["cancelledPickTasks"] = string.Join(",", taskWrites.Select(t => t.Copy.Number)),
            ["putbackTasks"] = JsonSerializer.Serialize(putbacks.Select(t => new { taskId = t.Id.ToString("D"), t.Number, t.LotId, quantity = InventoryQuantity.ToWire(t.PlannedQuantity) }))
        }, request.IdempotencyKey, WarehouseContract.OpCancelOutboundShipment, fingerprint));
        events.AddRange(packageWrites.Select(p => Reads.NewEvent(copy, OutboundEventKind.PackageCancelled, now,
            new Dictionary<string, string?> { ["packageId"] = p.Copy.Id.ToString("D"), ["packageNumber"] = p.Copy.PackageNumber })));

        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await Outbounds.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceOutboundAsync(copy, outbound.Version, tx)) return WarehouseWriteOutcome.None;
                foreach (var (t, expected) in taskWrites)
                {
                    if (!await session.ReplaceTaskAsync(t, expected, tx)) throw new OutboundWriteLostException();
                }

                foreach (var (p, expected) in packageWrites)
                {
                    if (!await session.ReplacePackageAsync(p, expected, tx)) throw new OutboundWriteLostException();
                }

                await session.InsertEventsAsync(events, tx);
                await session.InsertTasksAsync([.. putbacks], [.. taskEvents, .. putbackEvents], tx);
                return new WarehouseWriteOutcome(1, copy.Id);
            }, ct);
        }
        catch (Exception ex) when (ex is OutboundDuplicateException or OutboundWriteLostException)
        {
            outcome = WarehouseWriteOutcome.None;
        }

        if (outcome.Affected == 0)
            return await ReplayAsync(request.OutboundId, WarehouseContract.OpCancelOutboundShipment, request.IdempotencyKey, fingerprint, ct) ?? WarehouseSupport.Conflict<OutboundShipmentViewDto>();

        var (after, _) = await _releaser.ReleasePendingAsync(copy, Support.UserId, ct);
        return Response<OutboundShipmentViewDto>.Success(await Reads.ViewAsync(after, ct));
    }

    private WarehouseTaskEvent TaskEvent(WarehouseTask task, WarehouseTaskEventKind kind, DateTimeOffset now, Dictionary<string, string?> data) => new()
    {
        Id = Guid.NewGuid(), CreatedAt = now, TaskId = task.Id, Seq = ++task.EventSeq, Kind = kind, ActorUserId = Support.UserId, ActorName = Support.ActorName, AtUtc = now,
        CorrelationId = Support.CorrelationId, Data = data
    };
}

/// <summary>A compare-and-set inside a multi-document write lost: the whole transaction is abandoned and the command answers its 409 / replay.</summary>
public sealed class OutboundWriteLostException() : Exception("A record changed inside the outbound's write.");

/// <summary>
/// <c>releaseOutboundShipment</c> (pack §8.21; AC-30, AC-33 (v), AC-72, AC-77). Order: the reasons' shape (each ≤ 1000 in EVERY mode — 400
/// INVALID_REQUEST, r24 F-5; no repeated allocSeq); the key; the outbound (404); not <c>Packed</c> → 409 WMS_INVALID_TRANSITION; an Item outbound →
/// 409 WMS_ITEM_LEVEL_NOT_PUBLISHABLE (§8.24 rule 4); If-Match; an unknown allocSeq → 400. The pre-check (no write): for every allocation with a
/// picked quantity &gt; 0 and a lot — recall (422 LOT_UNDER_RECALL; the read fails → 503), TRACE status (Blocked / Expired → 422), retest due (422),
/// and the FEFO re-evaluation in EVERY mode on the free base (r22): an earlier-expiry free eligible lot now → Block 422 FEFO_ORDER_VIOLATION /
/// RequireReason 422 FEFO_BYPASS_REASON_REQUIRED without a reason / Warn passes; an allocation whose own lot override already carried a reason is
/// not asked again. Then ONE transaction: the reasons written to their allocations, <c>ReadyToShip</c> + <c>ReadyAtUtc</c>, the <c>Released</c>
/// key row and the <c>OutboundShipmentReady</c> outbox row (AC-30 K2: the outbox write failing rolls the whole command back).
/// </summary>
public sealed class ReleaseOutboundShipmentHandler : OutboundFlowHandler, IRequestHandler<ReleaseOutboundShipmentCommand, Response<OutboundShipmentViewDto>>
{
    private readonly IRecallLotStatusReader _recall;
    private readonly IWarehouseLotReader _lots;
    private readonly IWarehouseFefoReader _fefo;
    private readonly IInventoryReservationPort _reservations;

    public ReleaseOutboundShipmentHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads, WarehouseSupport support, IRecallLotStatusReader recall,
        IWarehouseLotReader lots, IWarehouseFefoReader fefo, IInventoryReservationPort reservations)
        : base(outbounds, reads, support)
    {
        _recall = recall;
        _lots = lots;
        _fefo = fefo;
        _reservations = reservations;
    }

    public async Task<Response<OutboundShipmentViewDto>> Handle(ReleaseOutboundShipmentCommand request, CancellationToken ct)
    {
        var reasons = new Dictionary<int, string?>();
        foreach (var r in request.FefoBypassReasons)
        {
            var text = OutboundAllocationRules.Reason(r.Reason);
            if (text is { Length: > OutboundAllocationRules.MaxReasonLength })
                return WarehouseSupport.Invalid<OutboundShipmentViewDto>("fefoBypassReasons[].reason must be at most 1000 characters.");
            if (!reasons.TryAdd(r.AllocSeq, text)) return WarehouseSupport.Invalid<OutboundShipmentViewDto>("fefoBypassReasons must not repeat an allocSeq.");
        }

        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpReleaseOutboundShipment), ("outboundId", request.OutboundId.ToString("D")),
            ("fefoBypassReasons", WarehouseSupport.List(reasons.OrderBy(r => r.Key).Select(r => $"{r.Key}:{r.Value}"))));
        if (await ReplayAsync(request.OutboundId, WarehouseContract.OpReleaseOutboundShipment, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var outbound = await Reads.FindVisibleAsync(request.OutboundId, ct);
        if (outbound is null) return OutboundShipmentReads.UnknownOutbound<OutboundShipmentViewDto>();
        if (outbound.Status != OutboundStatus.Packed) return Transition<OutboundShipmentViewDto>(outbound, "be made ready to ship");
        if (outbound.IsItemLevel())
        {
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(WarehouseErrorCodes.ItemLevelNotPublishable,
                "An Item-level outbound is not published in v1 (WAREHOUSE-OUTBOUND 1.0.0 and MVP-6 do not know the Item level).", 409);
        }

        if (outbound.Version != request.ExpectedVersion) return WarehouseSupport.Conflict<OutboundShipmentViewDto>();
        var allocations = outbound.Lines.SelectMany(l => l.Allocations.Select(a => (Line: l, Allocation: a))).ToList();
        if (reasons.Keys.Any(seq => allocations.All(x => x.Allocation.AllocSeq != seq)))
            return WarehouseSupport.Invalid<OutboundShipmentViewDto>("fefoBypassReasons[].allocSeq must name an allocation of this outbound.");

        var tasks = await Outbounds.PickTasksAsync(outbound.Id, ct);
        var picked = OutboundFlowRules.PickedByAllocation(tasks);
        var shipped = allocations.Where(x => picked.GetValueOrDefault(x.Allocation.AllocSeq) > 0m).ToList();
        var lotted = shipped.Where(x => x.Allocation.LotId is not null).ToList();

        // ── pre-check 1: recall (the read failing is 503, never "not recalled") ──
        var lotIds = lotted.Select(x => x.Allocation.LotId!).Distinct(StringComparer.Ordinal).ToList();
        if (lotIds.Count > 0)
        {
            IReadOnlySet<string> recalled;
            try
            {
                recalled = await _recall.UnderRecallAsync(lotIds, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return WarehouseSupport.Fail<OutboundShipmentViewDto>(InventoryErrorCodes.InventoryUnavailable, "The recall status of the lots could not be read; nothing was written.", 503);
            }

            if (lotted.FirstOrDefault(x => recalled.Contains(x.Allocation.LotId!)) is { Allocation: { } hit })
                return LotRefusal(InventoryErrorCodes.LotUnderRecall, "A picked lot is under recall; the outbound is not released.", hit);

            // ── pre-check 2: TRACE status ──
            var lots = await _lots.LotsAsync(lotIds, ct);
            foreach (var (_, a) in lotted)
            {
                if (!lots.TryGetValue(a.LotId!, out var lot)) continue;
                if (lot.Status == LotStatus.Blocked) return LotRefusal(InventoryErrorCodes.LotBlocked, "A picked lot is blocked; the outbound is not released.", a);
                if (lot.Status == LotStatus.Expired) return LotRefusal(InventoryErrorCodes.LotExpired, "A picked lot is expired; the outbound is not released.", a);
            }
        }

        // ── pre-check 3: expiry / retest on the FEFO read and the FEFO re-evaluation on the free base, every mode (r22) ──
        var mode = await _fefo.OutboundModeAsync(ct);
        var deviations = new List<(OutboundAllocation Allocation, Lot First)>();
        foreach (var group in lotted.GroupBy(x => (x.Line.ItemId, x.Line.SkuId, x.Line.SkuLevel)))
        {
            var order = await _fefo.OrderAsync(outbound.WarehouseId, group.Key.ItemId, group.Key.SkuId, group.Key.SkuLevel, null, Support.Now(), ct);
            var free = new List<FefoEligibleLot>();
            foreach (var e in order.Eligible)
            {
                var scopeFree = await _reservations.GetScopeFreeAsync(group.Key.ItemId, group.Key.SkuId, group.Key.SkuLevel, outbound.WarehouseId.ToString("D"), e.Lot.Id, ct);
                if (Math.Min(e.Available, scopeFree) > 0m) free.Add(e);
            }

            foreach (var (_, a) in group)
            {
                var excluded = order.Excluded.FirstOrDefault(x => x.Lot.Id.ToString("D") == a.LotId);
                if (excluded?.Reason == FefoExclusionReason.Expired) return LotRefusal(InventoryErrorCodes.LotExpired, "A picked lot is expired; the outbound is not released.", a);
                if (excluded?.Reason == FefoExclusionReason.RetestDue) return LotRefusal(InventoryErrorCodes.LotRetestDue, "A picked lot is due for retest; the outbound is not released.", a);
                if (a.FefoBypassReason is not null) continue; // the allocation's own reasoned lot change is not asked again
                var own = order.Eligible.FirstOrDefault(x => x.Lot.Id.ToString("D") == a.LotId)?.Lot ?? excluded?.Lot;
                if (own is null) continue;
                var decision = FefoIssueRule.Evaluate(own, new FefoOrder(free.Where(f => f.Lot.Id != own.Id).ToList(), []));
                if (!decision.Compliant) deviations.Add((a, decision.FirstEligible!));
            }
        }

        if (deviations.Count > 0 && mode == FefoOutboundMode.Block)
        {
            var (a, first) = deviations[0];
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(InventoryErrorCodes.FefoOrderViolation,
                $"FEFO: lot {first.LotNumber} must be issued first; this company refuses a lot out of FEFO order.", 422, new Dictionary<string, object?>
                {
                    ["allocSeq"] = a.AllocSeq, ["lotId"] = a.LotId, ["firstEligibleLotId"] = first.Id.ToString("D"), ["firstEligibleLotNumber"] = first.LotNumber,
                    ["firstEligibleExpiry"] = Day(first.ExpiryDate), ["deviations"] = deviations.Select(Deviation).ToList()
                });
        }

        var written = new Dictionary<int, string>();
        var missing = new List<(OutboundAllocation, Lot)>();
        foreach (var (a, first) in deviations)
        {
            var given = reasons.GetValueOrDefault(a.AllocSeq);
            if (given is { Length: >= OutboundAllocationRules.MinReasonLength }) written[a.AllocSeq] = given;
            else if (mode == FefoOutboundMode.RequireReason) missing.Add((a, first));
        }

        if (missing.Count > 0)
        {
            return WarehouseSupport.Fail<OutboundShipmentViewDto>(InventoryErrorCodes.FefoBypassReasonRequired,
                "A picked lot is no longer the first FEFO lot; give a reason (10-1000 characters) per allocation.", 422,
                new Dictionary<string, object?> { ["deviations"] = missing.Select(Deviation).ToList() });
        }

        // ── one transaction: reasons, ReadyToShip, the key row, the outbox row ──
        var now = Support.Now();
        var copy = outbound.Copy();
        foreach (var a in copy.AllAllocations().Where(a => written.ContainsKey(a.AllocSeq))) a.FefoBypassReason = written[a.AllocSeq];
        copy.Status = OutboundStatus.ReadyToShip;
        copy.ReadyAtUtc = PutawayPostingEngine.Second(now);
        copy.Version = outbound.Version + 1;
        copy.UpdatedAt = now;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        var released = Reads.NewEvent(copy, OutboundEventKind.Released, now, new Dictionary<string, string?>
        {
            ["readyAt"] = OutboundShipmentReads.Wire(copy.ReadyAtUtc.Value),
            ["fefoBypassReasons"] = written.Count == 0 ? null : JsonSerializer.Serialize(written.Select(w => new { allocSeq = w.Key, reason = w.Value }))
        }, request.IdempotencyKey, WarehouseContract.OpReleaseOutboundShipment, fingerprint);
        var eventId = Guid.NewGuid();
        var outbox = new WarehouseOutboxMessage
        {
            Id = Guid.NewGuid(), CreatedAt = now, Version = 1, AggregateId = copy.Id, EventId = eventId, CorrelationId = Support.CorrelationId, OccurredAt = now,
            PayloadJson = JsonSerializer.Serialize(OutboundFlowReads.ReadyEvent(copy, eventId, Support.CorrelationId))
        };
        // the frozen read of the tenant / company comes from the record: stamp the scope before the payload is built
        copy.TenantId ??= Support.TenantId;
        copy.LegalEntityId ??= Support.LegalEntityId;
        outbox.PayloadJson = JsonSerializer.Serialize(OutboundFlowReads.ReadyEvent(copy, eventId, Support.CorrelationId));

        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await Outbounds.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceOutboundAsync(copy, outbound.Version, tx)) return WarehouseWriteOutcome.None;
                await session.InsertEventsAsync([released], tx);
                await session.InsertOutboxAsync(outbox, tx);
                return new WarehouseWriteOutcome(1, copy.Id);
            }, ct);
        }
        catch (OutboundDuplicateException)
        {
            outcome = WarehouseWriteOutcome.None;
        }

        if (outcome.Affected > 0) return Response<OutboundShipmentViewDto>.Success(await Reads.ViewAsync(copy, ct));
        return await ReplayAsync(request.OutboundId, WarehouseContract.OpReleaseOutboundShipment, request.IdempotencyKey, fingerprint, ct)
               ?? WarehouseSupport.Conflict<OutboundShipmentViewDto>();
    }

    private static Response<OutboundShipmentViewDto> LotRefusal(string code, string message, OutboundAllocation a)
        => WarehouseSupport.Fail<OutboundShipmentViewDto>(code, message, 422, new Dictionary<string, object?>
            { ["allocSeq"] = a.AllocSeq, ["lotId"] = a.LotId, ["lotNumber"] = a.LotNumber });

    private static Dictionary<string, object?> Deviation((OutboundAllocation Allocation, Lot First) d) => new()
    {
        ["allocSeq"] = d.Allocation.AllocSeq, ["lotId"] = d.Allocation.LotId, ["firstEligibleLotId"] = d.First.Id.ToString("D"), ["firstEligibleExpiry"] = Day(d.First.ExpiryDate)
    };

    private static string? Day(DateTimeOffset? date) => date?.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

/// <summary>
/// <c>syncOutboundDispatch</c> (pack §8.21; AC-31, AC-35, AC-72, AC-78 (i)–(iii)). Only <c>ReadyToShip</c> (else 409 WMS_INVALID_TRANSITION),
/// If-Match. MVP-6 <c>queryShipments?sourceDocumentId={outboundId}</c>, then EVERY candidate's <c>getShipment</c> (one result too — r22): any read
/// Forbidden → 403 WMS_DISPATCH_READ_FORBIDDEN; otherwise any read Unavailable / NotFound → 503 WMS_DISPATCH_SOURCE_UNAVAILABLE; only candidates
/// with <c>sourceModule = MOD-0178</c> and <c>sourceType = WAREHOUSE_OUTBOUND</c> count — two → 409 WMS_DISPATCH_AMBIGUOUS, none → no change. The
/// one candidate dispatched (Dispatched / InTransit / Delivered / Exception / Closed, or Cancelled with <c>stockIssued: true</c>) → ONE transaction:
/// <c>Shipped</c>, <c>Shipment { … }</c> from its detail, the short-dispatch put-back tasks (+ their <c>Created</c> rows) or the unresolved SKU
/// rows, every allocation's release <c>Pending</c>, the <c>Shipped</c> key row; then the release rule outside it. No dispatch seen → 200 without a
/// write, a local row or a central record. 0178 never writes the sales issue (W-4: MVP-6 is its one writer).
/// </summary>
public sealed class SyncOutboundDispatchHandler : OutboundFlowHandler, IRequestHandler<SyncOutboundDispatchCommand, Response<SyncDispatchResultDto>>
{
    private readonly IShipmentReader _shipments;
    private readonly IWarehouseRecordCodeAllocator _numbers;
    private readonly OutboundReservationReleaser _releaser;

    public SyncOutboundDispatchHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads, WarehouseSupport support, IShipmentReader shipments,
        IWarehouseRecordCodeAllocator numbers, OutboundReservationReleaser releaser)
        : base(outbounds, reads, support)
    {
        _shipments = shipments;
        _numbers = numbers;
        _releaser = releaser;
    }

    public async Task<Response<SyncDispatchResultDto>> Handle(SyncOutboundDispatchCommand request, CancellationToken ct)
    {
        var fingerprint = WarehouseSupport.Fingerprint(("op", WarehouseContract.OpSyncOutboundDispatch), ("outboundId", request.OutboundId.ToString("D")));
        var earlier = await Outbounds.FindKeyEventAsync(request.OutboundId, Support.UserId, WarehouseContract.OpSyncOutboundDispatch, request.IdempotencyKey, ct);
        if (earlier is not null)
        {
            if (earlier.RequestFingerprint != fingerprint) return WarehouseSupport.KeyReused<SyncDispatchResultDto>();
            var current = await Reads.FindVisibleAsync(request.OutboundId, ct);
            return current is null
                ? OutboundShipmentReads.UnknownOutbound<SyncDispatchResultDto>()
                : Response<SyncDispatchResultDto>.Success(new SyncDispatchResultDto(true, current.Shipment?.Status, await Reads.ViewAsync(current, ct)));
        }

        var outbound = await Reads.FindVisibleAsync(request.OutboundId, ct);
        if (outbound is null) return OutboundShipmentReads.UnknownOutbound<SyncDispatchResultDto>();
        if (outbound.Status != OutboundStatus.ReadyToShip) return Transition<SyncDispatchResultDto>(outbound, "be synchronised with its shipment");
        if (outbound.Version != request.ExpectedVersion) return WarehouseSupport.Conflict<SyncDispatchResultDto>();

        // ── the MVP-6 reads (the MOD-0177 reader; caller's token + company) ──
        var query = await _shipments.QueryBySourceDocumentAsync(outbound.Id.ToString("D"), ct);
        if (query.Read == ShipmentReadOutcome.Forbidden) return Forbidden();
        if (query.Read != ShipmentReadOutcome.Ok) return Unavailable();
        var details = new List<ShipmentDetailResult>();
        foreach (var candidate in query.Shipments) details.Add(await _shipments.ReadDetailAsync(candidate.ShipmentId, ct));
        if (details.Any(d => d.Read == ShipmentReadOutcome.Forbidden)) return Forbidden();
        if (details.Any(d => d.Read != ShipmentReadOutcome.Ok)) return Unavailable();

        var mine = details.Where(d => d.SourceModule == WarehouseContract.ModuleId && d.SourceType == WarehouseContract.DispatchSourceType).ToList();
        if (mine.Count > 1)
        {
            return WarehouseSupport.Fail<SyncDispatchResultDto>(WarehouseErrorCodes.DispatchAmbiguous, "More than one MVP-6 shipment names this outbound as its source.", 409,
                new Dictionary<string, object?> { ["shipmentIds"] = mine.Select(m => m.ShipmentId).ToList() });
        }

        if (mine.Count == 0 || !OutboundFlowRules.IsDispatched(mine[0].Status, mine[0].StockIssued))
        {
            return Response<SyncDispatchResultDto>.Success(new SyncDispatchResultDto(false, mine.FirstOrDefault()?.Status, await Reads.ViewAsync(outbound, ct)));
        }

        var shipment = mine[0];
        var issued = new List<IssuedLine>();
        foreach (var line in shipment.Lines)
        {
            if (!Guid.TryParse(line.SkuId, out var sku) || !decimal.TryParse(line.Quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity))
                return Unavailable();
            issued.Add(new IssuedLine(sku, line.LotId, Guid.TryParse(line.FromLocationId, out var from) ? from : null, quantity));
        }

        var tasks = await Outbounds.PickTasksAsync(outbound.Id, ct);
        var picked = OutboundFlowRules.PickedByAllocation(tasks);
        var plan = OutboundFlowRules.ShortDispatch(outbound, picked, issued);
        var numbers = new List<string>();
        try
        {
            foreach (var _ in plan.Putbacks) numbers.Add(await _numbers.NextAsync(outbound.CompanyCodePrefix, WarehouseRecordCodeRules.PutawayTask, ct));
        }
        catch (WarehouseRecordCodeUnavailableException)
        {
            return WarehouseSupport.Fail<SyncDispatchResultDto>(WarehouseErrorCodes.RecordCodeUnavailable, "The put-back task numbers could not be issued; nothing was written.", 503);
        }

        var now = Support.Now();
        var copy = outbound.Copy();
        copy.Status = OutboundStatus.Shipped;
        copy.Shipment = new OutboundShipmentLink { ShipmentId = shipment.ShipmentId, ShipmentNumber = shipment.ShipmentNumber, Status = shipment.Status!, ObservedAtUtc = now };
        copy.ShortDispatchUnresolved = [.. plan.Unresolved];
        foreach (var a in copy.AllAllocations().Where(a => a.ReservationReleaseState == ReservationReleaseState.None)) a.ReservationReleaseState = ReservationReleaseState.Pending;
        copy.Version = outbound.Version + 1;
        copy.UpdatedAt = now;
        copy.UpdatedBy = Support.UserId;
        copy.UpdatedByName = Support.ActorName;
        var putbacks = plan.Putbacks.Select((p, i) => OutboundFlowReads.Putback(copy, WarehouseTaskOrigin.ShortDispatch, p, numbers[i], Support, now)).ToList();
        var putbackEvents = putbacks.Select(t => OutboundFlowReads.PutbackCreated(t, copy, Support, now)).ToList();
        var shippedEvent = Reads.NewEvent(copy, OutboundEventKind.Shipped, now, new Dictionary<string, string?>
        {
            ["shipmentId"] = shipment.ShipmentId, ["shipmentNumber"] = shipment.ShipmentNumber, ["shipmentStatus"] = shipment.Status,
            ["shortDispatch"] = JsonSerializer.Serialize(putbacks.Select(t => new
                { skuId = t.SkuId.ToString("D"), lotId = t.LotId, quantity = InventoryQuantity.ToWire(t.PlannedQuantity), taskId = t.Id.ToString("D") })),
            ["shortDispatchUnresolved"] = JsonSerializer.Serialize(plan.Unresolved.Select(u => new
            {
                skuId = u.SkuId.ToString("D"), lotId = u.LotId, expected = InventoryQuantity.ToWire(u.Expected), issued = InventoryQuantity.ToWire(u.Issued), reason = u.Reason
            }))
        }, request.IdempotencyKey, WarehouseContract.OpSyncOutboundDispatch, fingerprint);

        WarehouseWriteOutcome outcome;
        try
        {
            outcome = await Outbounds.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceOutboundAsync(copy, outbound.Version, tx)) return WarehouseWriteOutcome.None;
                await session.InsertEventsAsync([shippedEvent], tx);
                await session.InsertTasksAsync(putbacks, putbackEvents, tx);
                return new WarehouseWriteOutcome(1, copy.Id);
            }, ct);
        }
        catch (OutboundDuplicateException)
        {
            outcome = WarehouseWriteOutcome.None;
        }

        if (outcome.Affected == 0) return WarehouseSupport.Conflict<SyncDispatchResultDto>();
        var (after, _) = await _releaser.ReleasePendingAsync(copy, Support.UserId, ct);
        return Response<SyncDispatchResultDto>.Success(new SyncDispatchResultDto(true, shipment.Status, await Reads.ViewAsync(after, ct)));
    }

    private static Response<SyncDispatchResultDto> Forbidden()
        => WarehouseSupport.Fail<SyncDispatchResultDto>(WarehouseErrorCodes.DispatchReadForbidden,
            "MVP-6 refused the shipment read (the user needs supplychain.shipments.read); nothing was written.", 403);

    private static Response<SyncDispatchResultDto> Unavailable()
        => WarehouseSupport.Fail<SyncDispatchResultDto>(WarehouseErrorCodes.DispatchSourceUnavailable,
            "The MVP-6 shipment could not be read; nothing was written — try again.", 503);
}

/// <summary>
/// The maintenance service's <c>releaseOutboundReservations</c> (§8.17 step 6, §8.22; AC-32, AC-71 (vii)): an <c>AllocationFailed</c> /
/// <c>Shipped</c> / <c>Cancelled</c> outbound with <c>Pending</c> allocations, run in the outbound's scope with its CREATOR as actor; the release
/// rule of <see cref="OutboundReservationReleaser"/>. Nothing pending → success without a write; nothing released → the command fails (no record).
/// </summary>
public sealed class ReleaseOutboundReservationsHandler : IRequestHandler<ReleaseOutboundReservationsCommand, Response<bool>>
{
    private readonly IOutboundShipmentRepository _outbounds;
    private readonly OutboundReservationReleaser _releaser;
    private readonly WarehouseSupport _support;

    public ReleaseOutboundReservationsHandler(IOutboundShipmentRepository outbounds, OutboundReservationReleaser releaser, WarehouseSupport support)
    {
        _outbounds = outbounds;
        _releaser = releaser;
        _support = support;
    }

    public async Task<Response<bool>> Handle(ReleaseOutboundReservationsCommand request, CancellationToken ct)
    {
        var outbound = await _outbounds.FindAsync(request.OutboundId, ct);
        if (outbound is null || outbound.Status is not (OutboundStatus.AllocationFailed or OutboundStatus.Shipped or OutboundStatus.Cancelled)
                             || outbound.AllAllocations().All(a => a.ReservationReleaseState != ReservationReleaseState.Pending))
            return Response<bool>.Success(false);

        var (_, outcomes) = await _releaser.ReleasePendingAsync(outbound, _support.UserId, ct);
        return outcomes.Any(o => o.State == ReservationReleaseState.Released)
            ? Response<bool>.Success(true)
            : Response<bool>.Fail(WarehouseErrorCodes.Conflict, "No reservation of the outbound could be released yet; the next tick retries.", 409);
    }
}
