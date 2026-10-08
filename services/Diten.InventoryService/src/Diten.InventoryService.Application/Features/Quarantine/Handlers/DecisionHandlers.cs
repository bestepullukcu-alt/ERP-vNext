using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Traceability.Enums;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers;

/// <summary>
/// The pack §12 checks of a decision against its target, shared by create (draft) and submit (re-check: the stock or the lot
/// may have changed in between). Returns the first violation, in pack order.
/// </summary>
public sealed class QuarantineDecisionChecks
{
    private readonly QuarantineFacts _facts;

    public QuarantineDecisionChecks(QuarantineFacts facts) => _facts = facts;

    public async Task<QuarantineViolation?> HoldAsync(
        QuarantineHold hold, IReadOnlyList<RequestedLine> lines, string? blockReasonCode, bool coaChecked, DateOnly? newRetestDate,
        Guid? ownDecisionId, CancellationToken ct)
    {
        if (QuarantineDecisionRules.HoldState(hold.Status == HoldStatus.DecisionPending && hold.ActiveDecisionId == ownDecisionId ? HoldStatus.Open : hold.Status,
                hold.ActiveDecisionId == ownDecisionId ? null : hold.ActiveDecisionId) is { } v6)
            return v6;
        if (QuarantineDecisionRules.Lines(lines, hold.Quantity) is { } v12) return v12;
        if (QuarantineDecisionRules.Serials(hold.SerialIds, hold.Quantity, lines.Select(l => l.Quantity)) is { } ser) return ser;
        if (QuarantineDecisionRules.Coa(lines, coaChecked) is { } v4) return v4;
        var needsReason = lines.Any(l => l.Decision is LineDecision.Reject or LineDecision.Block);
        if (QuarantineDecisionRules.BlockReason(needsReason, blockReasonCode, needsReason ? await _facts.ActiveBlockReasonsAsync(ct) : new HashSet<string>()) is { } v5)
            return v5;
        var lot = await _facts.LotAsync(hold.LotId, ct);
        if (QuarantineDecisionRules.LotHold(QuarantineFacts.IsHeld(lot), DecisionKind.HoldDecision, null) is { } v8) return v8;
        // KQ-H: a release of a Retest hold — or of any hold of a lot whose retest date has passed (Q4: the release would be refused
        // LOT_RETEST_DUE at apply otherwise) — needs the new retest date the approval puts on the lot.
        var retestRelease = lines.Any(l => l.Decision == LineDecision.Release)
                            && (hold.Source == HoldSource.Retest || Diten.InventoryService.Domain.Traceability.Rules.LotDateRules.RetestDue(
                                lot?.RetestDate is { } rd ? DateOnly.FromDateTime(rd.UtcDateTime) : null, QuarantineSupport.Today()));
        if (QuarantineDecisionRules.RetestDate(retestRelease, newRetestDate, QuarantineSupport.Today(),
                lot?.ExpiryDate is { } e ? DateOnly.FromDateTime(e.UtcDateTime) : null) is { } v9)
            return v9;
        var (onHand, _) = await _facts.BucketAsync(hold.ItemId, hold.SkuId, hold.SkuLevel, hold.WarehouseId, hold.LocationId, hold.LotId, hold.StockStatus, ct);
        return QuarantineDecisionRules.Bucket(onHand, hold.Quantity);
    }

