using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.Locations.Queries;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Handlers.QueryHandlers;

/// <summary>v1.1 <c>getLocationHistory</c> (K-11): the node's change rows, newest first, paged. 404 as getLocation.</summary>
public sealed class GetLocationHistoryHandler : IRequestHandler<GetLocationHistoryQuery, Response<LocationHistoryPageDto>>
{
    private readonly ILocationRepository _locations;

    public GetLocationHistoryHandler(ILocationRepository locations) => _locations = locations;

    public async Task<Response<LocationHistoryPageDto>> Handle(GetLocationHistoryQuery request, CancellationToken cancellationToken)
    {
        if (!QueryParameterRules.IsValidPage(request.Page))
            return LocationWriteSupport.Invalid<LocationHistoryPageDto>("page must be an integer >= 1.");
        if (!QueryParameterRules.IsValidPageSize(request.PageSize))
            return LocationWriteSupport.Invalid<LocationHistoryPageDto>($"pageSize must be an integer between 1 and {InventoryContract.MaxPageSize}.");
        if (!LocationWriteSupport.TryParseId(request.LocationId, out var id)) return LocationWriteSupport.NotFound<LocationHistoryPageDto>();
        if (await _locations.FindAsync(id, cancellationToken) is null) return LocationWriteSupport.NotFound<LocationHistoryPageDto>();

        var page = QueryParameterRules.Page(request.Page);
        var pageSize = QueryParameterRules.PageSize(request.PageSize);
        var history = await _locations.HistoryAsync(id, page, pageSize, cancellationToken);
        return Response<LocationHistoryPageDto>.Success(new LocationHistoryPageDto
        {
            Items = history.Items.Select(LocationMapping.ToChange).ToList(),
            Total = history.Total,
            Page = page,
            PageSize = pageSize
        });
    }
}
