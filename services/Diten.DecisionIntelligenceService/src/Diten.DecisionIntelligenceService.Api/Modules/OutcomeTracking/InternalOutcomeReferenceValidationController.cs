using Diten.DecisionIntelligenceService.Application.Modules.OutcomeTracking.GateI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Diten.DecisionIntelligenceService.Api.Modules.OutcomeTracking;
[ApiController,Authorize,Route("internal/v1/decision-intelligence/outcome-tracking/outcome-references")]
public sealed class InternalOutcomeReferenceValidationController(OutcomeReferenceValidationProvider provider,IOutcomeGateIContextAccessor context):ControllerBase
{
 [HttpPost("validate")]public async Task<IActionResult> Validate([FromBody]OutcomeGateIRequest request,CancellationToken cancellationToken){var result=await provider.ValidateAsync(request,context.Current,cancellationToken);return StatusCode(result.StatusCode,result);}
}