    /// <summary>
    /// Q4 (KQ-I) — a LotRelease on submit: the lot still exists and is still in the status it was requested from (Draft / Blocked; an
    /// Expired, already Released or meanwhile blocked lot has
    /// nothing to approve → 409 <c>QUARANTINE_DECISION_NOT_APPLICABLE</c>), and no other LotRelease of the lot is waiting (V6 for a lot
    /// → 409 <c>QUARANTINE_DECISION_ALREADY_PENDING</c>).
    /// </summary>
    public async Task<QuarantineViolation?> LotReleaseAsync(QuarantineDecision decision, QuarantineLotReleaseApprovals approvals, CancellationToken ct)
    {
        var lot = await _facts.LotAsync(decision.LotId, ct);
        if (lot is null || lot.Status is not (LotStatus.Draft or LotStatus.Blocked)
            || (decision.LotStatusFrom is { } from && !string.Equals(lot.Status.ToString(), from, StringComparison.Ordinal)))
        {
            return new QuarantineViolation(QuarantineErrorCodes.DecisionNotApplicable, 409,
                $"The lot is {(lot is null ? "unknown" : lot.Status.ToString())}; only a Draft or Blocked lot is released.");
        }

        var other = await approvals.WaitingReleaseAsync(decision.LotId!, decision.Id, ct);
        return other is not null
            ? new QuarantineViolation(QuarantineDecisionRules.DecisionAlreadyPending, 409, $"Lot release {other.DecisionNumber} of this lot is already waiting for approval.")
            : null;
    }

    public async Task<(QuarantineViolation? Violation, string? SampleLocationId)> BlockedAsync(
        BlockedStock blocked, DecisionKind kind, RequestedDisposition? disposition, Guid? ownDecisionId, CancellationToken ct)
    {
        if (QuarantineDecisionRules.BlockedState(blocked.Status, blocked.ActiveDecisionId == ownDecisionId ? null : blocked.ActiveDecisionId) is { } v6)
            return (v6, null);
        decimal quantity;
        string? sampleLocation = null;
        if (kind == DecisionKind.Disposition)
        {
            if (QuarantineDecisionRules.Disposition(disposition!, blocked.Quantity, QuarantineSupport.Today()) is { } v10) return (v10, null);
            quantity = disposition!.Quantity;
            if (QuarantineDecisionRules.Serials(blocked.SerialIds, blocked.Quantity, [quantity]) is { } ser) return (ser, null);
        }
        else
        {
            if (QuarantineDecisionRules.Unblock(blocked.Reviewed) is { } v14) return (v14, null);
            quantity = blocked.Quantity;
            if (QuarantineDecisionRules.Serials(blocked.SerialIds, blocked.Quantity, [quantity]) is { } ser) return (ser, null);
        }

        var lot = await _facts.LotAsync(blocked.LotId, ct);
        if (QuarantineDecisionRules.LotHold(QuarantineFacts.IsHeld(lot), kind, disposition?.Kind) is { } v8) return (v8, null);

        if (disposition?.Kind == DispositionKind.Sample)
        {
            var settings = await _facts.SettingsAsync(ct);
            sampleLocation = settings.SampleLocations.GetValueOrDefault(blocked.WarehouseId);
            var valid = sampleLocation is not null && await _facts.IsStockableLocationOfWarehouseAsync(sampleLocation, blocked.WarehouseId, ct);
            if (QuarantineDecisionRules.SampleLocation(sampleLocation, valid, blocked.LocationId) is { } v13) return (v13, null);
        }

        var (onHand, _) = await _facts.BucketAsync(blocked.ItemId, blocked.SkuId, blocked.SkuLevel, blocked.WarehouseId, blocked.LocationId, blocked.LotId,
            StockStatus.Blocked, ct);
        return (QuarantineDecisionRules.Bucket(onHand, quantity), sampleLocation);
    }
}

