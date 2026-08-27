using Diten.FpaService.Application.Modules.Budgeting.Contracts;
using Diten.FpaService.Application.Modules.Budgeting.GateI;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.FpaService.Api.Modules.Budgeting;
[ApiController, Authorize(AuthenticationSchemes = Fu16AuthenticationOptions.Scheme)]
[Route("internal/v1/fpa/budgeting/budget-version-references")]
public sealed class InternalBudgetVersionValidationController(BudgetVersionReferenceValidationProvider provider,
    IBudgetGateITrustedContextAccessor context) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] BudgetGateIRequest request, CancellationToken cancellationToken)
    {
        var result = await provider.ValidateAsync(request, context.Current, cancellationToken);
        return StatusCode(result.StatusCode, result);
    }
}
