using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability.Commands;
using Diten.InventoryService.Application.Features.Traceability.Ports;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;

/// <summary>
/// changeLotStatus (K-E; pack §12 S / R / C; B-3). Order:
/// <list type="number">
/// <item>Shape (400): idempotency key, <c>If-Match</c>, a known status, reason ≥ 10 (<c>REASON_REQUIRED</c>),
/// Blocked acknowledged (<c>ACKNOWLEDGEMENT_REQUIRED</c>).</item>
/// <item>Idempotency: a committed key on this lot answers with the lot — same fingerprint → 200, else 400.</item>
/// <item>One transaction: the lot (404), the ETag (409), the lifecycle (<see cref="LotStatusRules.CanTransition"/>:
/// Expired never by hand, 422 <c>LOT_STATUS_TRANSITION_INVALID</c>), the 0175 gate, replace + one
/// <c>StatusChanged</c> history row carrying the key.</item>
/// </list>
/// The balance rows' stock status is NOT touched ("bloke lot ≠ bloke stok"); what a Blocked lot means for availability
/// is the ledger's (CT-D1, slice P2).
/// </summary>
public sealed class ChangeLotStatusHandler : IRequestHandler<ChangeLotStatusCommand, Response<LotRefDto>>
{
    private readonly ILotRepository _lots;
    private readonly ILotStatusChangeGate _gate;
    private readonly TraceWriteSupport _support;

    public ChangeLotStatusHandler(ILotRepository lots, ILotStatusChangeGate gate, TraceWriteSupport support)
    {
        _lots = lots;
        _gate = gate;
        _support = support;
    }

    public async Task<Response<LotRefDto>> Handle(ChangeLotStatusCommand request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var today = TraceMapping.Today(now);

        var keyError = TraceWriteSupport.KeyError(request.HeaderIdempotencyKey, request.IdempotencyKey, out var key);
        if (keyError is not null) return TraceWriteSupport.Invalid<LotRefDto>(keyError);
        if (string.IsNullOrWhiteSpace(request.IfMatch))
            return TraceWriteSupport.Invalid<LotRefDto>("The If-Match header is required.");
        if (!Enum.TryParse<LotStatus>(request.ToStatus, ignoreCase: false, out var target)
            || !Enum.IsDefined(target) || int.TryParse(request.ToStatus, out _))
            return TraceWriteSupport.Invalid<LotRefDto>("toStatus is required: Draft, Released, Blocked or Expired.");

        var reason = TraceWriteSupport.Trimmed(request.Reason);
        if (!LotStatusRules.IsReasonValid(reason))
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.ReasonRequired,
                $"A status change needs a reason of at least {LotStatusRules.MinReasonLength} characters.", 400);
        if (LotStatusRules.RequiresAcknowledgement(target) && request.AcknowledgeImpact != true)
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.AcknowledgementRequired,
                "Blocking a lot makes all of its stock unusable: acknowledgeImpact must be true.", 400);

        if (!TraceWriteSupport.TryParseId(request.LotId, out var lotId)) return TraceWriteSupport.UnknownLot<LotRefDto>();
        var expectedVersion = TraceWriteSupport.ExpectedVersion(request.IfMatch);
        var fingerprint = TraceWriteSupport.Fingerprint(
            ("toStatus", target.ToString()), ("reason", reason), ("acknowledgeImpact", request.AcknowledgeImpact == true ? "true" : "false"));

        if (await _lots.FindStatusChangeByKeyAsync(lotId, key, cancellationToken) is { } committed)
            return await ReplayAsync(lotId, committed.RequestFingerprint, fingerprint, today, cancellationToken);

        try
        {
            return await _lots.InTransactionAsync(async (tx, ct) =>
            {
                var lot = await tx.FindAsync(lotId, ct);
                if (lot is null) return TraceWriteSupport.UnknownLot<LotRefDto>();
                if (lot.Version != expectedVersion) return TraceWriteSupport.Conflict<LotRefDto>();
                if (!LotStatusRules.CanTransition(lot.Status, target))
                {
                    return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotStatusTransitionInvalid,
                        lot.Status == LotStatus.Expired || target == LotStatus.Expired
                            ? "Expired is set by the system when the expiry date has passed; it can never be set or left by hand."
                            : $"A {lot.Status} lot cannot become {target}.",
                        422);
                }

                if (await _gate.EvaluateAsync(lot, target, reason!, ct) != LotStatusGateDecision.Allow)
                {
                    // MOD-0175 owns approval / e-signature (DEC-INV-08); until it exists no gate answers anything else.
                    throw new NotSupportedException("A pending-approval lot status change belongs to MOD-0175.");
                }

                var entry = _support.History(lot.Id, LotChangeAction.StatusChanged, "status", lot.Status.ToString(), target.ToString(), reason, now);
                entry.IdempotencyKey = key;
                entry.RequestFingerprint = fingerprint;
                lot.Status = target;
                lot.UpdatedAt = now;
                lot.UpdatedBy = _support.UserId;
                if (!await tx.ReplaceAsync(lot, expectedVersion, ct)) return TraceWriteSupport.Conflict<LotRefDto>();
                await tx.AppendHistoryAsync([entry], ct);
                return Response<LotRefDto>.Success(TraceMapping.ToRef(lot, today, withETag: true), 200);
            }, cancellationToken);
        }
        catch (LotUniqueViolationException violation) when (violation.Index == LotUniqueIndex.StatusChangeKey)
        {
            // A concurrent request committed the same key on this lot first.
            var winner = await _lots.FindStatusChangeByKeyAsync(lotId, key, cancellationToken)
                         ?? throw new InvalidOperationException("Status key reported taken but no history row carries it.");
            return await ReplayAsync(lotId, winner.RequestFingerprint, fingerprint, today, cancellationToken);
        }
    }

    private async Task<Response<LotRefDto>> ReplayAsync(Guid lotId, string? committedFingerprint, string fingerprint, DateOnly today, CancellationToken ct)
    {
        if (!string.Equals(committedFingerprint, fingerprint, StringComparison.Ordinal))
            return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.IdempotencyKeyReused,
                "This idempotency key was already used for a different status change.", 400);
        var lot = await _lots.FindAsync(lotId, ct);
        return lot is null
            ? TraceWriteSupport.UnknownLot<LotRefDto>()
            : Response<LotRefDto>.Success(TraceMapping.ToRef(lot, today, withETag: true), 200);
    }
}
