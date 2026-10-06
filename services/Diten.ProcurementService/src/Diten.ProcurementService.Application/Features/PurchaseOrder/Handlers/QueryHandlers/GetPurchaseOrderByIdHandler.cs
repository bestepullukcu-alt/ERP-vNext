using Diten.ProcurementService.Application.Features.PurchaseOrder.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Handlers.QueryHandlers;

public sealed class GetPurchaseOrderByIdHandler
    : IRequestHandler<GetPurchaseOrderByIdQuery, Response<PurchaseOrderDto>>
{
    private readonly IPurchaseOrderRepository _repository;

    public GetPurchaseOrderByIdHandler(IPurchaseOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<PurchaseOrderDto>> Handle(
        GetPurchaseOrderByIdQuery request,
        CancellationToken cancellationToken)
    {
        // Cross-tenant / cross-LE erişim → repository null döner → 404 (NOT_FOUND; sızıntı yok).
        var entity = await _repository.GetByPoIdAsync(request.PoId, cancellationToken);
        if (entity is null)
        {
            return Response<PurchaseOrderDto>.Fail("NOT_FOUND", 404);
        }

        return Response<PurchaseOrderDto>.Success(PurchaseOrderMapping.ToDto(entity));
    }
}
