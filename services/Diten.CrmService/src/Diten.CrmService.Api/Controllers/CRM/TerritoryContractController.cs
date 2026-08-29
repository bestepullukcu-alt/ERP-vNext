using Diten.CrmService.Application.Features.Territory;
using Diten.CrmService.Application.Features.Territory.Contract;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0151 Territory Management contract surface (FU01). Reports bundle version, feature flags (only model+node
/// are true), required MOD-0048 reference-set readiness and the FU01 limitations. Gateway-only; browser never hits 5061.
/// </summary>
[Authorize]
[Route("api/crm/territory-management")]
public sealed class TerritoryContractController : CustomBaseController
{
    private readonly IMediator _mediator;

    public TerritoryContractController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("contract")]
    [HasPermission(TerritoryPermissions.Read)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetTerritoryContractQuery(), cancellationToken));
}
