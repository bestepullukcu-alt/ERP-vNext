using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Inventory.Services;
using Diten.InventoryService.Application.Features.Locations.Queries;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Handlers.QueryHandlers;

/// <summary>
/// v1.1 <c>searchLocations</c> (the L-2 list): paged (<c>pageSize</c> ≤ 200), free-text on code / name, repeatable
/// <c>type</c> / <c>status</c>, <c>isStockable</c>, <c>parentId</c>, ordered by code | name | type | status. Any
/// malformed parameter → 400 <c>INVALID_REQUEST</c>.
/// </summary>
public sealed class SearchLocationsHandler : IRequestHandler<SearchLocationsQuery, Response<LocationSearchPageDto>>
{
    public const int MaxSearchLength = 100;

    private readonly ILocationRepository _locations;

    public SearchLocationsHandler(ILocationRepository locations) => _locations = locations;

    public async Task<Response<LocationSearchPageDto>> Handle(SearchLocationsQuery request, CancellationToken cancellationToken)
    {
        if (!QueryParameterRules.IsValidPage(request.Page))
            return LocationWriteSupport.Invalid<LocationSearchPageDto>("page must be an integer >= 1.");
        if (!QueryParameterRules.IsValidPageSize(request.PageSize))
            return LocationWriteSupport.Invalid<LocationSearchPageDto>($"pageSize must be an integer between 1 and {InventoryContract.MaxPageSize}.");

        var types = new List<LocationType>();
        foreach (var raw in request.Types)
        {
            if (!LocationWriteSupport.TryParseType(raw, out var type))
                return LocationWriteSupport.Invalid<LocationSearchPageDto>("type must be Site, Warehouse, StorageLocation, Zone or Bin.");
            types.Add(type);
        }

        var statuses = new List<LocationStatus>();
        foreach (var raw in request.Statuses)
        {
            if (!LocationWriteSupport.TryParseStatus(raw, out var status))
                return LocationWriteSupport.Invalid<LocationSearchPageDto>("status must be Active, Inactive or Archived.");
            statuses.Add(status);
        }

        bool? stockable = request.IsStockable switch
        {
            null => null,
            "true" => true,
            "false" => false,
            _ => throw new InvalidOperationException("unreachable")
        };

        Guid? parentId = null;
        if (request.ParentId is not null)
        {
            if (!LocationWriteSupport.TryParseId(request.ParentId, out var parsed))
                return LocationWriteSupport.Invalid<LocationSearchPageDto>("parentId must be a UUID.");
            parentId = parsed;
        }

        var orderBy = request.OrderBy switch
        {
            null or "code" => LocationSortField.Code,
            "name" => LocationSortField.Name,
            "type" => LocationSortField.Type,
            "status" => LocationSortField.Status,
            _ => (LocationSortField?)null
        };
        if (orderBy is null)
            return LocationWriteSupport.Invalid<LocationSearchPageDto>("orderBy must be code, name, type or status.");

        var page = QueryParameterRules.Page(request.Page);
        var pageSize = QueryParameterRules.PageSize(request.PageSize);
        var result = await _locations.SearchAsync(new LocationSearch(page, pageSize, request.Search, types, statuses, stockable, parentId,
            orderBy.Value, request.OrderDir == "desc"), cancellationToken);

        return Response<LocationSearchPageDto>.Success(new LocationSearchPageDto
        {
            Items = result.Items.Select(l => LocationMapping.ToRef(l)).ToList(),
            Total = result.Total,
            Page = page,
            PageSize = pageSize
        });
    }
}