/// <summary>
/// §4.6 createDecision — a DRAFT decision (pack §4: insert-only from here on); nothing moves, nothing is locked yet (the
/// target is locked by the submit). Same key + same body → the draft (200); another body → 400.
/// </summary>
public sealed class CreateDecisionHandler : IRequestHandler<CreateDecisionCommand, Response<DecisionDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineDecisionChecks _checks;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineSupport _support;

    public CreateDecisionHandler(
        IQuarantineRepository repository, IQuarantineDecisionRepository decisions, QuarantineDecisionChecks checks, QuarantineFacts facts,
        QuarantineSupport support)
    {
        _repository = repository;
        _decisions = decisions;
        _checks = checks;
        _facts = facts;
        _support = support;
    }

    public async Task<Response<DecisionDto>> Handle(CreateDecisionCommand request, CancellationToken ct)
    {
        var fingerprint = Fingerprint(request);
        var replay = await ReplayAsync(request.IdempotencyKey, fingerprint, ct);
        if (replay is not null)
        {
            return replay;
        }

        if (request.Kind == DecisionKind.LotRelease)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineDecisionRules.DecisionInvalid, "LotRelease decisions are created by the lot gate only (KQ-I).", 400);
        }

        if (QuarantineDecisionRules.Reason(request.Reason) is { } v3)
        {
            return QuarantineSupport.Fail<DecisionDto>(v3);
        }

        var now = QuarantineSupport.Now();
        var decision = new QuarantineDecision
        {
            Id = Guid.NewGuid(),
            TenantId = _support.TenantId,
            LegalEntityId = _support.LegalEntityId,
            CreatedAt = now,
            Kind = request.Kind,
            Reason = request.Reason!.Trim(),
            DeciderUserId = _support.UserId,
            DecidedAtUtc = now,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint
        };

        if (request.Kind == DecisionKind.HoldDecision)
        {
            if (request.Disposition is not null)
            {
                return QuarantineSupport.Fail<DecisionDto>(QuarantineDecisionRules.DecisionInvalid, "A hold decision carries lines, not a disposition.", 400);
            }

            var hold = await _repository.FindHoldAsync(request.TargetId, ct);
            if (hold is null)
            {
                return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.HoldNotFound, "Hold not found.", 404);
            }

            if (await _checks.HoldAsync(hold, request.Lines, request.BlockReasonCode, request.CoaChecked, request.NewRetestDate, null, ct) is { } violation)
            {
                return QuarantineSupport.Fail<DecisionDto>(violation);
            }

            var lot = await _facts.LotAsync(hold.LotId, ct);
            decision.HoldId = hold.Id;
            // CT-Q5: a release of a hold whose lot is Draft releases the lot in the same approval (no second approval for one batch)
            decision.LotStatusTarget = lot?.Status == LotStatus.Draft && request.Lines.Any(l => l.Decision == LineDecision.Release)
                ? nameof(LotStatus.Released)
                : null;
            Snapshot(decision, hold.ItemId, hold.SkuId, hold.SkuLevel, hold.LotId, hold.WarehouseId, hold.LocationId, hold.StockStatus, hold.BaseUomId, hold.Quantity);
            var ordered = request.Lines.OrderBy(l => l.LineNo).ToList();
            var serialParts = QuarantineStockEffects.SplitSerials(hold.SerialIds, ordered.Select(l => l.Quantity).ToList());
            decision.Lines = ordered
                .Select((l, i) => new DecisionLine
                {
                    LineNo = l.LineNo, Decision = l.Decision, Quantity = l.Quantity, ToStockStatus = QuarantineStockEffects.LineTarget[l.Decision],
                    SerialIds = serialParts[i]
                })
                .ToList();
            decision.BlockReasonCode = request.Lines.Any(l => l.Decision is LineDecision.Reject or LineDecision.Block) ? request.BlockReasonCode : null;
            decision.CoaChecked = request.CoaChecked;
            decision.CoaReference = lot?.CoaReference;
            decision.NewRetestDate = request.NewRetestDate is { } d ? QuarantineSupport.Midnight(d) : null;
        }
        else
        {
            if (request.Lines.Count > 0)
            {
                return QuarantineSupport.Fail<DecisionDto>(QuarantineDecisionRules.DecisionInvalid, $"A {request.Kind} decision carries no lines.", 400);
            }

            if (request.Kind == DecisionKind.Disposition && request.Disposition is null)
            {
                return QuarantineSupport.Fail<DecisionDto>(QuarantineDecisionRules.DecisionInvalid, "A disposition decision needs disposition.", 400);
            }

            if (request.Kind == DecisionKind.Unblock && request.Disposition is not null)
            {
                return QuarantineSupport.Fail<DecisionDto>(QuarantineDecisionRules.DecisionInvalid, "An unblock decision carries only its reason.", 400);
            }

            var blocked = await _repository.FindBlockedAsync(request.TargetId, ct);
            if (blocked is null)
            {
                return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.BlockedNotFound, "Blocked-stock record not found.", 404);
            }

            var (violation, sampleLocation) = await _checks.BlockedAsync(blocked, request.Kind, request.Disposition, null, ct);
            if (violation is not null)
            {
                return QuarantineSupport.Fail<DecisionDto>(violation);
            }

            decision.BlockedStockId = blocked.Id;
            Snapshot(decision, blocked.ItemId, blocked.SkuId, blocked.SkuLevel, blocked.LotId, blocked.WarehouseId, blocked.LocationId, StockStatus.Blocked,
                blocked.BaseUomId, blocked.Quantity);
            if (request.Disposition is { } disposition)
            {
                decision.Disposition = new DecisionDisposition
                {
                    Kind = disposition.Kind,
                    Quantity = disposition.Quantity,
                    SupplierId = disposition.SupplierId,
                    ReturnDocumentNo = disposition.ReturnDocumentNo?.Trim(),
                    DestructionMethod = disposition.DestructionMethod?.Trim(),
                    DestructionDate = disposition.DestructionDate is { } dd ? QuarantineSupport.Midnight(dd) : null,
                    SampleLocationId = sampleLocation,
                    SerialIds = QuarantineStockEffects.SplitSerials(blocked.SerialIds, [disposition.Quantity])[0]
                };
            }
            else
            {
                decision.SerialIds = blocked.SerialIds is { Count: > 0 } serials ? [.. serials] : null; // Unblock moves every remaining serial
            }
        }

        try
        {
            await _repository.InTransactionAsync<bool>(async (session, c) =>
            {
                decision.DecisionNumber = await session.NextNumberAsync(QuarantineContract.DecisionPrefix, now.UtcDateTime.Year, c);
                await session.InsertDecisionAsync(decision, c);
                if (decision.BlockReasonCode is { } code)
                {
                    await session.MarkBlockReasonUsedAsync(code, now, c);
                }

                return true;
            }, ct);
        }
        catch (QuarantineDuplicateException)
        {
            return await ReplayAsync(request.IdempotencyKey, fingerprint, ct)
                   ?? throw new InvalidOperationException("Decision key reported taken but no decision carries it.");
        }

        return Response<DecisionDto>.Success(QuarantineMapping.Decision(decision, []), 201);
    }

    private async Task<Response<DecisionDto>?> ReplayAsync(string key, string fingerprint, CancellationToken ct)
    {
        var existing = await _decisions.FindByIdempotencyKeyAsync(_support.UserId, key, ct);
        if (existing is null)
        {
            return null;
        }

        return existing.RequestFingerprint == fingerprint
            ? Response<DecisionDto>.Success(QuarantineMapping.Decision(existing, await _decisions.EventsAsync([existing.Id], ct)), 200)
            : QuarantineSupport.KeyReused<DecisionDto>();
    }

    private static void Snapshot(
        QuarantineDecision decision, Guid itemId, Guid skuId, SkuLevel level, string? lotId, string warehouseId, string locationId, StockStatus status,
        string baseUomId, decimal quantity)
    {
        decision.ItemId = itemId;
        decision.SkuId = skuId;
        decision.SkuLevel = level;
        decision.LotId = lotId;
        decision.WarehouseId = warehouseId;
        decision.LocationId = locationId;
        decision.FromStockStatus = status;
        decision.BaseUomId = baseUomId;
        decision.AvailableQuantityAtDecision = quantity;
    }

    private static string Fingerprint(CreateDecisionCommand r) => QuarantineSupport.Fingerprint(
        [("kind", r.Kind.ToString()), ("target", r.TargetId.ToString("D")), ("reason", r.Reason?.Trim()), ("block", r.BlockReasonCode),
         ("coa", r.CoaChecked ? "1" : "0"), ("retest", r.NewRetestDate?.ToString("yyyy-MM-dd")),
         ("disp", r.Disposition is { } d ? $"{d.Kind}|{QuarantineSupport.D(d.Quantity)}|{d.SupplierId}|{d.ReturnDocumentNo}|{d.DestructionMethod}|{d.DestructionDate:yyyy-MM-dd}" : null),
         .. r.Lines.OrderBy(l => l.LineNo).Select(l => ($"l{l.LineNo}", $"{l.Decision}|{QuarantineSupport.D(l.Quantity)}"))]);
}

