using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.PlanVsCurrent;
using Diten.CrmService.Application.Features.Territory.ResourceAssignments;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

[Authorize]
[Route("api/crm/resources")]
public sealed class TerritoryResourcesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TerritoryResourcesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("{resourceId}/territory-responsibilities")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> Responsibilities(
        string resourceId, [FromQuery] DateTimeOffset? effectiveAt, [FromQuery] bool includeHistory,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetResourceTerritoryResponsibilitiesQuery(resourceId, effectiveAt, includeHistory), cancellationToken));

    /// <summary>FU04B person-level view: where this resource was PLANNED vs where it is CURRENT. Read-only.</summary>
    [HttpGet("{resourceId}/resource-assignment-plan-vs-current")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> PlanVsCurrent(
        string resourceId, [FromQuery] DateTimeOffset? effectiveAt, [FromQuery] Guid? territoryNodeId,
        [FromQuery] string? businessUnit, [FromQuery] string? positionCode, [FromQuery] string? diffType,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetResourcePlanVsCurrentQuery(resourceId, effectiveAt, territoryNodeId, businessUnit, positionCode, diffType),
            cancellationToken));
}
