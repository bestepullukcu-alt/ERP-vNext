using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Commands;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers;

/// <summary>
/// §4.5 "İncelemeye al" (no workflow — it moves no stock). If-Match on the hold; a repeat on a hold already in review answers
/// the current hold (idempotent). A decided hold → 409 <c>QUARANTINE_HOLD_CLOSED</c>.
/// </summary>
public sealed class StartReviewHandler : IRequestHandler<StartReviewCommand, Response<HoldRefDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineSupport _support;

    public StartReviewHandler(IQuarantineRepository repository, QuarantineFacts facts, QuarantineSupport support)
    {
        _repository = repository;
        _facts = facts;
        _support = support;
    }

    public async Task<Response<HoldRefDto>> Handle(StartReviewCommand request, CancellationToken ct)
    {
        if (request.Note is { Length: > QuarantineContract.MaxNoteLength })
        {
            return QuarantineSupport.Invalid<HoldRefDto>($"note must be at most {QuarantineContract.MaxNoteLength} characters.");
        }

        var hold = await _repository.FindHoldAsync(request.HoldId, ct);
        if (hold is null)
        {
            return QuarantineSupport.Fail<HoldRefDto>(QuarantineErrorCodes.HoldNotFound, "Hold not found.", 404);
        }

        if (hold.Status == HoldStatus.Decided)
        {
            return QuarantineSupport.Fail<HoldRefDto>(QuarantineDecisionRules.HoldClosed, "The hold is already decided.", 409);
        }

        if (hold.ReviewState != HoldReviewState.InReview)
        {
            if (hold.Version != request.ExpectedVersion)
            {
                return QuarantineSupport.Conflict<HoldRefDto>();
            }

            var now = QuarantineSupport.Now();
            hold.ReviewState = HoldReviewState.InReview;
            hold.ReviewStartedAtUtc = now;
            hold.ReviewStartedBy = _support.UserId;
            if (request.Note is { Length: > 0 } note)
            {
                hold.AnalysisNote = note;
            }

            hold.UpdatedAt = now;
            hold.Version = request.ExpectedVersion + 1;
            var saved = await _repository.InTransactionAsync(async (session, c) =>
            {
                if (!await session.ReplaceHoldAsync(hold, request.ExpectedVersion, c)) return false;
                await session.InsertHistoryAsync([_support.History(HistorySubject.Hold, hold.Id, HistoryAction.ReviewStarted, now, request.Note)], c);
                return true;
            }, ct);
            if (!saved)
            {
                return QuarantineSupport.Conflict<HoldRefDto>();
            }
        }

        var lots = await _facts.LotsAsync([hold.LotId], ct);
        return Response<HoldRefDto>.Success(QuarantineMapping.HoldRef(hold, lots, (await _facts.SettingsAsync(ct)).WaitTargetDays, null, QuarantineSupport.Now()));
    }
}

/// <summary>
/// §4.14 "İncelemeyi tamamla" (pack §21.3 #8): marks a blocked record reviewed, which opens "Blokeyi Kaldır" (V14). Reason ≥ 10,
/// If-Match; a closed record → 409 <c>QUARANTINE_BLOCKED_CLOSED</c>; already reviewed → the current record (idempotent).
/// </summary>
public sealed class CompleteBlockedReviewHandler : IRequestHandler<CompleteBlockedReviewCommand, Response<BlockedStockRefDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineSupport _support;

    public CompleteBlockedReviewHandler(IQuarantineRepository repository, QuarantineFacts facts, QuarantineSupport support)
    {
        _repository = repository;
        _facts = facts;
        _support = support;
    }

    public async Task<Response<BlockedStockRefDto>> Handle(CompleteBlockedReviewCommand request, CancellationToken ct)
    {
        if (QuarantineDecisionRules.Reason(request.Reason) is { } v3)
        {
            return QuarantineSupport.Fail<BlockedStockRefDto>(v3);
        }

        var blocked = await _repository.FindBlockedAsync(request.BlockedId, ct);
        if (blocked is null)
        {
            return QuarantineSupport.Fail<BlockedStockRefDto>(QuarantineErrorCodes.BlockedNotFound, "Blocked-stock record not found.", 404);
        }

        if (blocked.Status != BlockedStatus.Open)
        {
            return QuarantineSupport.Fail<BlockedStockRefDto>(QuarantineDecisionRules.BlockedClosed, "The blocked-stock record is closed.", 409);
        }

        if (!blocked.Reviewed)
        {
            if (blocked.Version != request.ExpectedVersion)
            {
                return QuarantineSupport.Conflict<BlockedStockRefDto>();
            }

            var now = QuarantineSupport.Now();
            blocked.Reviewed = true;
            blocked.UpdatedAt = now;
            blocked.Version = request.ExpectedVersion + 1;
            var saved = await _repository.InTransactionAsync(async (session, c) =>
            {
                if (!await session.ReplaceBlockedAsync(blocked, request.ExpectedVersion, c)) return false;
                await session.InsertHistoryAsync([_support.History(HistorySubject.Blocked, blocked.Id, HistoryAction.ReviewCompleted, now, request.Reason!.Trim())], c);
                return true;
            }, ct);
            if (!saved)
            {
                return QuarantineSupport.Conflict<BlockedStockRefDto>();
            }
        }

        var lots = await _facts.LotsAsync([blocked.LotId], ct);
        var reasons = (await _repository.BlockReasonsAsync(ct)).ToDictionary(r => r.Code, StringComparer.Ordinal);
        return Response<BlockedStockRefDto>.Success(QuarantineMapping.BlockedRef(blocked, lots, reasons, "en", null, null));
    }
}
