using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Locations.Queries;
using Diten.InventoryService.Application.Features.Locations.Services;
using Diten.InventoryService.Domain.Repositories;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Handlers.QueryHandlers;

/// <summary>
/// Frozen v1 <c>validateLocations</c>: one result per requested id, in request order. <c>exists</c> = a location of the
/// caller's company in any status (§15); <c>isStockable</c> only for one that exists. At most
/// <see cref="MaxIds"/> ids per call (400 above).
/// </summary>
public sealed class ValidateLocationsHandler : IRequestHandler<ValidateLocationsQuery, Response<LocationValidationResultDto>>
{
    public const int MaxIds = 1000;

    private readonly ILocationRepository _locations;

    public ValidateLocationsHandler(ILocationRepository locations) => _locations = locations;

    public async Task<Response<LocationValidationResultDto>> Handle(ValidateLocationsQuery request, CancellationToken cancellationToken)
    {
        if (request.LocationIds.Count > MaxIds)
            return LocationWriteSupport.Invalid<LocationValidationResultDto>($"At most {MaxIds} locationIds per call.");

        var ids = request.LocationIds
            .Select(raw => LocationWriteSupport.TryParseId(raw, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var found = (await _locations.FindManyAsync(ids, cancellationToken)).ToDictionary(l => l.Id);

        return Response<LocationValidationResultDto>.Success(new LocationValidationResultDto
        {
            Results = request.LocationIds.Select(raw =>
                    LocationWriteSupport.TryParseId(raw, out var id) && found.TryGetValue(id, out var location)
                        ? new LocationValidationEntryDto { LocationId = raw, Exists = true, IsStockable = location.IsStockable }
                        : new LocationValidationEntryDto { LocationId = raw, Exists = false })
                .ToList()
        });
    }
}
