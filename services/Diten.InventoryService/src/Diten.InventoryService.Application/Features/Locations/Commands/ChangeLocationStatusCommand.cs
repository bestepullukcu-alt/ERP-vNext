using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Locations.Commands;

/// <summary>POST /api/locations/{id}/status (v1.1 <c>changeLocationStatus</c>, <c>If-Match</c> required; K-04…K-06).</summary>
public sealed record ChangeLocationStatusCommand(string? LocationId, string? IfMatch, string? Status)
    : IRequest<Response<LocationDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForLedger("LifecycleTransition", "Location");
    public Guid? CentralAuditEntityId(object? response) => Id(LocationId);
}
