using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Traceability.Queries;
using Diten.InventoryService.Application.Features.Traceability.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Traceability.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Traceability.Handlers.QueryHandlers;

/// <summary>
/// getLotDetail (v1.1): the lot + its source — the FIRST inbound ledger movement carrying this lot in the caller's company
/// (read-only; absent until the ledger posts with registered lots, slice P2) — + its serial count. <c>stockSummary</c>
/// is the ledger per-lot projection of the caller's company (K-A, WP-0174-P2); absent while the lot has no position there.
/// </summary>
public sealed class GetLotDetailHandler : IRequestHandler<GetLotDetailQuery, Response<LotDetailDto>>
{
    private readonly ILotRepository _lots;

    public GetLotDetailHandler(ILotRepository lots) => _lots = lots;

    public async Task<Response<LotDetailDto>> Handle(GetLotDetailQuery request, CancellationToken cancellationToken)
    {
        if (!TraceWriteSupport.TryParseId(request.LotId, out var lotId)) return TraceWriteSupport.UnknownLot<LotDetailDto>();
        var lot = await _lots.FindAsync(lotId, cancellationToken);
        if (lot is null) return TraceWriteSupport.UnknownLot<LotDetailDto>();

        var inbound = await _lots.FirstInboundAsync(lotId, cancellationToken);
        var source = inbound is null
            ? null
            : new LotSourceDto
            {
                Kind = inbound.MovementType switch
                {
                    MovementType.GoodsReceiptPo => "GoodsReceipt",
                    MovementType.GoodsReceiptProd => "ProductionReceipt",
                    MovementType.OpeningBalance => "OpeningBalance",
                    _ => "Manual"
                },
                DocumentNumber = inbound.SourceDocumentId,
                Date = TraceMapping.Wire(inbound.TransactionDate)
            };

        var serials = await _lots.SerialCountAsync(lotId, cancellationToken);
        var stock = (await _lots.PositionsAsync([lotId], cancellationToken)).FirstOrDefault();
        return Response<LotDetailDto>.Success(TraceMapping.ToDetail(lot, TraceMapping.Today(DateTimeOffset.UtcNow), source, serials, stock));
    }
}