/// <summary>
/// §4.7 submitDecision — the decider sends the draft for approval. The target is locked first (one active decision per
/// record, V6); the approval port then starts the approval: started → <c>PendingApproval</c> (hold → DecisionPending);
/// not started → <c>StartFailed</c>, the record is reopened, the port's code is answered (Q1 production default: 503
/// <c>QUARANTINE_WORKFLOW_UNAVAILABLE</c>). NOTHING moves on submit (pack §13 #8).
/// </summary>
public sealed class SubmitDecisionHandler : IRequestHandler<SubmitDecisionCommand, Response<DecisionDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineDecisionChecks _checks;
    private readonly IQuarantineApprovalPort _approval;
    private readonly QuarantineSupport _support;
    private readonly QuarantineDeciderSignature _signature;
    private readonly QuarantineLotReleaseApprovals _lotReleases;

    public SubmitDecisionHandler(
        IQuarantineRepository repository, IQuarantineDecisionRepository decisions, QuarantineDecisionChecks checks, IQuarantineApprovalPort approval,
        QuarantineSupport support, QuarantineDeciderSignature signature, QuarantineLotReleaseApprovals lotReleases)
    {
        _repository = repository;
        _decisions = decisions;
        _checks = checks;
        _approval = approval;
        _support = support;
        _signature = signature;
        _lotReleases = lotReleases;
    }

    public async Task<Response<DecisionDto>> Handle(SubmitDecisionCommand request, CancellationToken ct)
    {
        var decision = await _decisions.FindAsync(request.DecisionId, ct);
        if (decision is null)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.DecisionNotFound, "Decision not found.", 404);
        }

        if (decision.DeciderUserId != _support.UserId)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.NotDecider, "Only the decider submits a decision.", 403);
        }

        var events = await _decisions.EventsAsync([decision.Id], ct);
        var state = QuarantineFacts.StateOf(events);
        if (state == DecisionState.PendingApproval)
        {
            return Response<DecisionDto>.Success(QuarantineMapping.Decision(decision, events), 202); // replay
        }

        if (state is not (DecisionState.Draft or DecisionState.StartFailed))
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.DecisionNotDraft, $"A {state} decision cannot be submitted.", 409);
        }

        var hold = decision.HoldId is { } holdId ? await _repository.FindHoldAsync(holdId, ct) : null;
        var blocked = decision.BlockedStockId is { } blockedId ? await _repository.FindBlockedAsync(blockedId, ct) : null;
        var violation = decision.Kind == DecisionKind.LotRelease
            ? await _checks.LotReleaseAsync(decision, _lotReleases, ct)
            : hold is not null
            ? await _checks.HoldAsync(hold, decision.Lines.Select(l => new RequestedLine(l.LineNo, l.Decision, l.Quantity)).ToList(), decision.BlockReasonCode,
                decision.CoaChecked, decision.NewRetestDate is { } r ? DateOnly.FromDateTime(r.UtcDateTime) : null, decision.Id, ct)
            : (await _checks.BlockedAsync(blocked!, decision.Kind, decision.Disposition is { } d
                ? new RequestedDisposition(d.Kind, d.Quantity, d.SupplierId, d.ReturnDocumentNo, d.DestructionMethod,
                    d.DestructionDate is { } dd ? DateOnly.FromDateTime(dd.UtcDateTime) : null)
                : null, decision.Id, ct)).Violation;
        if (violation is not null)
        {
            return QuarantineSupport.Fail<DecisionDto>(violation);
        }

        // Q3 (pack §8 decider steps 3–5, V15): the MOD-0022 policy of the decision's action; a signature, when sent or
        // required, must bind the caller, this decision, the policy meaning, and the current qd-v1 subject.
        var signature = await _signature.CheckAsync(decision, request.SignatureId, ct);
        if (signature.Refusal is { } refusal)
        {
            return refusal;
        }

        var now = QuarantineSupport.Now();
        var locked = await _repository.InTransactionAsync(async (session, c) =>
        {
            if (!await LockAsync(session, hold, blocked, decision.Id, now, c)) return false;
            var submitted = Stamp(QuarantineSupport.Event(decision.Id, DecisionEventKind.Submitted, now, _support.UserId), decision);
            submitted.DeciderSignatureId = signature.SignatureId;
            submitted.SignatureRequired = signature.Required;
            await session.InsertDecisionEventAsync(submitted, c);
            if (QuarantineApplyEngine.HasRecord(decision))
            {
                await session.InsertHistoryAsync([Stamp(_support.History(QuarantineApplyEngine.SubjectOf(decision), QuarantineApplyEngine.TargetId(decision),
                    HistoryAction.DecisionSubmitted, now, decision.Reason, decision.Id), decision)], c);
            }

            return true;
        }, ct);
        if (!locked)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineDecisionRules.DecisionAlreadyPending, "The record changed or another decision is waiting for approval.", 409);
        }

        var start = await _approval.StartAsync(decision, ct);
        var after = QuarantineSupport.Now();
        if (start.Started)
        {
            var started = Stamp(QuarantineSupport.Event(decision.Id, DecisionEventKind.WorkflowStarted, after, _support.UserId), decision);
            started.WorkflowInstanceId = start.InstanceId;
            await _repository.InTransactionAsync<bool>(async (session, c) =>
            {
                await session.InsertDecisionEventAsync(started, c);
                if (hold is not null)
                {
                    var fresh = await _repository.FindHoldAsync(hold.Id, c) ?? hold;
                    var expected = fresh.Version;
                    fresh.Status = HoldStatus.DecisionPending;
                    fresh.UpdatedAt = after;
                    fresh.Version = expected + 1;
                    if (!await session.ReplaceHoldAsync(fresh, expected, c)) throw new QuarantineRetryException();
                }

                return true;
            }, ct);
            return Response<DecisionDto>.Success(QuarantineMapping.Decision(decision, await _decisions.EventsAsync([decision.Id], ct)), 202);
        }

        var failed = Stamp(QuarantineSupport.Event(decision.Id, DecisionEventKind.StartFailed, after, _support.UserId, start.ErrorCode, start.Message), decision);
        var freshHold = hold is not null ? await _repository.FindHoldAsync(hold.Id, ct) : null;
        var freshBlocked = blocked is not null ? await _repository.FindBlockedAsync(blocked.Id, ct) : null;
        await _repository.InTransactionAsync<bool>(async (session, c) =>
        {
            await session.InsertDecisionEventAsync(failed, c);
            await QuarantineReopen.ReopenAsync(session, freshHold, freshBlocked, decision.Id, after, c);
            return true;
        }, ct);
        return QuarantineSupport.Fail<DecisionDto>(start.ErrorCode!, start.Message ?? "The approval could not be started.", start.Status);
    }

    /// <summary>One active decision per record (V6): the lock is the record's <c>ActiveDecisionId</c> under its version.</summary>
    private static async Task<bool> LockAsync(
        IQuarantineWriteSession session, QuarantineHold? hold, BlockedStock? blocked, Guid decisionId, DateTimeOffset now, CancellationToken ct)
    {
        if (hold is null && blocked is null)
        {
            return true; // KQ-I LotRelease: no 0175 record to lock (the lot's waiting release was checked)
        }

        if (hold is not null)
        {
            if (hold.ActiveDecisionId is not null && hold.ActiveDecisionId != decisionId) return false;
            var expected = hold.Version;
            hold.ActiveDecisionId = decisionId;
            hold.UpdatedAt = now;
            hold.Version = expected + 1;
            return await session.ReplaceHoldAsync(hold, expected, ct);
        }

        if (blocked!.ActiveDecisionId is not null && blocked.ActiveDecisionId != decisionId) return false;
        var expectedBlocked = blocked.Version;
        blocked.ActiveDecisionId = decisionId;
        blocked.UpdatedAt = now;
        blocked.Version = expectedBlocked + 1;
        return await session.ReplaceBlockedAsync(blocked, expectedBlocked, ct);
    }

    private T Stamp<T>(T entity, QuarantineDecision decision) where T : Diten.InventoryService.Domain.Entities.EntityBase
    {
        entity.TenantId = _support.TenantId;
        entity.LegalEntityId = decision.LegalEntityId;
        return entity;
    }
}

