using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountVariances.Commands;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Handlers.CommandHandlers;

/// <summary>
/// <c>retryCountAdjustment</c> (pack §8.6, §8.7; AC-21, AC-24, AC-25, AC-27). Line (404) → key replay → If-Match (409) → IN transaction 1: the line
/// approved with its correction Pending or refused on a reservation block (anything else 409 — an unapproved line never posts, AC-21) → Pending,
/// attempt + 1, the row <c>AdjustmentPending</c>. Then the ledger KEY first (Committed → Posted, no second movement; reversed / another payload →
/// IntegrityError) and, when nothing holds it, the SAME stored request under the same key; transaction 2 writes the result.
/// </summary>
public sealed class RetryCountAdjustmentHandler : CountVarianceLineHandler, IRequestHandler<RetryCountAdjustmentCommand, Response<CycleCountLineDto>>
{
    private readonly CycleCountPostingEngine _engine;

    public RetryCountAdjustmentHandler(ICycleCountRepository repository, CycleCountSupport support, CycleCountLineWriter writer, CycleCountPostingEngine engine)
        : base(repository, support, writer) => _engine = engine;

    public async Task<Response<CycleCountLineDto>> Handle(RetryCountAdjustmentCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpRetryCountAdjustment;
        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("lineId", request.LineId.ToString("D")));
        if (await LoadAsync(request.LineId, ct) is not { } loaded) return LineNotFound();
        var (line, count) = loaded;
        if (await ReplayAsync(count, line.Id, op, request.IdempotencyKey, fingerprint, request.ReadsVariances, ct) is { } replay) return replay;
        if (line.Version != request.ExpectedVersion) return CycleCountSupport.Conflict<CycleCountLineDto>();

        var write = await Writer.WriteAsync<CycleCountLineDto>(count.Id, line.Id, (c, l) =>
        {
            if (!CycleCountRules.TakesDecisions(c)) return Refuse(CycleCountSupport.InvalidState<CycleCountLineDto>(c.Status));
            if (l.Version != request.ExpectedVersion) return Refuse(CycleCountSupport.Conflict<CycleCountLineDto>());
            if (!CycleCountDecisionRules.CanRetry(l)) return WrongLineState(l);
            var now = Support.Now();
            var changed = l.Copy();
            var a = changed.Adjustment!;
            var previous = a.Status;
            a.Status = CycleCountAdjustmentStatus.Pending;
            a.Attempts += 1;
            a.LastAttemptAt = now;
            return CycleCountLineStep<CycleCountLineDto>.Change(changed, new CycleCountLineEvent(CycleCountEventKind.AdjustmentPending,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["previousAdjustmentStatus"] = previous.ToString(), ["previousRefusalCode"] = l.Adjustment!.RefusalCode,
                    ["attempts"] = CycleCountSupport.Int(a.Attempts), ["idempotencyKey"] = a.IdempotencyKey
                }));
        }, op, request.IdempotencyKey, fingerprint, ct);
        if (write.Kind != CycleCountLineWriteKind.Written)
            return await NotWrittenAsync(write, count, line.Id, op, request.IdempotencyKey, fingerprint, request.ReadsVariances, ct);

        var result = await _engine.RecoverAsync(write.Count!, write.Line!, ct);
        var written = await _engine.WriteResultAsync<CycleCountLineDto>(count.Id, line.Id, result, ct);
        var final = written.Line ?? await Repository.FindLineByIdAsync(line.Id, ct) ?? write.Line!;
        return Answer(written.Count ?? write.Count!, final, request.ReadsVariances);
    }
}
