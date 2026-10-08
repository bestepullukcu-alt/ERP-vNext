using System.Security.Claims;
using System.Text.Json.Serialization;
using Diten.PlanningService.Application.Features.DemandPlanning.RevisionCommands;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

// Owned Demand v2 candidate routes. Production publish policy remains hard-deny.
[Route("api/v2/demand/revisions")]
public sealed class RevisionCommandController(ISender sender) : CustomBaseController
{
    [HttpPost("{revisionId:guid}/rollback-drafts")]
    [HasPermission("demand.drafts.create")]
    public async Task<IActionResult> CreateRollbackDraft(Guid revisionId,
        [FromBody] RollbackDemandRevisionRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var response = await sender.Send(new RollbackDemandRevisionCommand(
            tenantId, actorId, legalEntityHint ?? Guid.Empty, revisionId,
            User.HasClaim("permission", "demand.drafts.create"), request.Reason,
            idempotencyKey ?? string.Empty, request.ExpectedSourceStateVersion),
            cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{revisionId:guid}/publish")]
    [HasPermission("demand.plans.publish")]
    public async Task<IActionResult> Publish(Guid revisionId,
        [FromBody] PublishDemandRevisionRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var response = await sender.Send(new PublishDemandRevisionCommand(
            tenantId, actorId, legalEntityHint ?? Guid.Empty, revisionId,
            idempotencyKey ?? string.Empty, request.ExpectedContentVersion,
            request.ExpectedStateVersion), cancellationToken);
        return CreateActionResultInstance(response);
    }

    [HttpPost("{revisionId:guid}/invalidate")]
    [HasPermission("demand.plans.invalidate")]
    public async Task<IActionResult> Invalidate(Guid revisionId,
        [FromBody] InvalidateDemandRevisionRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var response = await sender.Send(new InvalidateDemandRevisionCommand(
            tenantId, actorId, legalEntityHint ?? Guid.Empty, revisionId,
            idempotencyKey ?? string.Empty, request.ExpectedContentVersion,
            request.ExpectedStateVersion, request.ImpactCode,
            request.Reason, request.EvidenceReference), cancellationToken);
        return CreateActionResultInstance(response);
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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PublishDemandRevisionRequest(int ExpectedContentVersion,
    int ExpectedStateVersion);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record InvalidateDemandRevisionRequest(int ExpectedContentVersion,
    int ExpectedStateVersion, string ImpactCode, string Reason,
    string EvidenceReference);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RollbackDemandRevisionRequest(string Reason,
    int ExpectedSourceStateVersion);
