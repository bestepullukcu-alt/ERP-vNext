using System.Security.Claims;
using Diten.PlanningService.Application.Features.DemandPlanning;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

// Candidate Demand v2 producer routes; no Gateway route or central freeze.
[Route("api/v2/demand/revisions")]
public sealed class RevisionReadController(ISender sender) : CustomBaseController
{
    [HttpGet("current")]
    [HasPermission("demand.plans.consume")]
    public async Task<IActionResult> Current([FromQuery] string planningPeriodKey,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new GetCurrentPublishedRevisionQuery(
            tenantId, legalEntityHint ?? Guid.Empty, planningPeriodKey, actorId),
            cancellationToken);
        return CreateActionResultInstance(result);
    }

    [HttpGet("{revisionId:guid}/status")]
    [HasPermission("demand.plans.consume")]
    public async Task<IActionResult> Status(Guid revisionId,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new GetDemandRevisionStatusQuery(
            tenantId, legalEntityHint ?? Guid.Empty, revisionId, actorId),
            cancellationToken);
        return CreateActionResultInstance(result);
    }

    [HttpGet("{revisionId:guid}/manifest")]
    [HasPermission("demand.plans.consume")]
    public async Task<IActionResult> Manifest(Guid revisionId,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new GetDemandRevisionManifestQuery(
            tenantId, legalEntityHint ?? Guid.Empty, revisionId, actorId),
            cancellationToken);
        return CreateActionResultInstance(result);
    }

    [HttpGet("{revisionId:guid}/rows")]
    [HasPermission("demand.plans.consume")]
    public async Task<IActionResult> Rows(Guid revisionId,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromQuery] int? pageSize, [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new GetDemandRevisionRowsQuery(
            tenantId, legalEntityHint ?? Guid.Empty, revisionId, actorId,
            pageSize ?? 200, cursor), cancellationToken);
        return CreateActionResultInstance(result);
    }

    [HttpGet("{revisionId:guid}/audit-snapshot")]
    [HasPermission("demand.audit.read")]
    public async Task<IActionResult> AuditSnapshot(Guid revisionId,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new GetInvalidatedAuditSnapshotQuery(
            tenantId, legalEntityHint ?? Guid.Empty, revisionId, actorId),
            cancellationToken);
        return CreateActionResultInstance(result);
    }

    [HttpGet("{revisionId:guid}/audit-rows")]
    [HasPermission("demand.audit.read")]
    public async Task<IActionResult> AuditRows(Guid revisionId,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromQuery] int? pageSize, [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new GetInvalidatedAuditRowsQuery(
            tenantId, legalEntityHint ?? Guid.Empty, revisionId, actorId,
            pageSize ?? 100, cursor), cancellationToken);
        return CreateActionResultInstance(result);
    }

    private bool TryGetActor(out Guid tenantId, out Guid actorId)
    {
        tenantId = HttpContext.Items["DemandPlanning.TenantId"] is Guid value
            ? value : Guid.Empty;
        actorId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");
        return tenantId != Guid.Empty && Guid.TryParse(claim, out actorId) &&
            actorId != Guid.Empty;
    }
}
