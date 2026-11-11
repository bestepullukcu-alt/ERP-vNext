using Diten.PlanningService.Api.Features.DemandPlanning;
using Diten.PlanningService.Application.Features.Mrp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.PlanningService.Api.Features.Mrp;

[Authorize]
[Route("api/v1/mrp/calculations")]
public sealed class MrpCalculationController(MrpLiveCalculation calculation) : CustomBaseController
{
    [HttpGet]
    [HasPermission("mrp.runs.read")]
    public async Task<IActionResult> Get([FromQuery] string planningPeriodKey,
        [FromQuery] Guid skuId, [FromQuery] Guid warehouseId, CancellationToken ct)
    {
        var tenant = HttpContext.Items["DemandPlanning.TenantId"] is Guid value ? value : Guid.Empty;
        var company = Guid.TryParse(Request.Headers["X-Legal-Entity-Id"].ToString(), out var parsed)
            ? parsed : Guid.Empty;
        var result = await calculation.CalculateAsync(tenant, company, planningPeriodKey,
            skuId, warehouseId, ct);
        return result.Status switch
        {
            MrpCalculationStatus.Found => Ok(new { data = result.Plan }),
            MrpCalculationStatus.Invalid => BadRequest(),
            MrpCalculationStatus.Forbidden => NotFound(),
            _ => StatusCode(503, new { error = "MRP source verification is unavailable." })
        };
    }
}
