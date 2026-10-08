using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Entities;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.CommandHandlers;

/// <summary>
/// MOD-0175 Q4 (KQ-I): where a gated lot status change waits. <see cref="ChangeLotStatusHandler"/> asks it when the gate answered
/// <see cref="Ports.LotStatusGateDecision.PendingApproval"/>: it opens (or finds the open) approval request of the lot and returns
/// its id — the contract's <c>pendingRequestId</c> (QUARANTINE: the decision id, kind LotRelease, state Draft).
/// </summary>
public interface ILotStatusChangeApprovals
{
    Task<string> OpenAsync(Lot lot, LotStatus target, string reason, string idempotencyKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// The lot change an APPROVED MOD-0175 decision carries: the target status (KQ-I lot release, CT-Q5 release on a Draft lot) and / or
/// the new retest date (KQ-H retest release). <see cref="AllowedFrom"/> = the lot statuses the approval covers.
/// </summary>
public sealed record ApprovedLotChange(
    Guid DecisionId,
    string DecisionNumber,
    Guid LotId,
    LotStatus? Target,
    IReadOnlySet<LotStatus> AllowedFrom,
    DateTimeOffset? NewRetestDate,
    string Reason,
    Guid ApproverUserId);

/// <summary>Answers the approved lot change of a decision — null when the decision does not exist here or is not approved.</summary>
public interface IApprovedLotChanges
{
    Task<ApprovedLotChange?> FindApprovedAsync(Guid decisionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The internal <c>ApplyApprovedLotStatusChange</c> command (MOD-0175 pack §5 Q4 / §8 KQ-I step 4) — the ONLY path that changes a lot
/// status past the gate, and it needs the id of an APPROVED decision of THIS lot. No HTTP route exists; the MOD-0175 apply engine
/// sends it in-process.
/// </summary>
public sealed record ApplyApprovedLotStatusChangeCommand(Guid DecisionId, Guid LotId) : IRequest<Response<LotRefDto>>;

/// <summary>
/// Applies an approved lot change in ONE lot transaction: the new retest date (one <c>Updated</c> row, field <c>retestDate</c>) and the
/// status (one <c>StatusChanged</c> row carrying the key <c>mod-0175:{decisionId}</c>, so a repeat replays) — both with the
/// approver as actor and the reason "MOD-0175 approval {QKR}: …" (TRACE <c>source</c> is a closed enum, so the 0175 origin is in the
/// reason; source stays <c>api</c>). The lot's AVAILABLE stock follows the status through <c>ApplyStatusToAvailabilityAsync</c>
/// (CT-D1 / CT-Q16: the stored retest hold is unchanged here; the retest sweeper clears it in its own transaction).
/// Refused: no approved decision (409 <c>LOT_APPROVAL_REQUIRED</c>), another lot's decision (409), a lot status the approval does
/// not cover (422 <c>LOT_STATUS_TRANSITION_INVALID</c>).
/// </summary>
public sealed class ApplyApprovedLotStatusChangeHandler : IRequestHandler<ApplyApprovedLotStatusChangeCommand, Response<LotRefDto>>
{
    public const string ApprovalRequired = "LOT_APPROVAL_REQUIRED";

    private readonly ILotRepository _lots;
    private readonly IApprovedLotChanges _approvals;
    private readonly TraceWriteSupport _support;

    public ApplyApprovedLotStatusChangeHandler(ILotRepository lots, IApprovedLotChanges approvals, TraceWriteSupport support)
    {
        _lots = lots;
        _approvals = approvals;
        _support = support;
    }

    public static string Key(Guid decisionId) => $"mod-0175:{decisionId:D}";

    public async Task<Response<LotRefDto>> Handle(ApplyApprovedLotStatusChangeCommand request, CancellationToken cancellationToken)
    {
        var approved = await _approvals.FindApprovedAsync(request.DecisionId, cancellationToken);
        if (approved is null)
        {
            return TraceWriteSupport.Fail<LotRefDto>(ApprovalRequired, "A lot status change past the gate needs an approved MOD-0175 decision.", 409);
        }

        if (approved.LotId != request.LotId)
        {
            return TraceWriteSupport.Fail<LotRefDto>(ApprovalRequired, "The approved decision is not about this lot.", 409);
        }

        var now = DateTimeOffset.UtcNow;
        var today = TraceMapping.Today(now);
        var key = Key(approved.DecisionId);
        if (await _lots.FindStatusChangeByKeyAsync(request.LotId, key, cancellationToken) is not null)
        {
            return await CurrentAsync(request.LotId, today, cancellationToken);
        }

        var reason = $"MOD-0175 approval {approved.DecisionNumber}: {approved.Reason}";
        try
        {
            return await _lots.InTransactionAsync(async (tx, ct) =>
            {
                var lot = await tx.FindAsync(request.LotId, ct);
                if (lot is null) return TraceWriteSupport.UnknownLot<LotRefDto>();

                var changeStatus = approved.Target is { } target && lot.Status != target;
                if (changeStatus && (!approved.AllowedFrom.Contains(lot.Status) || !LotStatusRules.CanTransition(lot.Status, approved.Target!.Value)))
                {
                    return TraceWriteSupport.Fail<LotRefDto>(TraceErrorCodes.LotStatusTransitionInvalid,
                        $"The approval covers a {string.Join(" / ", approved.AllowedFrom)} lot; this lot is {lot.Status}.", 422);
                }

                var entries = new List<LotChangeEntry>();
                if (approved.NewRetestDate is { } retest && lot.RetestDate != retest)
                {
                    entries.Add(Row(lot.Id, LotChangeAction.Updated, "retestDate", TraceMapping.Wire(lot.RetestDate), TraceMapping.Wire(retest), reason, now, approved));
                    lot.RetestDate = retest;
                }

                var from = lot.Status;
                if (changeStatus)
                {
                    entries.Add(Row(lot.Id, LotChangeAction.StatusChanged, "status", from.ToString(), approved.Target!.Value.ToString(), reason, now, approved));
                    lot.Status = approved.Target.Value;
                }

                if (entries.Count == 0)
                {
                    return Response<LotRefDto>.Success(TraceMapping.ToRef(lot, today, withETag: true), 200); // nothing left to change
                }

                entries[^1].IdempotencyKey = key; // one keyed row per approval (unique per lot): a repeat replays
                entries[^1].RequestFingerprint = key;
                var expectedVersion = lot.Version;
                lot.UpdatedAt = now;
                lot.UpdatedBy = approved.ApproverUserId;
                if (!await tx.ReplaceAsync(lot, expectedVersion, ct)) return TraceWriteSupport.Conflict<LotRefDto>();
                await tx.AppendHistoryAsync(entries, ct);
                if (changeStatus)
                {
                    await tx.ApplyStatusToAvailabilityAsync(lot, from, ct);
                }

                return Response<LotRefDto>.Success(TraceMapping.ToRef(lot, today, withETag: true), 200);
            }, cancellationToken);
        }
        catch (LotUniqueViolationException violation) when (violation.Index == LotUniqueIndex.StatusChangeKey)
        {
            return await CurrentAsync(request.LotId, today, cancellationToken); // a concurrent apply of the same approval committed first
        }
    }

    private LotChangeEntry Row(
        Guid lotId, LotChangeAction action, string field, string? oldValue, string? newValue, string reason, DateTimeOffset now, ApprovedLotChange approved)
    {
        var entry = _support.History(lotId, action, field, oldValue, newValue, reason, now);
        entry.ActorUserId = approved.ApproverUserId;
        return entry;
    }

    private async Task<Response<LotRefDto>> CurrentAsync(Guid lotId, DateOnly today, CancellationToken ct)
    {
        var lot = await _lots.FindAsync(lotId, ct);
        return lot is null
            ? TraceWriteSupport.UnknownLot<LotRefDto>()
            : Response<LotRefDto>.Success(TraceMapping.ToRef(lot, today, withETag: true), 200);
    }
}
