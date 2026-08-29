using Diten.CrmService.Application.Features.StrategyTemplate.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.StrategyTemplate.StrategyTemplatePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0167 FU04 contract surface: what this FU can do, what it deliberately cannot, the in-domain vocabulary (plus the
/// MOD-0165 frequency values a declared intent is validated against) and the published ceilings.
/// <para>It exists so the template editor never hardcodes a status, a mode or a frequency value — a hardcoded list is a
/// second source of truth, and it drifts silently.</para>
/// </summary>
[Authorize]
public sealed class StrategyTemplateContractController : CustomBaseController
{
    private readonly IMediator _mediator;

    public StrategyTemplateContractController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/strategy-templates/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(
            await _mediator.Send(new GetStrategyTemplateContractQuery(), cancellationToken));
}