/// <summary>§4.11 reapplyDecision — "Yeniden uygula": only an ApplyFailed decision; same ledger keys (CT-Q9).</summary>
public sealed class ReapplyDecisionHandler : IRequestHandler<ReapplyDecisionCommand, Response<DecisionDto>>
{
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineApplyEngine _engine;

    public ReapplyDecisionHandler(IQuarantineDecisionRepository decisions, QuarantineApplyEngine engine)
    {
        _decisions = decisions;
        _engine = engine;
    }

    public async Task<Response<DecisionDto>> Handle(ReapplyDecisionCommand request, CancellationToken ct)
    {
        var decision = await _decisions.FindAsync(request.DecisionId, ct);
        if (decision is null)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.DecisionNotFound, "Decision not found.", 404);
        }

        var events = await _decisions.EventsAsync([decision.Id], ct);
        var state = QuarantineFacts.StateOf(events);
        if (state != DecisionState.ApplyFailed)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.DecisionNotApplicable, $"Only an ApplyFailed decision is re-applied (this one is {state}).", 409);
        }

        var approver = events.Last(e => e.Kind == DecisionEventKind.Approved).ApproverUserId!.Value;
        var result = await _engine.ApplyAsync(decision, approver, ct);
        if (result.State != DecisionState.Applied)
        {
            return QuarantineSupport.Fail<DecisionDto>(result.ErrorCode ?? QuarantineErrorCodes.DecisionNotApplicable, result.Message ?? "The decision could not be applied.", 422);
        }

        return Response<DecisionDto>.Success(QuarantineMapping.Decision(decision, await _decisions.EventsAsync([decision.Id], ct)));
    }
}

