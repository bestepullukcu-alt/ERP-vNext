using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.ContentComposition.ContentScopes;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ContentComposition.ContentScopes.ContentScopePermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) — content-scope READ surface. WP-SB-1R (bridge-decision §7) retired the ContentScope: the
/// create / update / archive endpoints are gone and the set context now lives on the ContentSet (country + language) and
/// derives from the composition template (product + audience). These reads remain (read-only) until the separate
/// clean-up job. Canonical under <c>/api/crm/content-composition/content-scopes</c>.
/// </summary>
[Authorize]
public sealed class ContentScopesController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ContentScopesController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/content-composition/content-scopes")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListContentScopesQuery(status, search, includeArchived), cancellationToken));

    [HttpGet("api/crm/content-composition/content-scopes/{contentScopeId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid contentScopeId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetContentScopeQuery(contentScopeId), cancellationToken));
}
