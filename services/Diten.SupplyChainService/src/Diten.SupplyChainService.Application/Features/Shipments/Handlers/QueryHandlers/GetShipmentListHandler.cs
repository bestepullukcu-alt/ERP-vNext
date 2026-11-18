using MediatR;
using System.Text.Json.Nodes;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Shipments.Queries;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Handlers.QueryHandlers;
public sealed class GetShipmentListHandler(IShipmentRepository repository, RequestContext context) : IRequestHandler<GetShipmentListQuery, Response<JsonObject>>
{
    public async Task<Response<JsonObject>> Handle(GetShipmentListQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ShipmentStatus? status = request.Status is null ? null : Enum.Parse<ShipmentStatus>(request.Status);
        ShipmentSortColumn? orderBy = request.OrderBy is null ? null : ShipmentListOrder.Columns[request.OrderBy];
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var page = await repository.QueryAsync(context.Scope,
            new ShipmentListRequest(status, request.SourceDocumentId, search, orderBy, request.OrderDir == "desc", request.Page, request.PageSize), ct);
        return Response<JsonObject>.Success(new JsonObject
        {
            ["items"] = new JsonArray(page.Items.Select(s => (JsonNode)ShipmentProjection.Summary(s)).ToArray()),
            ["page"] = request.Page,
            ["pageSize"] = request.PageSize,
            ["total"] = page.Total,
            ["filteredTotal"] = page.FilteredTotal,
            ["contractVersion"] = "v1"
        });
    }
}
