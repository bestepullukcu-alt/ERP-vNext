using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Queries;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Traceability.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers;

/// <summary>
/// QUARANTINE-BUNDLE <c>getLotQualityStatus</c> (contract 1.0.0, frozen since Q1, served from Q5 — O-Q4-2): the quality picture of ONE
/// lot in the caller's company, for the consumers 0142 / 0176 / 0177 and the MOD-0174 Lot pages ("Onay bekliyor" badge, pack §11.4).
/// <list type="bullet">
/// <item><c>retestDue</c> — the lot's retest date is before today (TRACE's own rule, <c>LotDateRules.RetestDue</c>).</item>
/// <item>the open holds (not Decided) and their QUARANTINE / QUALITY_INSPECTION quantities; the open blocked-stock records and their
/// quantity (records of THIS company — every repository read is company-scoped).</item>
/// <item><c>pendingLotRelease</c> — the lot's LotRelease decision (KQ-I, one open claim per lot — Q4R) while it has not reached a
/// terminal state; absent otherwise.</item>
/// </list>
/// A registered lot id the tenant does not know → 404 <c>UNKNOWN_LOT</c>; a typed (unregistered) lot text with no record here → 404.
/// Like every other quarantine read (pack §8, the guaranteed path), the decisions this read touches — the pending lot release and
/// the open records' active decisions — are reconciled first (<c>ReconcileAfterSeconds</c>), so an approved release shows the lot
/// as decided without anyone opening the decision. Nothing else moves.
/// </summary>
public sealed class GetLotQualityStatusHandler : IRequestHandler<GetLotQualityStatusQuery, Response<LotQualityStatusDto>>
{
    private const int OpenRecordsCap = 10_000;

    private static readonly HashSet<DecisionState> Terminal =
    [
        DecisionState.Applied, DecisionState.Rejected, DecisionState.Cancelled, DecisionState.TimedOut, DecisionState.Withdrawn, DecisionState.FourEyesViolation
    ];

    private readonly IQuarantineRepository _repository;
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineReads _reads;
    private readonly QuarantineWorkflowReconciler _reconciler;

    public GetLotQualityStatusHandler(
        IQuarantineRepository repository, IQuarantineDecisionRepository decisions, QuarantineFacts facts, QuarantineReads reads, QuarantineWorkflowReconciler reconciler)
    {
        _repository = repository;
        _decisions = decisions;
        _facts = facts;
        _reads = reads;
        _reconciler = reconciler;
    }

    public async Task<Response<LotQualityStatusDto>> Handle(GetLotQualityStatusQuery request, CancellationToken ct)
    {
        var lotId = request.LotId?.Trim() ?? string.Empty;
        if (lotId.Length is 0 or > QuarantineContract.MaxTextLength)
        {
            return QuarantineSupport.Fail<LotQualityStatusDto>(InventoryErrorCodes.UnknownLot, "Lot not found.", 404);
        }

        var registered = LotPostingRules.IsRegisteredLotForm(lotId, out _);
        var lot = registered ? await _facts.LotAsync(lotId, ct) : null;
        if (registered && lot is null)
        {
            return QuarantineSupport.Fail<LotQualityStatusDto>(InventoryErrorCodes.UnknownLot, "Lot not found.", 404);
        }

        var lotKey = registered ? lot!.Id.ToString("D") : lotId;
        var (holds, blocked, release) = await SnapshotAsync(lotKey, ct);
        var touched = holds.Select(h => h.ActiveDecisionId).Concat(blocked.Select(b => b.ActiveDecisionId)).Append(release?.Id);
        if (await _reconciler.ReconcileAsync(touched, force: false, ct) == QuarantineWorkflowReconciler.Result.Reconciled)
        {
            (holds, blocked, release) = await SnapshotAsync(lotKey, ct);
            lot = registered ? await _facts.LotAsync(lotId, ct) : null;
        }

        PendingLotReleaseDto? pending = null;
        if (release is not null && !Terminal.Contains(QuarantineFacts.StateOf(await _decisions.EventsAsync([release.Id], ct))))
        {
            pending = new PendingLotReleaseDto(QuarantineMapping.Id(release.Id), release.DecisionNumber);
        }

        if (!registered && holds.Count == 0 && blocked.Count == 0 && pending is null)
        {
            return QuarantineSupport.Fail<LotQualityStatusDto>(InventoryErrorCodes.UnknownLot, "Lot not found.", 404);
        }

        var settings = await _facts.SettingsAsync(ct);
        var now = QuarantineSupport.Now();
        var lots = await _facts.LotsAsync(holds.Select(h => h.LotId).Concat(blocked.Select(b => b.LotId)), ct);
        var active = await _reads.ActiveAsync(holds.Select(h => h.ActiveDecisionId).Concat(blocked.Select(b => b.ActiveDecisionId)), ct);
        var reasons = (await _repository.BlockReasonsAsync(ct)).ToDictionary(r => r.Code, StringComparer.Ordinal);
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var retestDue = lot?.RetestDate is { } retest && LotDateRules.RetestDue(DateOnly.FromDateTime(retest.UtcDateTime), today);

        return Response<LotQualityStatusDto>.Success(new LotQualityStatusDto(
            lotKey,
            retestDue,
            holds.Where(h => h.StockStatus == StockStatus.Quarantine).Sum(h => h.Quantity),
            holds.Where(h => h.StockStatus == StockStatus.QualityInspection).Sum(h => h.Quantity),
            blocked.Sum(b => b.Quantity),
            holds.Select(h => (HoldRefDto)QuarantineMapping.HoldRef(h, lots, settings.WaitTargetDays, QuarantineMapping.Active(h.ActiveDecisionId, active), now)).ToList(),
            blocked.Select(b => QuarantineMapping.BlockedRef(b, lots, reasons, request.Language, null, QuarantineMapping.Active(b.ActiveDecisionId, active))).ToList(),
            pending));
    }

    /// <summary>The lot's open holds (not Decided), its open blocked-stock records and its LotRelease decision (the one claim, KQ-I).</summary>
    private async Task<(List<QuarantineHold> Holds, List<BlockedStock> Blocked, QuarantineDecision? Release)> SnapshotAsync(string lotKey, CancellationToken ct)
    {
        var holds = (await _repository.ListHoldsAsync(new HoldQuery(null, null, null, null, null, null, lotKey, null, 1, OpenRecordsCap), ct)).Items
            .Where(h => h.Status != HoldStatus.Decided)
            .ToList();
        var blocked = (await _repository.ListBlockedAsync(new BlockedQuery(null, null, false, null, BlockedStatus.Open, null, 1, OpenRecordsCap), ct)).Items
            .Where(b => string.Equals(b.LotId, lotKey, StringComparison.Ordinal))
            .ToList();
        var release = await _repository.FindLotReleaseClaimAsync(lotKey, ct) is { } claim ? await _decisions.FindAsync(claim.DecisionId, ct) : null;
        return (holds, blocked, release);
    }
}
