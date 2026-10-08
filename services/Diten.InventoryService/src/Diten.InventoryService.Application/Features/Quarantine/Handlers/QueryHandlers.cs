using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Queries;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Entities;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Quarantine.Rules;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers;

/// <summary>Shared read helpers: decisions with their state, paging, enum filters.</summary>
public sealed class QuarantineReads
{
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly IQuarantineLedgerReader _ledger;

    public QuarantineReads(IQuarantineDecisionRepository decisions, IQuarantineLedgerReader ledger)
    {
        _decisions = decisions;
        _ledger = ledger;
    }

    public async Task<IReadOnlyDictionary<Guid, (QuarantineDecision Decision, DecisionState State)>> ActiveAsync(
        IEnumerable<Guid?> activeIds, CancellationToken ct)
    {
        var ids = activeIds.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        var result = new Dictionary<Guid, (QuarantineDecision, DecisionState)>();
        if (ids.Count == 0)
        {
            return result;
        }

        var events = (await _decisions.EventsAsync(ids, ct)).ToLookup(e => e.DecisionId);
        foreach (var id in ids)
        {
            if (await _decisions.FindAsync(id, ct) is { } decision)
            {
                result[id] = (decision, QuarantineFacts.StateOf(events[id]));
            }
        }

        return result;
    }

    public async Task<(IReadOnlyList<DecisionRefDto> Decisions, IReadOnlyList<Diten.InventoryService.Domain.Entities.InventoryTransaction> Movements)> TrailAsync(
        Guid targetId, IEnumerable<string> extraDocuments, IEnumerable<Guid> extraTransactions, CancellationToken ct)
    {
        var decisions = await _decisions.ListByTargetAsync(targetId, ct);
        var events = (await _decisions.EventsAsync(decisions.Select(d => d.Id).ToList(), ct)).ToLookup(e => e.DecisionId);
        var refs = decisions.OrderBy(d => d.CreatedAt)
            .Select(d => new DecisionRefDto(QuarantineMapping.Id(d.Id), d.DecisionNumber, d.Kind.ToString(), QuarantineFacts.StateOf(events[d.Id]).ToString()))
            .ToList();
        var documents = decisions.Select(d => d.DecisionNumber).Concat(extraDocuments).Distinct().ToList();
        var movements = await _ledger.TransactionsAsync(documents, extraTransactions.Distinct().ToList(), ct);
        return (refs, movements.OrderBy(m => m.CreatedAt).ToList());
    }

    public static bool TryPaging(string? page, string? pageSize, out int p, out int s)
    {
        p = 1;
        s = QuarantineContract.DefaultPageSize;
        if (page is not null && (!int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out p) || p < 1)) return false;
        return pageSize is null || (int.TryParse(pageSize, NumberStyles.None, CultureInfo.InvariantCulture, out s) && s is >= 1 and <= QuarantineContract.MaxPageSize);
    }

    public static bool TryEnum<T>(string? raw, out T? value) where T : struct, Enum
    {
        value = null;
        if (raw is null) return true;
        if (!Enum.TryParse<T>(raw, false, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(raw, out _)) return false;
        value = parsed;
        return true;
    }

    public static bool TryBool(string? raw, out bool value)
    {
        value = raw == "true";
        return raw is null or "true" or "false";
    }
}

