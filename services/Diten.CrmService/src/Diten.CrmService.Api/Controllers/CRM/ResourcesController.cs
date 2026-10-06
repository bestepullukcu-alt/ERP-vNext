using Diten.CrmService.Application.Features.Resources;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// WP-MOB-B02 — the caller's own resource mapping. Shares the <c>api/crm/resources</c> base with
/// <see cref="TerritoryResourcesController"/> (gateway <c>/api/crm/resources/{everything}</c> already covers it).
/// No HasPermission: it only ever returns the caller's own identity, so there is nothing to escalate to; [Authorize]
/// alone makes anonymous → 401.
/// </summary>
[Authorize]
[Route("api/crm/resources")]
public sealed class ResourcesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ResourcesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Interim user-as-resource: <c>{ items: [ { resourceId: &lt;sub&gt;, resourceType: "user", status: "active" } ] }</c>.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetMyResourcesQuery(MyResourceIdentity.ResolveUserId(User)), cancellationToken));
}
