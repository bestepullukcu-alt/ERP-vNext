using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.Traceability.Queries;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.QueryHandlers;

/// <summary>
/// listLots (TRACE v1 filters itemId / status / expiryBefore + v1.1 q / skuId / skuLevel / warn / includeStock / page /
/// pageSize / sort). Without <c>page</c> and <c>pageSize</c> the answer is the v1 unpaged list (no page members).
/// <c>includeStock</c> is accepted; <c>stockSummary</c> comes from the ledger per-lot projection written by slice P2 —
/// until then no item carries it (K-A: never computed per request).
/// </summary>
public sealed class ListLotsHandler : IRequestHandler<ListLotsQuery, Response<LotListDto>>
{
    private readonly ILotRepository _lots;

    public ListLotsHandler(ILotRepository lots) => _lots = lots;

    public async Task<Response<LotListDto>> Handle(ListLotsQuery request, CancellationToken cancellationToken)
    {
        Guid? itemId = null, skuId = null;
        SkuLevel? skuLevel = null;
        LotStatus? status = null;
        DateOnly? expiryBefore = null;

        if (request.ItemId is not null)
        {
            if (!WireParsing.TryParseUuid(request.ItemId, out var parsed)) return Invalid("itemId must be a UUID.");
            itemId = parsed;
        }

        if (request.SkuId is not null)
        {
            if (!WireParsing.TryParseUuid(request.SkuId, out var parsed)) return Invalid("skuId must be a UUID.");
            skuId = parsed;
        }

        if (request.SkuLevel is not null)
        {
            if (!WireNames.TryParseSkuLevel(request.SkuLevel, out var level)) return Invalid("skuLevel must be Gsku, Lsku or FinishedGood.");
            skuLevel = level;
        }

        if (request.Status is not null)
        {
            if (!Enum.TryParse<LotStatus>(request.Status, ignoreCase: false, out var parsed) || !Enum.IsDefined(parsed) || int.TryParse(request.Status, out _))
                return Invalid("status must be Draft, Released, Blocked or Expired.");
            status = parsed;
        }

        if (request.ExpiryBefore is not null)
        {
            if (!DateOnly.TryParseExact(request.ExpiryBefore, TraceContract.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return Invalid("expiryBefore must be a date (yyyy-MM-dd).");
            expiryBefore = day;
        }

        if (request.Q is { Length: > TraceContract.MaxSearchLength }) return Invalid($"q must be at most {TraceContract.MaxSearchLength} characters.");
        if (!TraceQueryParsing.TryFlag(request.Warn, out var warn)) return Invalid("warn must be true or false.");
        if (!TraceQueryParsing.TryFlag(request.IncludeStock, out _)) return Invalid("includeStock must be true or false.");
        if (!TraceQueryParsing.TryPaging(request.Page, request.PageSize, out var page, out var pageSize))
            return Invalid("page must be ≥ 1 and pageSize 1-200.");
        if (!TrySort(request.Sort, out var sort, out var descending))
            return Invalid("sort must be expiryDate, lotNumber or createdAt, optionally prefixed with '-'.");

        var paged = request.Page is not null || request.PageSize is not null;
        var today = TraceMapping.Today(DateTimeOffset.UtcNow);
        var result = await _lots.ListAsync(new LotListQuery(
            itemId, skuId, skuLevel, status, expiryBefore, string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(), warn, today,
            paged ? page : null, paged ? pageSize : null, sort, descending), cancellationToken);

        return Response<LotListDto>.Success(new LotListDto
        {
            Items = result.Items.Select(l => TraceMapping.ToRef(l, today)).ToList(),
            Total = result.Total,
            Page = paged ? page : null,
            PageSize = paged ? pageSize : null
        });
    }

    private static bool TrySort(string? raw, out LotSortField field, out bool descending)
    {
        field = LotSortField.LotNumber;
        descending = false;
        if (raw is null) return true;
        descending = raw.StartsWith('-');
        var name = descending ? raw[1..] : raw;
        field = name switch
        {
            "lotNumber" => LotSortField.LotNumber,
            "expiryDate" => LotSortField.ExpiryDate,
            "createdAt" => LotSortField.CreatedAt,
            _ => (LotSortField)(-1)
        };
        return Enum.IsDefined(field);
    }

    private static Response<LotListDto> Invalid(string message) => TraceWriteSupport.Invalid<LotListDto>(message);
}