/// <summary>The Q1 test approval port's outcome (not QUARANTINE-BUNDLE; see <see cref="RecordTestOutcomeCommand"/>).</summary>
public sealed class RecordTestOutcomeHandler : IRequestHandler<RecordTestOutcomeCommand, Response<DecisionDto>>
{
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineOutcomeApplier _applier;

    public RecordTestOutcomeHandler(IQuarantineDecisionRepository decisions, QuarantineOutcomeApplier applier)
    {
        _decisions = decisions;
        _applier = applier;
    }

    public async Task<Response<DecisionDto>> Handle(RecordTestOutcomeCommand request, CancellationToken ct)
    {
        var decision = await _decisions.FindAsync(request.DecisionId, ct);
        if (decision is null)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.DecisionNotFound, "Decision not found.", 404);
        }

        var instance = (await _decisions.EventsAsync([decision.Id], ct)).LastOrDefault(e => e.Kind == DecisionEventKind.WorkflowStarted)?.WorkflowInstanceId;
        var outcome = await _applier.ApplyOutcomeAsync(decision.Id, request.Outcome, request.ApproverUserId, instance, ct);
        if (!outcome.IsSuccessful)
        {
            return Response<DecisionDto>.Fail(outcome.ErrorCode!, outcome.Errors[0], outcome.StatusCode);
        }

        return Response<DecisionDto>.Success(QuarantineMapping.Decision(decision, await _decisions.EventsAsync([decision.Id], ct)));
    }
}