/// <summary>§4.1 listHolds — page + KPI cards of the queue (pack §11.3). Open first, then the longest waiting.</summary>
public sealed class ListHoldsHandler : IRequestHandler<ListHoldsQuery, Response<HoldListDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineReads _reads;

    private readonly QuarantineWorkflowReconciler _reconciler;

    public ListHoldsHandler(IQuarantineRepository repository, QuarantineFacts facts, QuarantineReads reads, QuarantineWorkflowReconciler reconciler)
    {
        _repository = repository;
        _facts = facts;
        _reads = reads;
        _reconciler = reconciler;
    }

    public async Task<Response<HoldListDto>> Handle(ListHoldsQuery request, CancellationToken ct)
    {
        StockStatus? status = null;
        if (request.StockStatus is not null)
        {
            if (!WireNames.TryParseStockStatus(request.StockStatus, out var parsed) || !QuarantineStockEffects.HoldStatuses.Contains(parsed))
                return QuarantineSupport.Invalid<HoldListDto>("stockStatus must be QUARANTINE or QUALITY_INSPECTION.");
            status = parsed;
        }

        if (!QuarantineReads.TryEnum<HoldSource>(request.Source, out var source) || !QuarantineReads.TryEnum<HoldStatus>(request.Status, out var holdStatus)
            || !QuarantineReads.TryEnum<HoldReviewState>(request.ReviewState, out var review) || !QuarantineReads.TryBool(request.LateOnly, out var lateOnly)
            || !QuarantineReads.TryPaging(request.Page, request.PageSize, out var page, out var size)
            || (request.WarehouseId is not null && !QuarantineSupport.TryParseId(request.WarehouseId, out _))
            || request.Q is { Length: > QuarantineContract.MaxTextLength })
        {
            return QuarantineSupport.Invalid<HoldListDto>("A filter is malformed (source, status, reviewState, lateOnly, page, pageSize, warehouseId, q).");
        }

        var settings = await _facts.SettingsAsync(ct);
        var now = QuarantineSupport.Now();
        DateTimeOffset? enteredBefore = lateOnly ? now.AddDays(-settings.WaitTargetDays) : null;
        var query = new HoldQuery(request.Q?.Trim(), request.WarehouseId, status, source, lateOnly ? null : holdStatus, review, request.LotId, enteredBefore, page, size);
        var result = await _repository.ListHoldsAsync(query, ct);
        // Q2: read-time reconciliation of the decisions this page shows (pack §8, the guaranteed path); re-read when applied
        if (await _reconciler.ReconcileAsync(result.Items.Select(h => h.ActiveDecisionId), force: false, ct) == QuarantineWorkflowReconciler.Result.Reconciled)
        {
            result = await _repository.ListHoldsAsync(query, ct);
        }
        var items = result.Items;
        if (lateOnly)
        {
            items = items.Where(h => h.Status != HoldStatus.Decided && (holdStatus is null || h.Status == holdStatus)).ToList();
        }

        var lots = await _facts.LotsAsync(items.Select(h => h.LotId), ct);
        var active = await _reads.ActiveAsync(items.Select(h => h.ActiveDecisionId), ct);
        var refs = items.Select(h => QuarantineMapping.HoldRef(h, lots, settings.WaitTargetDays, QuarantineMapping.Active(h.ActiveDecisionId, active), now))
            .OrderBy(h => h.Status == nameof(HoldStatus.Decided) ? 1 : 0).ThenByDescending(h => h.WaitDays - h.TargetDays).ToList();

        // KPI cards over the company's open holds (pack §11.3), independent of the page.
        var open = (await _repository.ListHoldsAsync(new HoldQuery(null, null, null, null, null, null, null, null, 1, 10_000), ct)).Items
            .Where(h => h.Status != HoldStatus.Decided).ToList();
        var openLots = await _facts.LotsAsync(open.Select(h => h.LotId), ct);
        var kpis = new HoldKpisDto(
            open.Count,
            open.Count(h => h.ReviewState == HoldReviewState.InReview),
            open.GroupBy(h => h.BaseUomId).Select(g => new QuantityByUomDto(g.Key, g.Sum(h => h.Quantity))).OrderBy(q => q.UomCode, StringComparer.Ordinal).ToList(),
            open.Count(h => QuarantineMapping.WaitDays(h.EnteredAtUtc, null, now) > settings.WaitTargetDays),
            open.Count(h => h.LotId is not null && openLots.TryGetValue(h.LotId, out var lot) && lot.ExpiryDate is { } e
                            && e <= now.AddDays(QuarantineContract.ExpirySoonDays)));
        return Response<HoldListDto>.Success(new HoldListDto(refs, lateOnly ? refs.Count : result.Total, kpis));
    }
}

