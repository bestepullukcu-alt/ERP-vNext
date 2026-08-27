using Diten.FpaService.Application.Modules.ScenarioPlanning.GateI;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Diten.FpaService.Api.Modules.ScenarioPlanning;
[ApiController,Authorize(AuthenticationSchemes=Fu16AuthenticationOptions.Scheme),Route("internal/v1/fpa/scenario-planning/references")]
public sealed class InternalScenarioReferenceValidationController(ScenarioReferenceValidationProvider provider,IScenarioGateIContextAccessor context):ControllerBase
{
 [HttpPost("validate")]public async Task<IActionResult> Validate([FromBody]ScenarioGateIRequest request,CancellationToken cancellationToken){var result=await provider.ValidateAsync(request,context.Current,cancellationToken);return StatusCode(result.StatusCode,result);}
}
