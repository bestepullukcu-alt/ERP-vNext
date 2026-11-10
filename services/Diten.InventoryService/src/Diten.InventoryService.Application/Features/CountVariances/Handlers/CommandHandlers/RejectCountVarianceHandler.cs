using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountVariances.Commands;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Handlers.CommandHandlers;

/// <summary>
/// <c>rejectCountVariance</c> (pack §8.5; AC-31, AC-48, AC-52 (c)). The reason is 1..500 characters (400 <c>COUNT_REASON_REQUIRED</c>) → line (404) → key
/// replay → If-Match (409) → IN the transaction: the count Submitted, the line AwaitingApproval or (Approved + Refused) → <c>Rejected</c> with the
/// decision; nothing is posted. A drift or serial line takes reject. The last final line closes the count (AC-33).
/// </summary>
public sealed class RejectCountVarianceHandler : CountVarianceLineHandler, IRequestHandler<RejectCountVarianceCommand, Response<CycleCountLineDto>>
{
    public RejectCountVarianceHandler(ICycleCountRepository repository, CycleCountSupport support, CycleCountLineWriter writer) : base(repository, support, writer)
    {
    }

    public async Task<Response<CycleCountLineDto>> Handle(RejectCountVarianceCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpRejectCountVariance;
        if (CycleCountRules.Reason(request.Reason) is not { } reason) return CycleCountSupport.ReasonRequired<CycleCountLineDto>();
        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("lineId", request.LineId.ToString("D")), ("reason", reason));
        if (await LoadAsync(request.LineId, ct) is not { } loaded) return LineNotFound();
        var (line, count) = loaded;
        if (await ReplayAsync(count, line.Id, op, request.IdempotencyKey, fingerprint, request.ReadsVariances, ct) is { } replay) return replay;
        if (line.Version != request.ExpectedVersion) return CycleCountSupport.Conflict<CycleCountLineDto>();

        var by = Support.UserId;
        var write = await Writer.WriteAsync<CycleCountLineDto>(count.Id, line.Id, (c, l) =>
        {
            if (!CycleCountRules.TakesDecisions(c)) return Refuse(CycleCountSupport.InvalidState<CycleCountLineDto>(c.Status));
            if (l.Version != request.ExpectedVersion) return Refuse(CycleCountSupport.Conflict<CycleCountLineDto>());
            if (!CycleCountDecisionRules.CanReject(l)) return WrongLineState(l);
            var now = Support.Now();
            var changed = l.Copy();
            changed.Status = CycleCountLineStatus.Rejected;
            changed.Decision = new CycleCountDecision { Kind = CycleCountDecisionKind.Rejected, Source = CycleCountDecisionSource.User, By = by, At = now, Reason = reason };
            return CycleCountLineStep<CycleCountLineDto>.Change(changed, new CycleCountLineEvent(CycleCountEventKind.Rejected,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["reason"] = reason, ["previousStatus"] = l.Status.ToString(), ["adjustmentStatus"] = l.Adjustment?.Status.ToString()
                }));
        }, op, request.IdempotencyKey, fingerprint, ct);

        return write.Kind == CycleCountLineWriteKind.Written
            ? Response<CycleCountLineDto>.Success(CycleCountReads.Line(write.Line!, CycleCountReads.ShowsExpected(write.Count!, request.ReadsVariances)))
            : await NotWrittenAsync(write, count, line.Id, op, request.IdempotencyKey, fingerprint, request.ReadsVariances, ct);
    }
}