/// <summary>§4.2 getHold — detail with history, the ledger movements of the hold and its decisions, decisions list.</summary>
public sealed class GetHoldHandler : IRequestHandler<GetHoldQuery, Response<HoldDetailDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineReads _reads;

    private readonly QuarantineWorkflowReconciler _reconciler;

    public GetHoldHandler(IQuarantineRepository repository, QuarantineFacts facts, QuarantineReads reads, QuarantineWorkflowReconciler reconciler)
    {
        _repository = repository;
        _facts = facts;
        _reads = reads;
        _reconciler = reconciler;
    }

    public async Task<Response<HoldDetailDto>> Handle(GetHoldQuery request, CancellationToken ct)
    {
        if (!QuarantineSupport.TryParseId(request.HoldId, out var id) || await _repository.FindHoldAsync(id, ct) is not { } hold)
        {
            return QuarantineSupport.Fail<HoldDetailDto>(QuarantineErrorCodes.HoldNotFound, "Hold not found.", 404);
        }

        if (await _reconciler.ReconcileAsync([hold.ActiveDecisionId], force: false, ct) == QuarantineWorkflowReconciler.Result.Reconciled)
        {
            hold = (await _repository.FindHoldAsync(id, ct))!;
        }

        var settings = await _facts.SettingsAsync(ct);
        var lots = await _facts.LotsAsync([hold.LotId], ct);
        var active = await _reads.ActiveAsync([hold.ActiveDecisionId], ct);
        var head = QuarantineMapping.HoldRef(hold, lots, settings.WaitTargetDays, QuarantineMapping.Active(hold.ActiveDecisionId, active), QuarantineSupport.Now());
        var history = await _repository.HistoryAsync(HistorySubject.Hold, hold.Id, ct);
        var source = hold.SourceMovementId is { } s && Guid.TryParse(s, out var tx) ? [tx] : Array.Empty<Guid>();
        var (decisions, movements) = await _reads.TrailAsync(hold.Id, [hold.HoldNumber], source, ct);
        return Response<HoldDetailDto>.Success(QuarantineMapping.HoldDetail(head, hold, history, movements, decisions));
    }
}

/// <summary>§4.8 getDecision.</summary>
public sealed class GetDecisionHandler : IRequestHandler<GetDecisionQuery, Response<DecisionDto>>
{
    private readonly IQuarantineDecisionRepository _decisions;

    private readonly QuarantineWorkflowReconciler _reconciler;

    public GetDecisionHandler(IQuarantineDecisionRepository decisions, QuarantineWorkflowReconciler reconciler)
    {
        _decisions = decisions;
        _reconciler = reconciler;
    }

    public async Task<Response<DecisionDto>> Handle(GetDecisionQuery request, CancellationToken ct)
    {
        if (!QuarantineSupport.TryParseId(request.DecisionId, out var id) || await _decisions.FindAsync(id, ct) is not { } decision)
        {
            return QuarantineSupport.Fail<DecisionDto>(QuarantineErrorCodes.DecisionNotFound, "Decision not found.", 404);
        }

        await _reconciler.ReconcileAsync([id], force: false, ct);
        return Response<DecisionDto>.Success(QuarantineMapping.Decision(decision, await _decisions.EventsAsync([id], ct)));
    }
}

/// <summary>
/// §4.12 listBlocked — the company's blocked-stock records. The virtual "Bloke lot" rows of lot-blocked lots (origin
/// <c>LotBlocked</c>, CT-D4) are NOT listed by slice Q1 (FU-0175-Q1-LOTBLOCKED, evidence §7).
/// </summary>
public sealed class ListBlockedHandler : IRequestHandler<ListBlockedQuery, Response<BlockedListDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineReads _reads;

    private readonly QuarantineWorkflowReconciler _reconciler;

    public ListBlockedHandler(IQuarantineRepository repository, QuarantineFacts facts, QuarantineReads reads, QuarantineWorkflowReconciler reconciler)
    {
        _repository = repository;
        _facts = facts;
        _reads = reads;
        _reconciler = reconciler;
    }

    public async Task<Response<BlockedListDto>> Handle(ListBlockedQuery request, CancellationToken ct)
    {
        if (!QuarantineReads.TryEnum<BlockedOrigin>(request.Origin, out var origin) || !QuarantineReads.TryEnum<BlockedStatus>(request.Status, out var status)
            || !QuarantineReads.TryBool(request.PendingOnly, out var pendingOnly) || !QuarantineReads.TryPaging(request.Page, request.PageSize, out var page, out var size)
            || (request.WarehouseId is not null && !QuarantineSupport.TryParseId(request.WarehouseId, out _))
            || request.Q is { Length: > QuarantineContract.MaxTextLength })
        {
            return QuarantineSupport.Invalid<BlockedListDto>("A filter is malformed (origin, status, pendingOnly, page, pageSize, warehouseId, q).");
        }

        var query = new BlockedQuery(request.Q?.Trim(), request.BlockReasonCode, pendingOnly, origin, status, request.WarehouseId, page, size);
        var result = await _repository.ListBlockedAsync(query, ct);
        if (await _reconciler.ReconcileAsync(result.Items.Select(b => b.ActiveDecisionId), force: false, ct) == QuarantineWorkflowReconciler.Result.Reconciled)
        {
            result = await _repository.ListBlockedAsync(query, ct);
        }
        var lots = await _facts.LotsAsync(result.Items.Select(b => b.LotId), ct);
        var reasons = (await _repository.BlockReasonsAsync(ct)).ToDictionary(r => r.Code, StringComparer.Ordinal);
        var active = await _reads.ActiveAsync(result.Items.Select(b => b.ActiveDecisionId), ct);
        var items = result.Items
            .Select(b => QuarantineMapping.BlockedRef(b, lots, reasons, request.Language, null, QuarantineMapping.Active(b.ActiveDecisionId, active)))
            .ToList();
        return Response<BlockedListDto>.Success(new BlockedListDto(items, result.Total));
    }
}

