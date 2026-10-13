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
/// §4.4 listCases — filters type (R1: <c>Real</c>; <c>Drill</c> rows come with R5 and answer an empty page now), status (repeatable), class,
/// skuId, openedFrom (date), q (number / root lot number); sort <c>-openedAtUtc</c> (default) or <c>number</c>; paging 1–200; status chip counts.
/// </summary>
public sealed class ListRecallCasesHandler : IRequestHandler<ListRecallCasesQuery, Response<RecallCasePageDto>>
{
    private readonly IRecallRepository _repository;

    public ListRecallCasesHandler(IRecallRepository repository) => _repository = repository;

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

        var page = await _repository.ListCasesAsync(new RecallCaseQuery(statuses.Count == 0 ? null : statuses, @class, sku, from,
            string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(), request.Sort == "number", paging.Page, paging.PageSize), ct);
        var counts = page.StatusCounts.ToDictionary(p => p.Key.ToString(), p => p.Value, StringComparer.Ordinal);
        if (request.Type == "Drill")
        {
            // R5: drill runs are not cases; until then the Drill filter is an honest empty page
            return Response<RecallCasePageDto>.Success(new RecallCasePageDto([], 0, paging.Page, paging.PageSize, counts));
        }

        var items = (await _repository.ScopeItemsOfCasesAsync(page.Items.Select(c => c.Id).ToList(), ct)).ToLookup(i => (i.CaseId, i.Revision));
        return Response<RecallCasePageDto>.Success(new RecallCasePageDto(
            page.Items.Select(c => RecallMapping.Ref(c, items[(c.Id, c.ScopeRevision)].ToList())).ToList(), page.Total, paging.Page, paging.PageSize, counts));
    }
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
