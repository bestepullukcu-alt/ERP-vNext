using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Recall.Queries;
using Diten.InventoryService.Application.Features.Recall.Services;
using Diten.InventoryService.Domain.Recall.Enums;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Recall.Rules;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Recall.Handlers;

/// <summary>
/// §4.4 listCases — R6 (RECALL 1.5.0, CT decision A): the UNION read of Vakalar. Real cases travel in <c>items</c> (CaseRef); drill runs
/// travel in <c>drillRows</c> (DrillCaseRow — a drill run is not a case: no class, no level, pack §4.11 / §4.12, mockup difference D-25).
/// ONE page is cut here over both kinds:
/// <list type="bullet">
/// <item>order <c>-openedAtUtc</c> (default): case <c>openedAtUtc</c> / drill <c>startedAtUtc</c> descending, then number descending, a case
///   before a drill on a full tie; order <c>number</c>: number ascending (ordinal), case first on a tie;</item>
/// <item>the page takes the first page × pageSize cases from the store and every matching drill run (few per company), merges, skips, takes —
///   the same page asked twice gives the same rows; every row carries <c>sortAt</c> + <c>position</c> (0-based in the page);</item>
/// <item>filters: type Real → cases only, Drill → drills only, absent → both; <c>status</c> (CaseStatus) filters cases and <c>drillStatus</c>
///   (DrillRunStatus) drills — a status of only ONE kind excludes the other kind; <c>class</c> excludes the drill rows (a drill has no class);
///   skuId (the drill lot's SKU), openedFrom (drill startedAtUtc) and q (number / lot number) apply to both.</item>
/// </list>
/// Status chip counts: <c>statusCounts</c> (cases, as before) and <c>drillStatusCounts</c> (drills under the other filters, when drills are listed).
/// </summary>
public sealed class ListRecallCasesHandler : IRequestHandler<ListRecallCasesQuery, Response<RecallCasePageDto>>
{
    private readonly IRecallRepository _repository;
    private readonly IRecallDrillRepository _drills;
    private readonly Ports.IRecallTraceSource _lots;

    public ListRecallCasesHandler(IRecallRepository repository, IRecallDrillRepository drills, Ports.IRecallTraceSource lots)
    {
        _repository = repository;
        _drills = drills;
        _lots = lots;
    }

