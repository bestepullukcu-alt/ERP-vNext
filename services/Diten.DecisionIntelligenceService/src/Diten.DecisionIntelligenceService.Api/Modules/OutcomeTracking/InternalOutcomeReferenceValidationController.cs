using Diten.DecisionIntelligenceService.Application.Modules.OutcomeTracking.GateI;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Diten.DecisionIntelligenceService.Api.Modules.OutcomeTracking;
[ApiController,Authorize(AuthenticationSchemes=Fu16AuthenticationOptions.Scheme),Route("internal/v1/decision-intelligence/outcome-tracking/outcome-references")]
public sealed class InternalOutcomeReferenceValidationController(OutcomeReferenceValidationProvider provider,IOutcomeGateIContextAccessor context):ControllerBase
{
 [HttpPost("validate")]public async Task<IActionResult> Validate([FromBody]OutcomeGateIRequest request,CancellationToken cancellationToken){var result=await provider.ValidateAsync(request,context.Current,cancellationToken);return StatusCode(result.StatusCode,result);}
}
