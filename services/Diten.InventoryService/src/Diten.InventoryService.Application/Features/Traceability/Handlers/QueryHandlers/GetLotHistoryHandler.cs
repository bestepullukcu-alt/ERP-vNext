using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability.Queries;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.QueryHandlers;

/// <summary>getLotHistory (v1.1): the append-only change history of the lot, newest first, paged.</summary>
public sealed class GetLotHistoryHandler : IRequestHandler<GetLotHistoryQuery, Response<LotHistoryPageDto>>
{
    private readonly ILotRepository _lots;

    public GetLotHistoryHandler(ILotRepository lots) => _lots = lots;

    public async Task<Response<LotHistoryPageDto>> Handle(GetLotHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!TraceQueryParsing.TryPaging(request.Page, request.PageSize, out var page, out var pageSize))
            return TraceWriteSupport.Invalid<LotHistoryPageDto>("page must be ≥ 1 and pageSize 1-200.");
        if (!TraceWriteSupport.TryParseId(request.LotId, out var lotId) || await _lots.FindAsync(lotId, cancellationToken) is null)
            return TraceWriteSupport.UnknownLot<LotHistoryPageDto>();

        var history = await _lots.HistoryAsync(lotId, page, pageSize, cancellationToken);
        return Response<LotHistoryPageDto>.Success(new LotHistoryPageDto
        {
            Items = history.Items.Select(TraceMapping.ToChange).ToList(),
            Total = history.Total,
            Page = page,
            PageSize = pageSize
        });
    }
}
