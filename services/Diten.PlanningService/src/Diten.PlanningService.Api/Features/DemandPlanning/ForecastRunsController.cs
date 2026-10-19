using System.Security.Claims;
using System.Text.Json.Serialization;
using Diten.PlanningService.Application.Features.DemandPlanning.ForecastRuns;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PlanningService.Api.Features.DemandPlanning;

// Demand v2 remains an owned draft. The production authority adapter fails closed.
[Route("api/v2/demand/forecast-runs")]
public sealed class ForecastRunsController(ISender sender) : CustomBaseController
{
    [HttpGet("evaluation")]
    [HasPermission("demand.forecast-runs.create")]
    public async Task<IActionResult> Evaluate([FromQuery] Guid planningCycleId,
        [FromQuery] Guid skuId, [FromQuery] string warehouseId,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new EvaluateForecastQuery(tenantId, actorId,
            legalEntityHint ?? Guid.Empty,
            User.HasClaim("permission", "demand.forecast-runs.create"),
            planningCycleId, skuId, warehouseId), cancellationToken);
        return CreateActionResultInstance(result);
    }

    [HttpPost]
    [HasPermission("demand.forecast-runs.create")]
    public async Task<IActionResult> Create([FromBody] CreateForecastRunRequest request,
        [FromHeader(Name = "X-Legal-Entity-Id")] Guid? legalEntityHint,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var tenantId, out var actorId)) return Forbid();
        var result = await sender.Send(new CreateForecastRunCommand(tenantId,
            actorId, legalEntityHint ?? Guid.Empty,
            User.HasClaim("permission", "demand.forecast-runs.create"),
            User.HasClaim("permission", "demand.drafts.create"),
            request.PlanningCycleId, request.SkuId, request.WarehouseId,
            request.Method, request.Reason, request.ExpectedCycleVersion,
            idempotencyKey ?? string.Empty), cancellationToken);
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

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateForecastRunRequest(Guid PlanningCycleId, Guid SkuId,
    string WarehouseId, string Method, string Reason, int ExpectedCycleVersion);
