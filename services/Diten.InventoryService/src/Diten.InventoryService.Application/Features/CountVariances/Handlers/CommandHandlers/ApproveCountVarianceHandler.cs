using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountVariances.Commands;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Handlers.CommandHandlers;

/// <summary>
/// <c>approveCountVariance</c> (pack §8.5, §8.6, §8.9; AC-22, AC-23, AC-24, AC-48, AC-52 (b) (c)). Order: line (404) → key replay → If-Match (the line's
/// version, 409) → IN transaction 1: the count Submitted (409), the line AwaitingApproval (409), four eyes — the line's counter and the count's
/// submitter may not approve (403 <c>COUNT_SELF_APPROVAL_FORBIDDEN</c>, no setting turns it off), a drift line never (409 <c>COUNT_SNAPSHOT_DRIFT</c>),
/// a serial-controlled line never (422 <c>COUNT_SERIAL_NOT_POSTABLE</c>, Q-10 = A) → Approved + the first correction (Pending, its dates and its
/// <c>FirstAttemptAt</c>), the rows <c>Approved</c> + <c>AdjustmentPending</c>. A flagged line of variance 0 has nothing to post and is final at once.
/// Then the ONE posting and transaction 2 (Posted / Refused / IntegrityError); an unknown outcome leaves it Pending (503).
/// </summary>
public sealed class ApproveCountVarianceHandler : CountVarianceLineHandler, IRequestHandler<ApproveCountVarianceCommand, Response<CycleCountLineDto>>
{
    private readonly CycleCountPostingEngine _engine;

    public ApproveCountVarianceHandler(ICycleCountRepository repository, CycleCountSupport support, CycleCountLineWriter writer, CycleCountPostingEngine engine)
        : base(repository, support, writer) => _engine = engine;

    public async Task<Response<CycleCountLineDto>> Handle(ApproveCountVarianceCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpApproveCountVariance;
        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("lineId", request.LineId.ToString("D")));
        if (await LoadAsync(request.LineId, ct) is not { } loaded) return LineNotFound();
        var (line, count) = loaded;
        if (await ReplayAsync(count, line.Id, op, request.IdempotencyKey, fingerprint, request.ReadsVariances, ct) is { } replay) return replay;
        if (line.Version != request.ExpectedVersion) return CycleCountSupport.Conflict<CycleCountLineDto>();

        var approver = Support.UserId;
        var write = await Writer.WriteAsync<CycleCountLineDto>(count.Id, line.Id, (c, l) =>
        {
            if (!CycleCountRules.TakesDecisions(c)) return Refuse(CycleCountSupport.InvalidState<CycleCountLineDto>(c.Status));
            if (l.Version != request.ExpectedVersion) return Refuse(CycleCountSupport.Conflict<CycleCountLineDto>());
            if (!CycleCountDecisionRules.CanApprove(l)) return WrongLineState(l);
            if (CycleCountDecisionRules.IsSelfApproval(c, l, approver))
                return Refuse(CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.SelfApprovalForbidden,
                    "The user who counted the line or submitted the count may not approve its variance.", 403));
            if (l.SnapshotDrift)
                return Refuse(CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.SnapshotDrift,
                    "The line's balance equation failed at submit; recount or reject it.", 409));
            if (l.SerialControlled)
                return Refuse(CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.SerialNotPostable,
                    "A serial-controlled line's variance is not posted in this version; reject or recount it.", 422));
            return Approve(l, Support.Now(), approver);
        }, op, request.IdempotencyKey, fingerprint, ct);
        if (write.Kind != CycleCountLineWriteKind.Written)
            return await NotWrittenAsync(write, count, line.Id, op, request.IdempotencyKey, fingerprint, request.ReadsVariances, ct);

        var approved = write.Line!;
        if (approved.Adjustment is null) return Answer(write.Count!, approved, request.ReadsVariances);

        var result = await _engine.PostAsync(write.Count!, approved, ct);
        var written = await _engine.WriteResultAsync<CycleCountLineDto>(count.Id, line.Id, result, ct);
        var final = written.Line ?? await Repository.FindLineByIdAsync(line.Id, ct) ?? approved;
        return Answer(written.Count ?? write.Count!, final, request.ReadsVariances);
    }

    /// <summary>The approved line (a user's decision) and its rows — the correction is Pending until the ledger answers.</summary>
    private static CycleCountLineStep<CycleCountLineDto> Approve(CycleCountLine line, DateTimeOffset now, Guid approver)
    {
        var changed = line.Copy();
        changed.Status = CycleCountLineStatus.Approved;
        changed.Decision = new CycleCountDecision
            { Kind = CycleCountDecisionKind.Approved, Source = CycleCountDecisionSource.User, By = approver, At = now };
        changed.Adjustment = CycleCountDecisionRules.FirstAdjustment(line, now);
        return CycleCountLineStep<CycleCountLineDto>.Change(changed, ApprovalEvents(changed).ToArray());
    }

    /// <summary>
    /// The rows of an approval (a user's or the tolerance's — pack §21 "fark, karar, posting sonucu"): <c>Approved</c> and, when there is a correction,
    /// <c>AdjustmentPending</c>. The variance / correction quantities are blind-projected on read (<see cref="CycleCountReads.ExpectedDataKeys"/>).
    /// </summary>
    public static IEnumerable<CycleCountLineEvent> ApprovalEvents(CycleCountLine approved)
    {
        yield return new CycleCountLineEvent(CycleCountEventKind.Approved, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["source"] = approved.Decision!.Source.ToString(), ["varianceQuantity"] = approved.VarianceQuantity is { } v ? InventoryQuantity.ToWire(v) : null,
            ["movementDuringCount"] = approved.MovementDuringCount ? "true" : "false"
        });
        if (approved.Adjustment is not { } a) yield break;
        yield return new CycleCountLineEvent(CycleCountEventKind.AdjustmentPending, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["movementType"] = WireNames.ToWire(a.MovementType), ["adjustmentQuantity"] = InventoryQuantity.ToWire(a.Quantity),
            ["idempotencyKey"] = a.IdempotencyKey, ["transactionDate"] = CycleCountSupport.Wire(a.TransactionDate),
            ["postingDate"] = CycleCountSupport.Wire(a.PostingDate), ["attempts"] = CycleCountSupport.Int(a.Attempts)
        });
    }
}
