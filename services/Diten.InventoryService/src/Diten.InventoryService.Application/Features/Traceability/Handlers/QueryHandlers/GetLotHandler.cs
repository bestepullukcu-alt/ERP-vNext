using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability.Queries;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.QueryHandlers;

/// <summary>getLot (TRACE v1): the lot of the caller's tenant (identity tenant-wide, CT-D3), with its ETag; else 404.</summary>
public sealed class GetLotHandler : IRequestHandler<GetLotQuery, Response<LotRefDto>>
{
    private readonly ILotRepository _lots;

    public GetLotHandler(ILotRepository lots) => _lots = lots;

    public async Task<Response<LotRefDto>> Handle(GetLotQuery request, CancellationToken cancellationToken)
    {
        if (!TraceWriteSupport.TryParseId(request.LotId, out var lotId)) return TraceWriteSupport.UnknownLot<LotRefDto>();
        var lot = await _lots.FindAsync(lotId, cancellationToken);
        return lot is null
            ? TraceWriteSupport.UnknownLot<LotRefDto>()
            : Response<LotRefDto>.Success(TraceMapping.ToRef(lot, TraceMapping.Today(DateTimeOffset.UtcNow), withETag: true));
    }
}
