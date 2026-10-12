using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations.Queries;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Handlers.QueryHandlers;

/// <summary>
/// Frozen v1 <c>listLocations</c>: every status, unpaged (ASSUMPTION-LOC-08), ordered by code. The frozen
/// <c>legalEntityId</c> filter is honoured only when it names the resolved scope company; any other value → 403
/// <c>CROSS_LE_FORBIDDEN</c> (ASSUMPTION-LOC-06). A malformed <c>type</c> / <c>parentId</c> → 400.
/// </summary>
public sealed class ListLocationsHandler : IRequestHandler<ListLocationsQuery, Response<LocationListDto>>
{
    private readonly ILocationRepository _locations;
    private readonly ITenantContext _tenant;

    public ListLocationsHandler(ILocationRepository locations, ITenantContext tenant)
    {
        _locations = locations;
        _tenant = tenant;
    }

    public async Task<Response<LocationListDto>> Handle(ListLocationsQuery request, CancellationToken cancellationToken)
    {
        LocationType? type = null;
        if (request.Type is not null)
        {
            if (!LocationWriteSupport.TryParseType(request.Type, out var parsed))
                return LocationWriteSupport.Invalid<LocationListDto>("type must be Site, Warehouse, StorageLocation, Zone or Bin.");
            type = parsed;
        }

        Guid? parentId = null;
        if (request.ParentId is not null)
        {
            if (!LocationWriteSupport.TryParseId(request.ParentId, out var parsed))
                return LocationWriteSupport.Invalid<LocationListDto>("parentId must be a UUID.");
            parentId = parsed;
        }

        if (request.LegalEntityId is not null
            && !(LocationWriteSupport.TryParseId(request.LegalEntityId, out var legalEntity) && legalEntity == _tenant.LegalEntityId))
        {
            return LocationWriteSupport.Fail<LocationListDto>(LocationErrorCodes.CrossLeForbidden,
                "legalEntityId may only name the selected company.", 403);
        }

        var items = await _locations.ListAsync(type, parentId, cancellationToken);
        return Response<LocationListDto>.Success(new LocationListDto
        {
            Items = items.Select(l => LocationMapping.ToRef(l)).ToList(),
            Total = items.Count
        });
    }
}
