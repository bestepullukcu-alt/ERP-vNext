using System.Security.Claims;
using System.Text.Json.Serialization;
using Diten.PlanningService.Application.Features.DemandPlanning.Cycles;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

[Route("api/v2/demand/planning-cycles")]
public sealed class PlanningCyclesController : CustomBaseController
{
    private readonly ISender _sender;
    public PlanningCyclesController(ISender sender) => _sender = sender;

    [HttpPost]
    [HasPermission("demand.cycles.create")]
    public async Task<IActionResult> Create(
        [FromBody] CreatePlanningCycleRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? selectedLegalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (HttpContext.Items["DemandPlanning.TenantId"] is not Guid tenantId ||
            tenantId == Guid.Empty)
            return Forbid();
        var actorClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(actorClaim, out var actorId) || actorId == Guid.Empty)
            return Forbid();
        var result = await _sender.Send(new CreatePlanningCycleCommand(
            tenantId, actorId, selectedLegalEntityHint ?? Guid.Empty,
            request.AsOfDate, request.FirstWeekStart,
            idempotencyKey ?? string.Empty), cancellationToken);
        return CreateActionResultInstance(result);
    }
}

// Unknown fields, including client-supplied planningPeriodKey/Tenant/LegalEntity,
// are rejected by the JSON input formatter rather than silently ignored.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreatePlanningCycleRequest(
    DateOnly AsOfDate, DateOnly FirstWeekStart);

