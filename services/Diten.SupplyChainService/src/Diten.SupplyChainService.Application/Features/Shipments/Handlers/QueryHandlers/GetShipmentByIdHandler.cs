using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Shipments.Queries;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Handlers.QueryHandlers;
public sealed class GetShipmentByIdHandler(IShipmentRepository repository, RequestContext context) : IRequestHandler<GetShipmentByIdQuery, Response<JsonObject>>
{
    public async Task<Response<JsonObject>> Handle(GetShipmentByIdQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var detail = await repository.GetDetailAsync(context.Scope, request.ShipmentId, ct);
        if (detail is null) return Response<JsonObject>.Fail(ContractErrorCodes.ShipmentNotFound, 404);
        // C-02 (2026-10-03): SHIPMENT_ROOT_INVALID is not declared anywhere in SHIPMENT-BUNDLE (only
        // RETURN_SHIPMENT_ROOT_INVALID is, at 502), and the contract declares INTERNAL_ERROR as the only code for
        // HTTP 500. An undeclared code cannot be handled by any consumer. Pack §274 freezes codes in the contract.
        if (detail.RootState == RawRootState.InvalidStoredValue)
            return Response<JsonObject>.Fail(ContractErrorCodes.InternalError, 500);
        return Response<JsonObject>.Success(ShipmentProjection.Detail(detail));
    }
}
