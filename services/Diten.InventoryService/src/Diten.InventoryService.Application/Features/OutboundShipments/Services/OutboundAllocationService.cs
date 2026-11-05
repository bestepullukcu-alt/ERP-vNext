using System.Globalization;
using System.Text.Json;
using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Application.Features.Inventory.Commands;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Services;

/// <summary>
/// Pack §8.17 steps 5b–6 — what follows transaction A, shared by the request (<c>CreateOutboundShipmentCommand</c>) and the maintenance service
/// (<c>CompleteOutboundAllocationCommand</c>): the FU03 batch built from the PERSISTED plan (never recomputed — the body FU03 sees is byte-equal
/// on every attempt, so a repeat is FU03's replay), then exactly one of
/// <list type="bullet">
/// <item>(c) transaction B — reservation ids, <c>Allocated</c>, the pick tasks and their <c>Created</c> rows, the <c>Allocated</c> row — CAS on
/// the version read;</item>
/// <item>the FU03 refusal → <c>AllocationFailed</c> + <c>AllocationFailure { BatchRefused, lines }</c> (no reservation: the batch is atomic);</item>
/// <item>FU03 400 <c>IDEMPOTENCY_KEY_REUSED</c> → <c>AllocationFailed</c> + <c>{ KeyReused }</c> and EVERY allocation <c>Pending</c> (a
/// reservation may exist under one of the keys — r24), an error log;</item>
/// <item>an unknown outcome (exception, cancellation, a 503 answer) → nothing written, 503 <c>INVENTORY_UNAVAILABLE</c> /
/// <c>OutcomeUnknown</c>: the record stays <c>Allocating</c> and only the maintenance service completes it (a failed answer is no proof that
/// no reservation was made).</item>
/// </list>
/// </summary>
public sealed class OutboundAllocationService
{
    private const int MaxWriteAttempts = 3;

    private readonly IOutboundShipmentRepository _outbounds;
    private readonly IInventoryReservationPort _reservations;
    private readonly IWarehouseRecordCodeAllocator _numbers;
    private readonly OutboundShipmentReads _reads;
    private readonly WarehouseSupport _support;
    private readonly ILogger<OutboundAllocationService> _logger;

    public OutboundAllocationService(IOutboundShipmentRepository outbounds, IInventoryReservationPort reservations, IWarehouseRecordCodeAllocator numbers,
        OutboundShipmentReads reads, WarehouseSupport support, ILogger<OutboundAllocationService> logger)
    {
        _outbounds = outbounds;
        _reservations = reservations;
        _numbers = numbers;
        _reads = reads;
        _support = support;
        _logger = logger;
    }

    /// <summary>The FU03 lines of the persisted plan (§8.10 FU03 Q-07: type, source, no <c>expiresAt</c>, no priority) — the same on every attempt.</summary>
    public static IReadOnlyList<CreateReservationCommand> Batch(OutboundShipment outbound)
        => outbound.Lines.SelectMany(line => line.Allocations.Select(a => new CreateReservationCommand
        {
            IdempotencyKey = a.ReservationKey,
            ItemId = line.ItemId.ToString("D"),
            SkuId = line.SkuId.ToString("D"),
            SkuLevel = WireNames.ToWire(line.SkuLevel),
            Quantity = InventoryQuantity.ToWire(a.Quantity),
            ReservationType = OutboundAllocationRules.ReservationType,
            SourceModule = WarehouseContract.ReservationSourceModule,
            SourceDocumentId = outbound.Id.ToString("D"),
            SourceLineId = line.LineSeq.ToString(CultureInfo.InvariantCulture),
            WarehouseId = outbound.WarehouseId.ToString("D"),
            LotId = a.LotId
        })).ToList();