    public async Task<Response<RecallCasePageDto>> Handle(ListRecallCasesQuery request, CancellationToken ct)
    {
        if (request.Type is not (null or "Real" or "Drill")) return RecallSupport.Invalid<RecallCasePageDto>("type must be Real or Drill.");

        var statuses = new List<RecallCaseStatus>();
        foreach (var raw in request.Status)
        {
            if (!RecallWire.TryParseName<RecallCaseStatus>(raw, out var status)) return RecallSupport.Invalid<RecallCasePageDto>("Unknown status.");
            statuses.Add(status);
        }

        int? @class = null;
        if (request.Class is not null)
        {
            if (request.Class is not ("1" or "2" or "3")) return RecallSupport.Invalid<RecallCasePageDto>("class must be 1, 2 or 3.");
            @class = int.Parse(request.Class, CultureInfo.InvariantCulture);
        }

        Guid? sku = null;
        if (request.SkuId is not null)
        {
            if (!RecallSupport.TryParseId(request.SkuId, out var parsed)) return RecallSupport.Invalid<RecallCasePageDto>("skuId must be a canonical UUID.");
            sku = parsed;
        }

        DateTimeOffset? from = null;
        if (request.OpenedFrom is not null)
        {
            if (!DateOnly.TryParseExact(request.OpenedFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return RecallSupport.Invalid<RecallCasePageDto>("openedFrom must be a date yyyy-MM-dd.");
            from = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        }

        if (request.Sort is not (null or "-openedAtUtc" or "number")) return RecallSupport.Invalid<RecallCasePageDto>("sort must be -openedAtUtc or number.");
        if (request.Q is { Length: > 100 }) return RecallSupport.Invalid<RecallCasePageDto>("q must be at most 100 characters.");
        if (RecallPaging.Parse(request.Page, request.PageSize) is not { } paging)
        {
            return RecallSupport.Invalid<RecallCasePageDto>($"page must be ≥ 1 and pageSize 1-{RecallContract.MaxPageSize}.");
        }

        var drillStatuses = new List<RecallDrillRunStatus>();
        foreach (var raw in request.DrillStatus ?? [])
        {
            if (!RecallWire.TryParseName<RecallDrillRunStatus>(raw, out var status)) return RecallSupport.Invalid<RecallCasePageDto>("Unknown drillStatus.");
            drillStatuses.Add(status);
        }

        var search = string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim();
        var byNumber = request.Sort == "number";
        var wantCases = request.Type != "Drill" && !(drillStatuses.Count > 0 && statuses.Count == 0);
        var wantDrills = request.Type != "Real" && @class is null && !(statuses.Count > 0 && drillStatuses.Count == 0);
        var need = paging.Page * paging.PageSize;

        // cases: the first page × pageSize of the store's own order (+ the chips, always, as before)
        var casePage = await _repository.ListCasesAsync(new RecallCaseQuery(statuses.Count == 0 ? null : statuses, @class, sku, from, search, byNumber,
            1, wantCases ? need : 1), ct);
        var counts = casePage.StatusCounts.ToDictionary(p => p.Key.ToString(), p => p.Value, StringComparer.Ordinal);
        IReadOnlyList<Domain.Recall.Entities.RecallCase> cases = wantCases ? casePage.Items : [];

        // drill runs: every matching run of the company (their SKU is the lot's — read from the lot master)
        IReadOnlyList<Domain.Recall.Entities.RecallDrillRun> runs = [];
        Dictionary<string, Ports.TraceLot> lotOf = new(StringComparer.Ordinal);
        Dictionary<string, long>? drillCounts = null;
        if (wantDrills)
        {
            var all = await _drills.CaseListRunsAsync(new RecallDrillCaseRowQuery(null, from, search), ct);
            var lotIds = all.Select(r => Guid.TryParseExact(r.LotId, "D", out var id) ? id : (Guid?)null).OfType<Guid>().Distinct().ToList();
            lotOf = (await _lots.LotsAsync(lotIds, ct)).ToDictionary(l => l.Id.ToString("D"), StringComparer.Ordinal);
            var matching = all.Where(r => sku is null || (lotOf.TryGetValue(r.LotId, out var lot) && lot.SkuId == sku)).ToList();
            drillCounts = Enum.GetValues<RecallDrillRunStatus>().ToDictionary(s => s.ToString(), s => (long)matching.Count(r => r.Status == s), StringComparer.Ordinal);
            runs = drillStatuses.Count == 0 ? matching : matching.Where(r => drillStatuses.Contains(r.Status)).ToList();
        }

        var merged = cases.Select(c => new Row(c.OpenedAtUtc, c.Number, 0, c, null))
            .Concat(runs.Select(r => new Row(r.StartedAtUtc, r.Number, 1, null, r)))
            .OrderBy(r => r, byNumber ? NumberOrder : TimeOrder)
            .Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).ToList();

        var pageCases = merged.Where(r => r.Case is not null).Select(r => r.Case!).ToList();
        var items = (await _repository.ScopeItemsOfCasesAsync(pageCases.Select(c => c.Id).ToList(), ct)).ToLookup(i => (i.CaseId, i.Revision));
        var caseRows = new List<RecallCaseRefDto>();
        var drillRows = new List<RecallDrillCaseRowDto>();
        for (var position = 0; position < merged.Count; position++)
        {
            var row = merged[position];
            if (row.Case is { } c)
            {
                caseRows.Add(RecallMapping.Ref(c, items[(c.Id, c.ScopeRevision)].ToList()) with { SortAt = RecallSupport.Wire(row.SortAt), Position = position });
                continue;
            }

            var run = row.Run!;
            var item = lotOf.TryGetValue(run.LotId, out var lot)
                ? new RecallItemRefDto(lot.ItemId.ToString("D"), lot.SkuId.ToString("D"), Domain.Rules.WireNames.ToWire(lot.SkuLevel))
                : null;
            drillRows.Add(new RecallDrillCaseRowDto(run.Id.ToString("D"), run.Number, RecallContract.CaseTypeDrill, run.Status.ToString(),
                new RecallLotRefDto(run.LotId, run.LotNumber), item, RecallSupport.Wire(run.StartedAtUtc), RecallSupport.Wire(run.EndedAtUtc),
                new RecallUserRefDto(run.StartedBy.ToString("D")), run.DurationMinutes, run.TargetHours, run.Passed, RecallSupport.Wire(row.SortAt), position,
                run.Version));
        }

        var total = (wantCases ? casePage.Total : 0) + (wantDrills ? runs.Count : 0);
        return Response<RecallCasePageDto>.Success(new RecallCasePageDto(caseRows, total, paging.Page, paging.PageSize, counts,
            wantDrills ? drillRows : null, drillCounts));
    }

    private sealed record Row(DateTimeOffset SortAt, string Number, int Kind, Domain.Recall.Entities.RecallCase? Case, Domain.Recall.Entities.RecallDrillRun? Run);

    /// <summary>-openedAtUtc: newest first, then number descending, a case (0) before a drill (1).</summary>
    private static readonly Comparer<Row> TimeOrder = Comparer<Row>.Create((a, b) =>
    {
        var byTime = b.SortAt.CompareTo(a.SortAt);
        if (byTime != 0) return byTime;
        var byNumber = string.CompareOrdinal(b.Number, a.Number);
        return byNumber != 0 ? byNumber : a.Kind.CompareTo(b.Kind);
    });

    /// <summary>number: ascending (ordinal), a case before a drill.</summary>
    private static readonly Comparer<Row> NumberOrder = Comparer<Row>.Create((a, b) =>
    {
        var byNumber = string.CompareOrdinal(a.Number, b.Number);
        return byNumber != 0 ? byNumber : a.Kind.CompareTo(b.Kind);
    });
}

/// <summary>§4.5 getCase — scoped to the caller's company (another company's id → the controller answers 403).</summary>
public sealed class GetRecallCaseHandler : IRequestHandler<GetRecallCaseQuery, Response<RecallCaseDetailDto>>
{
    private readonly RecallReads _reads;

