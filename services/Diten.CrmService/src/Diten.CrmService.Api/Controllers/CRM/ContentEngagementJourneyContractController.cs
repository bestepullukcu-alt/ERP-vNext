using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Contract;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney
    .ContentEngagementJourneyPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU05 — ContentEngagementJourney contract surface (feature flags, in-domain vocabulary, supported filters,
/// limits, permissions, reason codes, limitations). Canonical under
/// <c>/api/crm/knowledge/content-engagement-journey/contract</c>; exposed through the existing <c>knowledge</c> ocelot
/// wildcard. Permissions run on the documented DEV-ONLY fallback until FU05-RBAC lands.
/// </summary>
[Authorize]
public sealed class ContentEngagementJourneyContractController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ContentEngagementJourneyContractController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/knowledge/content-engagement-journey/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> GetContract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetContentEngagementJourneyContractQuery(), cancellationToken));
}
