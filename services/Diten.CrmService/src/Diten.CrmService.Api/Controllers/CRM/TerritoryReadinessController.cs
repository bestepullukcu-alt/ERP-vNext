using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.Readiness;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0151 FU09A read-only readiness/candidate-input surface. These GET endpoints never create a route, visit plan,
/// frequency policy or workflow action. ModelRead is the documented fallback until assignment/resource read keys are
/// available in the central RBAC catalog; this controller seeds or grants nothing.
/// </summary>
[Authorize]
[Route("api/crm/territory-readiness")]
public sealed class TerritoryReadinessController : CustomBaseController
{
    private readonly IMediator _mediator;
    public TerritoryReadinessController(IMediator mediator) => _mediator = mediator;

    [HttpGet("accounts/{accountId:guid}/coverage-readiness")]
    [HttpGet("/api/crm/territory-management/readiness/accounts/{accountId:guid}/coverage-readiness")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> Account(
        Guid accountId, [FromQuery] DateTimeOffset? effectiveAt, [FromQuery] string? businessUnit,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetAccountCoverageReadinessQuery(accountId, effectiveAt, businessUnit), cancellationToken));

    [HttpGet("nodes/{nodeId:guid}/coverage-accounts")]
    [HttpGet("/api/crm/territory-management/readiness/nodes/{nodeId:guid}/coverage-accounts")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> Node(
        Guid nodeId, [FromQuery] DateTimeOffset? effectiveAt, [FromQuery] string? businessUnit,
        [FromQuery] bool includeNonReady = true, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetNodeCoverageAccountsQuery(nodeId, effectiveAt, businessUnit, includeNonReady), cancellationToken));

    [HttpGet("resources/{resourceId}/coverage-readiness")]
    [HttpGet("/api/crm/territory-management/readiness/resources/{resourceId}/coverage-readiness")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> Resource(
        string resourceId, [FromQuery] DateTimeOffset? effectiveAt, [FromQuery] string? businessUnit,
        [FromQuery] bool includeNonReady = true, CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetResourceCoverageReadinessQuery(resourceId, effectiveAt, businessUnit, includeNonReady), cancellationToken));

    [HttpGet("contacts/{contactId:guid}/territory-coverage")]
    [HttpGet("/api/crm/territory-management/readiness/contacts/{contactId:guid}/territory-coverage")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> Contact(
        Guid contactId, [FromQuery] DateTimeOffset? effectiveAt, [FromQuery] string? businessUnit,
        [FromQuery] string? date, [FromQuery] string? weekday, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetContactTerritoryCoverageQuery(contactId, effectiveAt, businessUnit, date, weekday), cancellationToken));

    [HttpGet("route-candidates")]
    [HttpGet("/api/crm/territory-management/readiness/route-candidates")]
    [HasPermission(TerritoryPermissions.ModelRead)]
    public async Task<IActionResult> RouteCandidates(
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? businessUnit,
        [FromQuery] Guid? territoryModelId,
        [FromQuery] Guid? territoryNodeId,
        [FromQuery] string? resourceId,
        [FromQuery] Guid? accountId,
        [FromQuery] Guid? contactId,
        [FromQuery] string? date,
        [FromQuery] string? weekday,
        [FromQuery] bool includeNonReady = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(new GetRouteCandidatesQuery(
            effectiveAt, businessUnit, territoryModelId, territoryNodeId, resourceId, accountId, contactId,
            date, weekday, includeNonReady), cancellationToken));
}