    public GetRecallCaseHandler(RecallReads reads) => _reads = reads;

    public Task<Response<RecallCaseDetailDto>> Handle(GetRecallCaseQuery request, CancellationToken ct)
        => RecallSupport.TryParseId(request.CaseId, out var id) ? _reads.CurrentAsync(id, ct) : Task.FromResult(RecallSupport.CaseNotFound<RecallCaseDetailDto>());
}

/// <summary>§4.6 getCaseScope — the items with their own and their effective inclusion (CT-R6) and the current canonical SHA-256.</summary>
public sealed class GetRecallCaseScopeHandler : IRequestHandler<GetRecallCaseScopeQuery, Response<RecallScopeViewDto>>
{
    private readonly RecallReads _reads;

    public GetRecallCaseScopeHandler(RecallReads reads) => _reads = reads;

    public Task<Response<RecallScopeViewDto>> Handle(GetRecallCaseScopeQuery request, CancellationToken ct)
        => RecallSupport.TryParseId(request.CaseId, out var id) ? _reads.CurrentScopeAsync(id, ct) : Task.FromResult(RecallSupport.CaseNotFound<RecallScopeViewDto>());
}

/// <summary>§4.12 getCaseTimeline — the insert-only events, oldest first, 1-based positions.</summary>
public sealed class GetRecallCaseTimelineHandler : IRequestHandler<GetRecallCaseTimelineQuery, Response<RecallTimelinePageDto>>
{
    private readonly IRecallRepository _repository;

    public GetRecallCaseTimelineHandler(IRecallRepository repository) => _repository = repository;

    public async Task<Response<RecallTimelinePageDto>> Handle(GetRecallCaseTimelineQuery request, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(request.CaseId, out var id) || await _repository.FindCaseAsync(id, ct) is null)
        {
            return RecallSupport.CaseNotFound<RecallTimelinePageDto>();
        }

        if (RecallPaging.Parse(request.Page, request.PageSize) is not { } paging)
        {
            return RecallSupport.Invalid<RecallTimelinePageDto>($"page must be ≥ 1 and pageSize 1-{RecallContract.MaxPageSize}.");
        }

        var result = await _repository.EventsAsync(id, paging.Page, paging.PageSize, ct);
        var offset = (long)(paging.Page - 1) * paging.PageSize;
        return Response<RecallTimelinePageDto>.Success(new RecallTimelinePageDto(
            result.Items.Select((e, i) => RecallMapping.Timeline(e, offset + i + 1)).ToList(), result.Total, paging.Page, paging.PageSize));
    }
}

/// <summary>
/// §4.13 getLotRecallStatus — is the lot in the approved scope of a case that is Active or in its effectiveness check, in ANY company of
/// the caller's tenant (CT-R7: a recalled lot is recalled everywhere). Another company's case shows its number and status only — never its
/// id or content. A lot of another tenant does not exist (404 <c>UNKNOWN_LOT</c>).
/// </summary>
public sealed class GetLotRecallStatusHandler : IRequestHandler<GetLotRecallStatusQuery, Response<RecallLotStatusDto>>
{
    private readonly IRecallRepository _repository;
    private readonly ILotRepository _lots;
    private readonly RecallSupport _support;

    public GetLotRecallStatusHandler(IRecallRepository repository, ILotRepository lots, RecallSupport support)
    {
        _repository = repository;
        _lots = lots;
        _support = support;
    }

    public async Task<Response<RecallLotStatusDto>> Handle(GetLotRecallStatusQuery request, CancellationToken ct)
    {
        if (!RecallSupport.TryParseId(request.LotId, out var lotId) || await _lots.FindAsync(lotId, ct) is null)
        {
            return RecallSupport.Fail<RecallLotStatusDto>(RecallErrorCodes.UnknownLot, "Lot not found.", 404);
        }

        var cases = (await _repository.ActiveLotBlocksAsync(lotId.ToString("D"), ct))
            .Where(b => b.CaseStatus is { } s && RecallCaseRules.IsUnderRecall(s))
            .Select(b =>
            {
                var same = b.CaseLegalEntityId == _support.LegalEntityId;
                return new RecallLotCaseDto(same ? b.CaseId.ToString("D") : null, b.CaseNumber, b.CaseStatus!.Value.ToString(), same);
            })
            .ToList();
        return Response<RecallLotStatusDto>.Success(new RecallLotStatusDto(lotId.ToString("D"), cases.Count > 0, cases));
    }
}
