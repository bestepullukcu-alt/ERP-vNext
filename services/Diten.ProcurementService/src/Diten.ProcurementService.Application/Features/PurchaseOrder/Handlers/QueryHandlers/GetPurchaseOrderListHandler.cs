using Diten.ProcurementService.Application.Features.PurchaseOrder.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using PurchaseOrderEntity = Diten.ProcurementService.Domain.Entities.PurchaseOrder;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Handlers.QueryHandlers;

public sealed class GetPurchaseOrderListHandler
    : IRequestHandler<GetPurchaseOrderListQuery, Response<PurchaseOrderListResultDto>>
{
    // Cursor sayfa boyutu (contract listPurchaseOrders cursor). Sabit; policy netleşince config'e taşınır.
    private const int PageSize = 50;

    private readonly IPurchaseOrderRepository _repository;

    public GetPurchaseOrderListHandler(IPurchaseOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<PurchaseOrderListResultDto>> Handle(
        GetPurchaseOrderListQuery request,
        CancellationToken cancellationToken)
    {
        // Repository, Tenant + LegalEntity + IsDeleted=false ile filtreler (cross-LE/tenant sızıntısı yok).
        var entities = await _repository.GetAllAsync(request.SupplierId, request.Status, cancellationToken);

        // Cursor = son dönen PoId (opaque). Kararlı sıralama PoId.
        var ordered = entities
            .OrderBy(x => x.PoId, StringComparer.Ordinal)
            .ToList();

        IEnumerable<PurchaseOrderEntity> page = ordered;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            page = ordered.Where(x => string.CompareOrdinal(x.PoId, request.Cursor) > 0);
        }

        var pageList = page.Take(PageSize + 1).ToList();
        var hasMore = pageList.Count > PageSize;
        var items = pageList.Take(PageSize).Select(PurchaseOrderMapping.ToDto).ToList();
        var nextCursor = hasMore ? pageList[PageSize - 1].PoId : null;

        var result = new PurchaseOrderListResultDto(items, nextCursor, PurchaseOrderContract.Version);
        return Response<PurchaseOrderListResultDto>.Success(result);
    }
}