    /// <summary>
    /// Steps 5b–5c for an <c>Allocating</c> outbound (version as read). <paramref name="createdStatus"/>: 201 for the request, 200 for the
    /// maintenance service. The actor of FU03 is the caller's scope user (the request's user / the outbound's creator in maintenance).
    /// </summary>
    public async Task<Response<OutboundShipmentViewDto>> ReserveAndCompleteAsync(OutboundShipment outbound, int createdStatus, CancellationToken ct)
    {
        ReservationBatchResponse batch;
        try
        {
            batch = await _reservations.CreateBatchAsync(Batch(outbound), outbound.LegalEntityId ?? Guid.Empty, _support.UserId, ct);
        }
        catch (Exception ex)
        {
            // an exception, a timeout or a cancellation may come AFTER the commit (S1 §8.3 Codex P2 pattern) — never read as "not reserved"
            _logger.LogWarning(ex, "wms.outbound.allocation_outcome_unknown {OutboundId} {OutboundNumber}", outbound.Id, outbound.OutboundNumber);
            return OutcomeUnknown();
        }

        if (batch.IsSuccessful) return await CompleteAsync(outbound, batch, createdStatus, ct);
        if (batch.StatusCode == 400 && batch.ErrorCode == InventoryErrorCodes.IdempotencyKeyReused)
        {
            _logger.LogError("wms.outbound.reservation_key_conflict {OutboundId} {OutboundNumber}: FU03 refused the batch's keys as reused; the allocations are marked for release.",
                outbound.Id, outbound.OutboundNumber);
            return await FailAsync(outbound, new OutboundAllocationFailure { Kind = AllocationFailureKind.KeyReused, AtUtc = _support.Now() }, ct);
        }

        if (batch.StatusCode is 400 or 422)
        {
            var allocations = outbound.Lines.SelectMany(l => l.Allocations.Select(a => (Line: l, Allocation: a))).ToList();
            var failure = new OutboundAllocationFailure { Kind = AllocationFailureKind.BatchRefused, AtUtc = _support.Now() };
            foreach (var line in batch.Lines.Where(l => l.ErrorCode is not null && l.Index >= 0 && l.Index < allocations.Count))
            {
                var (ol, a) = allocations[line.Index];
                failure.Lines.Add(new OutboundAllocationFailureLine
                {
                    LineSeq = ol.LineSeq, Code = line.ErrorCode, Requested = Dec(line.Requested) ?? a.Quantity, Allocatable = Dec(line.Available)
                });
            }

            if (failure.Lines.Count == 0)
            {
                failure.Lines.AddRange(outbound.Lines.Select(l => new OutboundAllocationFailureLine
                    { LineSeq = l.LineSeq, Code = batch.ErrorCode, Requested = l.RequestedQuantity }));
            }

            return await FailAsync(outbound, failure, ct);
        }

        return OutcomeUnknown(); // 503 (LOCATION unreadable) and anything else: not a decision — the maintenance service re-sends the same batch
    }

    public static Response<OutboundShipmentViewDto> OutcomeUnknown()
        => Response<OutboundShipmentViewDto>.Fail(InventoryErrorCodes.InventoryUnavailable,
            "The reservation's outcome is not known yet; the outbound is completed by the warehouse maintenance with the same request.", 503,
            new Dictionary<string, object?> { ["reason"] = "OutcomeUnknown" });

    /// <summary>The 422 of a failed allocation — the request's answer and every replay of it (from the stored <c>AllocationFailure</c>).</summary>
    public static Response<T> FailureResponse<T>(OutboundAllocationFailure failure)
        => failure.Kind == AllocationFailureKind.KeyReused
            ? Response<T>.Fail(WarehouseErrorCodes.AllocationFailed, "The allocation could not be completed (reservation key conflict); nothing was opened.", 422,
                new Dictionary<string, object?> { ["reason"] = "ReservationKeyConflict", ["lines"] = Array.Empty<object>() })
            : Response<T>.Fail(WarehouseErrorCodes.AllocationFailed, "The stock could not be reserved for every line; nothing was opened.", 422,
                new Dictionary<string, object?> { ["lines"] = failure.Lines.Select(Row).ToList() });

    /// <summary>The 422 of a plan that could not cover a line (§8.17 "Adım 2–4'te bir satır karşılanamazsa yazım hiç başlamaz").</summary>
    public static Response<T> PlanFailureResponse<T>(IReadOnlyList<PlanFailure> failures)
        => Response<T>.Fail(WarehouseErrorCodes.AllocationFailed, "The stock cannot cover every line; nothing was opened.", 422,
            new Dictionary<string, object?>
            {
                ["lines"] = failures.Select(f => Row(new OutboundAllocationFailureLine
                    { LineSeq = f.LineSeq, Code = f.Code, Requested = f.Requested, Allocatable = f.Allocatable })).ToList()
            });

    private static Dictionary<string, object?> Row(OutboundAllocationFailureLine l)
    {
        var row = new Dictionary<string, object?> { ["lineSeq"] = l.LineSeq, ["requested"] = InventoryQuantity.ToWire(l.Requested) };
        if (l.Code is not null) row["code"] = l.Code;
        if (l.Allocatable is { } a) row["allocatable"] = InventoryQuantity.ToWire(a);
        return row;
    }

