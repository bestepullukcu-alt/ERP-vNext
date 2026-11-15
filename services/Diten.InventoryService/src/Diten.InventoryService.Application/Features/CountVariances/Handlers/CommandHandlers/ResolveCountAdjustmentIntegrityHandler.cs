using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountVariances.Commands;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Handlers.CommandHandlers;

/// <summary>
/// <c>resolveCountAdjustmentIntegrity</c> (pack §8.3 F-12; AC-26, AC-47 (e)). Shape: <c>decision</c> = <c>AcceptHolder</c> | <c>Reject</c> (400), a reason
/// of 1..500 (400 <c>COUNT_REASON_REQUIRED</c>) → line (404) → key replay → If-Match (409) → the line's correction is an IntegrityError (409).
/// <list type="bullet">
/// <item><c>AcceptHolder</c>: the movement holding the line's key IS this line's correction — same type, quantity, bin, lot, status and
/// <c>SourceLineId</c> — and is not reversed → the correction becomes Posted with the holder's transaction (no new movement); otherwise 409
/// <c>COUNT_ADJUSTMENT_INTEGRITY</c> and nothing changes.</item>
/// <item><c>Reject</c>: the line is Rejected; the correction stays IntegrityError (history); nothing is posted.</item>
/// </list>
/// Either way the line is final and the last one closes the count.
/// </summary>
public sealed class ResolveCountAdjustmentIntegrityHandler : CountVarianceLineHandler,
    IRequestHandler<ResolveCountAdjustmentIntegrityCommand, Response<CycleCountLineDto>>
{
    private readonly CycleCountPostingEngine _engine;

    public ResolveCountAdjustmentIntegrityHandler(ICycleCountRepository repository, CycleCountSupport support, CycleCountLineWriter writer,
        CycleCountPostingEngine engine) : base(repository, support, writer) => _engine = engine;

    public async Task<Response<CycleCountLineDto>> Handle(ResolveCountAdjustmentIntegrityCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpResolveCountAdjustmentIntegrity;
        if (!Enum.TryParse<CycleCountIntegrityDecision>(request.Decision, ignoreCase: false, out var decision) || !Enum.IsDefined(decision)
            || request.Decision!.Any(char.IsDigit))
            return CycleCountSupport.Invalid<CycleCountLineDto>("decision must be AcceptHolder or Reject.");
        if (CycleCountRules.Reason(request.Reason) is not { } reason) return CycleCountSupport.ReasonRequired<CycleCountLineDto>();
        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("lineId", request.LineId.ToString("D")), ("decision", decision.ToString()), ("reason", reason));
        if (await LoadAsync(request.LineId, ct) is not { } loaded) return LineNotFound();
        var (line, count) = loaded;
        if (await ReplayAsync(count, line.Id, op, request.IdempotencyKey, fingerprint, request.ReadsVariances, ct) is { } replay) return replay;
        if (line.Version != request.ExpectedVersion) return CycleCountSupport.Conflict<CycleCountLineDto>();
        if (!CycleCountRules.TakesDecisions(count)) return CycleCountSupport.InvalidState<CycleCountLineDto>(count.Status);
        if (!CycleCountDecisionRules.CanResolve(line)) return WrongLineState(line).Refusal!;

        Guid? holderId = null;
        string? holderNumber = null;
        if (decision == CycleCountIntegrityDecision.AcceptHolder)
        {
            var found = await _engine.RawLookupAsync(count, line, ct);
            var holder = found.Holder;
            var movement = holder is null ? null : await Repository.LedgerMovementAsync(holder.TransactionId, ct);
            if (holder is null || movement is null || !IsThisCorrection(line, holder, movement))
            {
                return CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.AdjustmentIntegrity,
                    "The movement holding this line's key is not this line's correction (or it was reversed); it cannot be accepted.", 409,
                    new Dictionary<string, object?> { ["lineId"] = line.Id.ToString("D"), ["holderTransactionId"] = holder?.TransactionId.ToString("D") });
            }

            holderId = holder.TransactionId;
            holderNumber = holder.TransactionNumber;
        }

        var by = Support.UserId;
        var write = await Writer.WriteAsync<CycleCountLineDto>(count.Id, line.Id, (c, l) =>
        {
            if (!CycleCountRules.TakesDecisions(c)) return Refuse(CycleCountSupport.InvalidState<CycleCountLineDto>(c.Status));
            if (l.Version != request.ExpectedVersion) return Refuse(CycleCountSupport.Conflict<CycleCountLineDto>());
            if (!CycleCountDecisionRules.CanResolve(l)) return WrongLineState(l);
            var now = Support.Now();
            var changed = l.Copy();
            changed.IntegrityResolution = new CycleCountIntegrityResolution { Kind = decision, By = by, At = now, Reason = reason, HolderTransactionId = holderId };
            if (decision == CycleCountIntegrityDecision.AcceptHolder)
            {
                changed.Adjustment!.Status = CycleCountAdjustmentStatus.Posted;
                changed.Adjustment.TransactionId = holderId;
                changed.Adjustment.TransactionNumber = holderNumber;
            }
            else
            {
                changed.Status = CycleCountLineStatus.Rejected;
            }

            return CycleCountLineStep<CycleCountLineDto>.Change(changed, new CycleCountLineEvent(CycleCountEventKind.IntegrityResolved,
                new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["decision"] = decision.ToString(), ["reason"] = reason, ["holderTransactionId"] = holderId?.ToString("D"),
                    ["holderTransactionNumber"] = holderNumber, ["lineStatus"] = changed.Status.ToString(),
                    ["adjustmentStatus"] = changed.Adjustment!.Status.ToString()
                }));
        }, op, request.IdempotencyKey, fingerprint, ct);

        return write.Kind == CycleCountLineWriteKind.Written
            ? Response<CycleCountLineDto>.Success(CycleCountReads.Line(write.Line!, CycleCountReads.ShowsExpected(write.Count!, request.ReadsVariances)))
            : await NotWrittenAsync(write, count, line.Id, op, request.IdempotencyKey, fingerprint, request.ReadsVariances, ct);
    }

    /// <summary>
    /// F-12 "aynı": the holder is THIS line's correction — the same movement type, SKU and item (DL-3 CT ORTA-2, owner decision Beste 2026-11-10:
    /// read from the holder's ledger row), base quantity, bin (into it for a gain, out of it for a loss, the other side empty), lot, stock status and
    /// <c>SourceLineId</c> — and no reversal names it.
    /// </summary>
    private static bool IsThisCorrection(CycleCountLine line, PostingKeyHolder holder, CycleCountLedgerMovementRow movement)
    {
        var a = line.Adjustment!;
        if (holder.ReversedByTransactionId is not null || movement.MovementType != a.MovementType) return false;
        if (movement.SkuId != line.SkuId) return false;
        if (movement.ItemId != line.ItemId) return false;
        var bin = line.LocationId.ToString("D");
        var status = WireNames.ToWire(line.StockStatus);
        var gain = a.MovementType == MovementType.CountGain;
        var sideOk = gain
            ? string.Equals(holder.ToLocationId, bin, StringComparison.OrdinalIgnoreCase) && holder.FromLocationId is null && holder.ToStockStatus == status
            : string.Equals(holder.FromLocationId, bin, StringComparison.OrdinalIgnoreCase) && holder.ToLocationId is null && holder.FromStockStatus == status;
        return sideOk && holder.BaseQuantity == a.Quantity && string.Equals(holder.LotId, line.LotId, StringComparison.Ordinal)
               && string.Equals(holder.SourceLineId, line.Id.ToString("D"), StringComparison.OrdinalIgnoreCase);
    }
}
