using System.Text.Json;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Entities;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Services;

/// <summary>What one release pass did to one allocation (the cancel / sync answer carries it per allocation).</summary>
public sealed record AllocationReleaseOutcome(int AllocSeq, ReservationReleaseState State, string? FailureReason);

/// <summary>
/// The ONE release rule (pack §8.22 "her yolda aynı"; cancel, the post-dispatch remainder of §8.21 and the maintenance's
/// <c>ReleaseOutboundReservationsCommand</c> incl. the FU03 400 branch of §8.17 step 6), for every allocation whose state is <c>Pending</c>:
/// <list type="number">
/// <item>read first — FU03 <c>GetAsync</c> (the latest status + version); an allocation WITHOUT a reservation id (only the FU03 400 branch,
/// <c>KeyReused</c>) is read with ONE <c>FindByIdempotencyKeysAsync(outbound tenant, outbound company, keys)</c>, whose row holds the same place;
/// <c>RELEASED</c> (any closed reason) / <c>EXPIRED</c> / not found → no call, <c>Released</c>;</item>
/// <item><c>ACTIVE</c> → <c>ReleaseAsync</c> with the version of THAT read (never a stored one) and the actor given (the cancelling / syncing
/// user; the maintenance passes the outbound's creator) → <c>Released</c>;</item>
/// <item>409 <c>RESERVATION_CONFLICT</c> → <c>Pending</c> + <c>ReservationReleaseFailed (Conflict)</c>; any other failure (an exception, a scope
/// mismatch refused fail-closed by FU03, 5xx) → <c>Pending</c> + <c>ReservationReleaseFailed</c> — the maintenance tries again.</item>
/// </list>
/// The result is written in ONE outbound write (states, the reservation ids found by key, the rows) — compare-and-set, re-read on a lost write.
/// Runs OUTSIDE the command's own transaction, after it (§8.22 "işlemin dışında, sıralı").
/// </summary>
public sealed class OutboundReservationReleaser
{
    private const int MaxWriteAttempts = 3;
    private const string Active = "ACTIVE";

    private readonly IOutboundShipmentRepository _outbounds;
    private readonly IInventoryReservationPort _reservations;
    private readonly OutboundShipmentReads _reads;
    private readonly WarehouseSupport _support;
    private readonly ILogger<OutboundReservationReleaser> _logger;

    public OutboundReservationReleaser(IOutboundShipmentRepository outbounds, IInventoryReservationPort reservations, OutboundShipmentReads reads,
        WarehouseSupport support, ILogger<OutboundReservationReleaser> logger)
    {
        _outbounds = outbounds;
        _reservations = reservations;
        _reads = reads;
        _support = support;
        _logger = logger;
    }

    public async Task<(OutboundShipment Outbound, IReadOnlyList<AllocationReleaseOutcome> Outcomes)> ReleasePendingAsync(OutboundShipment outbound, Guid actorUserId,
        CancellationToken ct)
    {
        var pending = outbound.AllAllocations().Where(a => a.ReservationReleaseState == ReservationReleaseState.Pending).ToList();
        if (pending.Count == 0) return (outbound, []);

        var released = new Dictionary<int, Guid?>();
        var failed = new Dictionary<int, string>();

        // (1) the keyless allocations (FU03 400 branch): ONE keyed read, the outbound's own tenant + company
        var keyless = pending.Where(a => a.ReservationId is null).ToList();
        if (keyless.Count > 0)
        {
            IReadOnlyList<ReservationKeyMatch>? matches = null;
            try
            {
                matches = await _reservations.FindByIdempotencyKeysAsync(outbound.TenantId ?? Guid.Empty, outbound.LegalEntityId ?? Guid.Empty,
                    keyless.Select(a => a.ReservationKey).ToList(), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "wms.outbound.release_keyed_read_failed {OutboundId}", outbound.Id);
                foreach (var a in keyless) failed[a.AllocSeq] = ex is InvalidOperationException ? "ScopeRefused" : "ReadFailed";
            }

            if (matches is not null)
            {
                foreach (var a in keyless)
                {
                    var match = matches.FirstOrDefault(m => string.Equals(m.IdempotencyKey, a.ReservationKey, StringComparison.Ordinal));
                    if (match is null)
                    {
                        released[a.AllocSeq] = null; // never created: the key is this outbound's own and the batch is never re-sent
                        continue;
                    }

                    await DecideAsync(a.AllocSeq, match.ReservationId, match.Status, match.Etag, actorUserId, released, failed, ct);
                }
            }
        }

        // (2) the allocations with a reservation id: GetAsync first
        foreach (var a in pending.Where(a => a.ReservationId is not null))
        {
            ReservationViewDto? view;
            try
            {
                view = await _reservations.GetAsync(a.ReservationId!.Value, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "wms.outbound.release_read_failed {OutboundId} {AllocSeq}", outbound.Id, a.AllocSeq);
                failed[a.AllocSeq] = "ReadFailed";
                continue;
            }

            if (view is null)
            {
                released[a.AllocSeq] = a.ReservationId;
                continue;
            }

            await DecideAsync(a.AllocSeq, a.ReservationId!.Value, view.Status, view.Etag, actorUserId, released, failed, ct);
        }

        var outcomes = pending.Select(a => released.ContainsKey(a.AllocSeq)
            ? new AllocationReleaseOutcome(a.AllocSeq, ReservationReleaseState.Released, null)
            : new AllocationReleaseOutcome(a.AllocSeq, ReservationReleaseState.Pending, failed.GetValueOrDefault(a.AllocSeq, "ReadFailed"))).ToList();
        return (await WriteAsync(outbound, released, failed, ct), outcomes);
    }