    private static decimal? Dec(string? wire)
        => decimal.TryParse(wire, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    // ── transaction B ────────────────────────────────────────────────────────────────────────────────────

    private async Task<Response<OutboundShipmentViewDto>> CompleteAsync(OutboundShipment outbound, ReservationBatchResponse batch, int createdStatus,
        CancellationToken ct)
    {
        var byKey = batch.Lines.Where(l => l.IdempotencyKey is not null && l.Reservation is not null)
            .ToDictionary(l => l.IdempotencyKey!, l => Guid.Parse(l.Reservation!.ReservationId), StringComparer.Ordinal);
        if (outbound.AllAllocations().Any(a => !byKey.ContainsKey(a.ReservationKey)))
        {
            _logger.LogError("wms.outbound.batch_without_reservation {OutboundId}: an FU03 success named no reservation for a key.", outbound.Id);
            return OutcomeUnknown();
        }

        // the pick task numbers come from the counter, outside the transaction (a number taken by a write that later fails is skipped, Q-07 B)
        var numbers = new Dictionary<(int AllocSeq, Guid Bin), string>();
        try
        {
            foreach (var a in outbound.AllAllocations())
            {
                foreach (var bin in a.PlannedBins) numbers[(a.AllocSeq, bin.LocationId)] = await _numbers.NextAsync(outbound.CompanyCodePrefix, WarehouseRecordCodeRules.PickTask, ct);
            }
        }
        catch (WarehouseRecordCodeUnavailableException)
        {
            return Response<OutboundShipmentViewDto>.Fail(WarehouseErrorCodes.RecordCodeUnavailable,
                "The pick task numbers could not be issued; the outbound is completed by the warehouse maintenance.", 503);
        }

        var current = outbound;
        for (var attempt = 0; attempt < MaxWriteAttempts; attempt++)
        {
            if (current.Status != OutboundStatus.Allocating) return await SettledAsync(current, createdStatus, ct);

            var now = _support.Now();
            var copy = current.Copy();
            copy.Status = OutboundStatus.Allocated;
            copy.AllocationLeaseUntilUtc = null;
            copy.Version = current.Version + 1;
            copy.UpdatedAt = now;
            foreach (var a in copy.AllAllocations()) a.ReservationId = byKey[a.ReservationKey];

            var tasks = new List<WarehouseTask>();
            var taskEvents = new List<WarehouseTaskEvent>();
            foreach (var line in copy.Lines)
            {
                foreach (var a in line.Allocations)
                {
                    foreach (var bin in a.PlannedBins)
                    {
                        var task = PickTask(copy, line, a, bin, numbers[(a.AllocSeq, bin.LocationId)], now);
                        tasks.Add(task);
                        taskEvents.Add(TaskCreated(task, now));
                    }
                }
            }

            var events = new List<OutboundShipmentEvent>
            {
                _reads.NewEvent(copy, OutboundEventKind.Allocated, now, new Dictionary<string, string?>
                {
                    ["allocations"] = JsonSerializer.Serialize(copy.AllAllocations().Select(a => new
                    {
                        a.AllocSeq, a.LotId, quantity = InventoryQuantity.ToWire(a.Quantity), reservationId = a.ReservationId?.ToString("D"), a.ReservationKey
                    })),
                    ["pickTasks"] = string.Join(",", tasks.Select(t => t.Number))
                })
            };

            var outcome = await _outbounds.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceOutboundAsync(copy, current.Version, tx)) return WarehouseWriteOutcome.None;
                await session.InsertEventsAsync(events, tx);
                await session.InsertPickTasksAsync(tasks, taskEvents, tx);
                return new WarehouseWriteOutcome(1, copy.Id);
            }, ct);
            if (outcome.Affected > 0) return Response<OutboundShipmentViewDto>.Success(await _reads.ViewAsync(copy, ct), createdStatus);