/// <summary>§4.13 getBlocked.</summary>
public sealed class GetBlockedHandler : IRequestHandler<GetBlockedQuery, Response<BlockedStockDetailDto>>
{
    private readonly IQuarantineRepository _repository;
    private readonly IQuarantineDecisionRepository _decisions;
    private readonly QuarantineFacts _facts;
    private readonly QuarantineReads _reads;

    private readonly QuarantineWorkflowReconciler _reconciler;

    public GetBlockedHandler(IQuarantineRepository repository, IQuarantineDecisionRepository decisions, QuarantineFacts facts, QuarantineReads reads, QuarantineWorkflowReconciler reconciler)
    {
        _repository = repository;
        _decisions = decisions;
        _facts = facts;
        _reads = reads;
        _reconciler = reconciler;
    }

    public async Task<Response<BlockedStockDetailDto>> Handle(GetBlockedQuery request, CancellationToken ct)
    {
        if (!QuarantineSupport.TryParseId(request.BlockedId, out var id) || await _repository.FindBlockedAsync(id, ct) is not { } blocked)
        {
            return QuarantineSupport.Fail<BlockedStockDetailDto>(QuarantineErrorCodes.BlockedNotFound, "Blocked-stock record not found.", 404);
        }

        if (await _reconciler.ReconcileAsync([blocked.ActiveDecisionId], force: false, ct) == QuarantineWorkflowReconciler.Result.Reconciled)
        {
            blocked = (await _repository.FindBlockedAsync(id, ct))!;
        }

        var lots = await _facts.LotsAsync([blocked.LotId], ct);
        var reasons = (await _repository.BlockReasonsAsync(ct)).ToDictionary(r => r.Code, StringComparer.Ordinal);
        var active = await _reads.ActiveAsync([blocked.ActiveDecisionId], ct);
        OriginRefDto? origin = null;
        if (blocked.OriginHoldId is { } holdId || blocked.OriginDecisionId is not null)
        {
            var hold = blocked.OriginHoldId is { } h ? await _repository.FindHoldAsync(h, ct) : null;
            var decision = blocked.OriginDecisionId is { } d ? await _decisions.FindAsync(d, ct) : null;
            origin = new OriginRefDto(hold?.HoldNumber, decision?.DecisionNumber);
        }

        var head = QuarantineMapping.BlockedRef(blocked, lots, reasons, request.Language, origin, QuarantineMapping.Active(blocked.ActiveDecisionId, active));
        var history = await _repository.HistoryAsync(HistorySubject.Blocked, blocked.Id, ct);
        var source = blocked.SourceMovementId is { } s && Guid.TryParse(s, out var tx) ? [tx] : Array.Empty<Guid>();
        var (decisions, movements) = await _reads.TrailAsync(blocked.Id, [blocked.BlockedNumber], source, ct);
        return Response<BlockedStockDetailDto>.Success(new BlockedStockDetailDto
        {
            BlockedId = head.BlockedId,
            BlockedNumber = head.BlockedNumber,
            LegalEntityId = head.LegalEntityId,
            Item = head.Item,
            Lot = head.Lot,
            Warehouse = head.Warehouse,
            Location = head.Location,
            Quantity = head.Quantity,
            BaseUom = head.BaseUom,
            BlockReason = head.BlockReason,
            Note = head.Note,
            BlockedAtUtc = head.BlockedAtUtc,
            Origin = head.Origin,
            OriginRef = head.OriginRef,
            PendingDisposition = head.PendingDisposition,
            Reviewed = head.Reviewed,
            Status = head.Status,
            AllowedActions = head.AllowedActions,
            ActiveDecision = head.ActiveDecision,
            Version = head.Version,
            History = history.Select(QuarantineMapping.History).ToList(),
            Movements = movements.Select(QuarantineMapping.Movement).ToList(),
            Decisions = decisions
        });
    }
}
