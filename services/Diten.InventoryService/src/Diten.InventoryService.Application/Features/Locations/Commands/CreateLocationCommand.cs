using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.Locations.Commands;

/// <summary>
/// POST /api/locations (v1.1 <c>createLocation</c>). Raw request values; the handler owns parsing. There is no
/// LegalEntityId member: a Site takes the scope company, everything below inherits its parent's (K-03).
/// </summary>
public sealed record CreateLocationCommand : IRequest<Response<LocationDetailDto>>
{
    public string? HeaderIdempotencyKey { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? Code { get; init; }
    public string? Name { get; init; }
    public string? Type { get; init; }
    public string? ParentId { get; init; }
    public bool? IsStockable { get; init; }
    public string? OperatingMode { get; init; }
    public string? OrgUnitRef { get; init; }
    public string? CostCenterCode { get; init; }
}
