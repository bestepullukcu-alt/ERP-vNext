using Diten.CrmService.Application.Features.CyclePeriod.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.CyclePeriod.CyclePeriodPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0165 FU06 contract surface: what this FU can do, what it deliberately cannot, the in-domain vocabulary and the
/// published ceilings.
/// <para>It exists so the period editor never hardcodes a status, a resolution outcome or a length limit — a hardcoded
/// list is a second source of truth, and it drifts silently.</para>
/// </summary>
[Authorize]
public sealed class CyclePeriodContractController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CyclePeriodContractController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/cycle-periods/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetCyclePeriodContractQuery(), cancellationToken));
}
