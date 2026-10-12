using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Commands;

/// <summary>
/// PATCH /api/locations/{id} (v1.1 <c>updateLocation</c>, <c>If-Match</c> required). An absent member leaves the
/// field unchanged; <c>type</c> is not accepted (ASSUMPTION-LOC-04). A <c>parentId</c> change is a move (K-07).
/// </summary>
public sealed record UpdateLocationCommand : IRequest<Response<LocationDetailDto>>
{
    public string? LocationId { get; init; }
    public string? IfMatch { get; init; }
    public Patch<string> Code { get; init; }
    public Patch<string> Name { get; init; }
    public Patch<string> ParentId { get; init; }
    public Patch<bool?> IsStockable { get; init; }
    public Patch<string> OperatingMode { get; init; }
    public Patch<string> OrgUnitRef { get; init; }
    public Patch<string> CostCenterCode { get; init; }
}
