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
        var s = await repository.GetAsync(context.Scope, request.ShipmentId, ct);
        return s is null ? Response<JsonObject>.Fail("SHIPMENT_NOT_FOUND", 404) : Response<JsonObject>.Success(ShipmentProjection.Detail(s));
    }
}
