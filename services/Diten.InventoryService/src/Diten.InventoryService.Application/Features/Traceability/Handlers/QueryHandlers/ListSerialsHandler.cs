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
/// listSerials (v1.1). The stored status is Active / Consumed; "Blocked" = an Active serial of a Blocked or Expired lot,
/// so the status filter is translated into stored status + lot set (pack §4 <c>SerialNumber</c>). The stock-status part
/// of "Blocked" (a serial on a BLOCKED / QUARANTINE balance row) needs the ledger link of slice P2.
/// </summary>
public sealed class ListSerialsHandler : IRequestHandler<ListSerialsQuery, Response<SerialPageDto>>
{
    private readonly ISerialRepository _serials;
    private readonly ILotRepository _lots;

    public ListSerialsHandler(ISerialRepository serials, ILotRepository lots)
    {
        _serials = serials;
        _lots = lots;
    }

    public async Task<Response<SerialPageDto>> Handle(ListSerialsQuery request, CancellationToken cancellationToken)
    {
        Guid? skuId = null, lotId = null;
        SkuLevel? skuLevel = null;
        if (request.SkuId is not null)
        {
            if (!WireParsing.TryParseUuid(request.SkuId, out var parsed)) return Invalid("skuId must be a UUID.");
            skuId = parsed;
        }

        if (request.SkuLevel is not null)
        {
            if (!WireNames.TryParseLegacySkuLevel(request.SkuLevel, out var level)) return Invalid("skuLevel must be Gsku, Lsku or FinishedGood.");
            skuLevel = level;
        }

        if (request.LotId is not null)
        {
            if (!TraceWriteSupport.TryParseId(request.LotId, out var parsed)) return Invalid("lotId must be a lot id.");
            lotId = parsed;
        }

        if (request.Q is { Length: > TraceContract.MaxSearchLength }) return Invalid($"q must be at most {TraceContract.MaxSearchLength} characters.");
        if (request.Status is not (null or "Active" or "Consumed" or "Blocked")) return Invalid("status must be Active, Consumed or Blocked.");
        if (!TraceQueryParsing.TryPaging(request.Page, request.PageSize, out var page, out var pageSize))
            return Invalid("page must be ≥ 1 and pageSize 1-200.");

        SerialStatus? stored = null;
        IReadOnlyCollection<Guid>? inside = null, outside = null;
        if (request.Status is not null)
        {
            stored = request.Status == "Consumed" ? SerialStatus.Consumed : SerialStatus.Active;
            if (request.Status != "Consumed")
            {
                var unusable = await _lots.IdsWithStatusAsync([LotStatus.Blocked, LotStatus.Expired], cancellationToken);
                if (request.Status == "Blocked") inside = unusable;
                else outside = unusable;
            }
        }

        var result = await _serials.ListAsync(new SerialListQuery(
            string.IsNullOrWhiteSpace(request.Q) ? null : request.Q.Trim(), skuId, skuLevel, lotId, stored, inside, outside, page, pageSize),
            cancellationToken);

        var lots = (await _lots.FindManyAsync(result.Items.Where(s => s.LotId.HasValue).Select(s => s.LotId!.Value).Distinct().ToList(), cancellationToken))
            .ToDictionary(l => l.Id);
        return Response<SerialPageDto>.Success(new SerialPageDto
        {
            Items = result.Items.Select(s => TraceMapping.ToSerial(s, s.LotId is { } id && lots.TryGetValue(id, out var lot) ? lot : null)).ToList(),
            Total = result.Total,
            Page = page,
            PageSize = pageSize
        });
    }

    private static Response<SerialPageDto> Invalid(string message) => TraceWriteSupport.Invalid<SerialPageDto>(message);
}