            current = await _outbounds.FindAsync(outbound.Id, ct) ?? throw new InvalidOperationException("An outbound vanished while it was completed.");
        }

        return WarehouseSupport.Conflict<OutboundShipmentViewDto>();
    }

    /// <summary>Another writer (the request or the maintenance service) settled the outbound first: answer what it is now.</summary>
    private async Task<Response<OutboundShipmentViewDto>> SettledAsync(OutboundShipment current, int createdStatus, CancellationToken ct)
        => current.Status == OutboundStatus.AllocationFailed && current.AllocationFailure is { } failure
            ? FailureResponse<OutboundShipmentViewDto>(failure)
            : Response<OutboundShipmentViewDto>.Success(await _reads.ViewAsync(current, ct), createdStatus);

    private static WarehouseTask PickTask(OutboundShipment o, OutboundLine line, OutboundAllocation a, OutboundPlannedBin bin, string number, DateTimeOffset now)
        => new()
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            Number = number,
            CompanyCodePrefix = o.CompanyCodePrefix,
            WarehouseCode = o.WarehouseCode,
            Kind = WarehouseTaskKind.Pick,
            Status = WarehouseTaskStatus.Open,
            Origin = WarehouseTaskOrigin.Outbound,
            SourceRef = new WarehouseTaskSourceRef
            {
                SourceModule = WarehouseContract.ReservationSourceModule, SourceType = OutboundAllocationRules.ReservationType,
                SourceDocumentId = o.Id.ToString("D"), SourceLineId = line.LineSeq.ToString(CultureInfo.InvariantCulture)
            },
            WarehouseId = o.WarehouseId,
            ItemId = line.ItemId,
            SkuId = line.SkuId,
            SkuLevel = line.SkuLevel,
            LotId = a.LotId,
            LotNumber = a.LotNumber,
            SerialControlled = line.SerialControlled,
            StockStatus = StockStatus.Available,
            FromLocationId = bin.LocationId,
            ToLocationId = o.StagingLocationId,
            PlannedQuantity = bin.Quantity,
            BaseUomId = line.BaseUomId,
            CreatedBy = o.CreatedBy,
            CreatedByName = o.CreatedByName,
            CorrelationId = o.CorrelationId,
            OutboundId = o.Id,
            LineSeq = line.LineSeq,
            AllocSeq = a.AllocSeq,
            ReservationId = a.ReservationId,
            FefoRank = a.FefoRank,
            OutboundNumber = o.OutboundNumber // DL-5: the pick movement's documentGroupId (§8.19)
        };

    private WarehouseTaskEvent TaskCreated(WarehouseTask task, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        CreatedAt = now,
        TaskId = task.Id,
        Seq = ++task.EventSeq,
        Kind = WarehouseTaskEventKind.Created,
        ActorUserId = _support.UserId,
        ActorName = _support.ActorName,
        AtUtc = now,
        CorrelationId = _support.CorrelationId,
        Data = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["number"] = task.Number, ["origin"] = task.Origin.ToString(), ["outboundId"] = task.OutboundId?.ToString("D"),
            ["allocSeq"] = task.AllocSeq?.ToString(CultureInfo.InvariantCulture), ["fromLocationId"] = task.FromLocationId.ToString("D"),
            ["toLocationId"] = task.ToLocationId?.ToString("D"), ["lotId"] = task.LotId, ["plannedQuantity"] = InventoryQuantity.ToWire(task.PlannedQuantity),
            ["reservationId"] = task.ReservationId?.ToString("D")
        }
    };

    // ── AllocationFailed ─────────────────────────────────────────────────────────────────────────────────

    private async Task<Response<OutboundShipmentViewDto>> FailAsync(OutboundShipment outbound, OutboundAllocationFailure failure, CancellationToken ct)
    {
        var current = outbound;
        for (var attempt = 0; attempt < MaxWriteAttempts; attempt++)
        {
            if (current.Status != OutboundStatus.Allocating) return await SettledAsync(current, 200, ct);

            var now = _support.Now();
            var copy = current.Copy();
            copy.Status = OutboundStatus.AllocationFailed;
            copy.AllocationLeaseUntilUtc = null;
            copy.AllocationFailure = failure.Copy();
            copy.Version = current.Version + 1;
            copy.UpdatedAt = now;
            if (failure.Kind == AllocationFailureKind.KeyReused)
            {
                // r24: FU03's 400 is no proof that none of the keys made a reservation — every allocation waits for the keyed release (DL-5)
                foreach (var a in copy.AllAllocations()) a.ReservationReleaseState = ReservationReleaseState.Pending;
            }

            var data = new Dictionary<string, string?>
            {
                ["kind"] = failure.Kind.ToString(),
                ["lines"] = JsonSerializer.Serialize(failure.Lines.Select(l => new
                {
                    l.LineSeq, l.Code, requested = InventoryQuantity.ToWire(l.Requested), allocatable = l.Allocatable is { } x ? InventoryQuantity.ToWire(x) : null
                }))
            };
            if (failure.Kind == AllocationFailureKind.KeyReused) data["code"] = InventoryErrorCodes.IdempotencyKeyReused;
            var events = new List<OutboundShipmentEvent> { _reads.NewEvent(copy, OutboundEventKind.AllocationFailed, now, data) };
            var outcome = await _outbounds.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceOutboundAsync(copy, current.Version, tx)) return WarehouseWriteOutcome.None;
                await session.InsertEventsAsync(events, tx);
                return new WarehouseWriteOutcome(1, copy.Id);
            }, ct);
            if (outcome.Affected > 0) return FailureResponse<OutboundShipmentViewDto>(failure);
            current = await _outbounds.FindAsync(outbound.Id, ct) ?? throw new InvalidOperationException("An outbound vanished while its failure was written.");
        }

        return WarehouseSupport.Conflict<OutboundShipmentViewDto>();
    }
}
