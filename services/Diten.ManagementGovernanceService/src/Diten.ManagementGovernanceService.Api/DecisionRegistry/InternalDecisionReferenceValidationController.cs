using Diten.ManagementGovernanceService.Application.DecisionRegistry.GateI;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.ManagementGovernanceService.Api.DecisionRegistry;

[ApiController]
[Authorize(AuthenticationSchemes = Fu16AuthenticationOptions.Scheme)]
[Route("internal/v1/decision-registry/decision-references")]
public sealed class InternalDecisionReferenceValidationController(
    DecisionReferenceValidationProvider provider,
    IGateITrustedContextAccessor trustedContext) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(
        [FromBody] DecisionReferenceValidationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await provider.ValidateAsync(request, trustedContext.Current, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