    private async Task DecideAsync(int allocSeq, Guid reservationId, string status, string etag, Guid actor, Dictionary<int, Guid?> released,
        Dictionary<int, string> failed, CancellationToken ct)
    {
        if (!string.Equals(status, Active, StringComparison.Ordinal))
        {
            released[allocSeq] = reservationId; // RELEASED (Consumed / Released) or EXPIRED: no call — FU03 answers 409 on a closed reservation
            return;
        }

        Response<ReservationResponseDto> answer;
        try
        {
            answer = await _reservations.ReleaseAsync(reservationId, etag, actor, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "wms.outbound.release_failed {ReservationId}", reservationId);
            failed[allocSeq] = "ReleaseFailed";
            return;
        }

        if (answer.IsSuccessful) released[allocSeq] = reservationId;
        else failed[allocSeq] = answer.StatusCode == 409 ? "Conflict" : answer.ErrorCode ?? "ReleaseFailed";
    }

    private async Task<OutboundShipment> WriteAsync(OutboundShipment outbound, IReadOnlyDictionary<int, Guid?> released, IReadOnlyDictionary<int, string> failed,
        CancellationToken ct)
    {
        var current = outbound;
        for (var attempt = 0; attempt < MaxWriteAttempts; attempt++)
        {
            var now = _support.Now();
            var copy = current.Copy();
            var doneNow = new List<OutboundAllocation>();
            foreach (var a in copy.AllAllocations().Where(a => a.ReservationReleaseState == ReservationReleaseState.Pending))
            {
                if (!released.TryGetValue(a.AllocSeq, out var id)) continue;
                a.ReservationReleaseState = ReservationReleaseState.Released;
                a.ReservationId ??= id;
                doneNow.Add(a);
            }

            var events = new List<OutboundShipmentEvent>();
            if (doneNow.Count > 0)
            {
                events.Add(_reads.NewEvent(copy, OutboundEventKind.ReservationsReleased, now, new Dictionary<string, string?>
                {
                    ["allocations"] = JsonSerializer.Serialize(doneNow.Select(a => new { a.AllocSeq, reservationId = a.ReservationId?.ToString("D") }))
                }));
            }

            var stillFailed = failed.Where(f => copy.AllAllocations().Any(a => a.AllocSeq == f.Key && a.ReservationReleaseState == ReservationReleaseState.Pending)).ToList();
            if (stillFailed.Count > 0)
            {
                events.Add(_reads.NewEvent(copy, OutboundEventKind.ReservationReleaseFailed, now, new Dictionary<string, string?>
                {
                    ["allocations"] = JsonSerializer.Serialize(stillFailed.Select(f => new { allocSeq = f.Key, reason = f.Value })),
                    ["reason"] = stillFailed.Select(f => f.Value).Distinct().Count() == 1 ? stillFailed[0].Value : "Mixed"
                }));
            }

            if (events.Count == 0) return current;
            copy.Version = current.Version + 1;
            copy.UpdatedAt = now;
            var outcome = await _outbounds.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceOutboundAsync(copy, current.Version, tx)) return WarehouseWriteOutcome.None;
                await session.InsertEventsAsync(events, tx);
                return new WarehouseWriteOutcome(1, copy.Id);
            }, ct);
            if (outcome.Affected > 0) return copy;
            current = await _outbounds.FindAsync(outbound.Id, ct) ?? throw new InvalidOperationException("An outbound vanished while its release was written.");
        }

        _logger.LogWarning("wms.outbound.release_write_conflict {OutboundId}: the release result was not written; the maintenance repeats it.", outbound.Id);
        return current;
    }
}
