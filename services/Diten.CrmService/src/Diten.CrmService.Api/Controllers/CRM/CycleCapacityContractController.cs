using Diten.CrmService.Application.Features.CycleCapacity.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.CycleCapacity.CycleCapacityPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0155 FU06 contract surface: what this FU can do, what it deliberately cannot, the in-domain vocabulary, the
/// published ceilings and the configured defaults a new capacity is born with.
/// <para>It exists so the capacity editor never hardcodes a minute ceiling, a resolution name or the eight-hour day —
/// a hardcoded value is a second source of truth, and it drifts silently.</para>
/// </summary>
[Authorize]
public sealed class CycleCapacityContractController : CustomBaseController
{
    private readonly IMediator _mediator;

    public CycleCapacityContractController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/cycle-capacities/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetCycleCapacityContractQuery(), cancellationToken));
}
