using Diten.CrmService.Application.Features.ContentComposition.ContentSets;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ContentComposition.ContentSets.ContentSetPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) — content-set READ surface. WP-KP-4 (DESIGN-KP-STUDIO §7, bridge-decision §8) retired the
/// content set: the Knowledge Path Studio took its job (composition + claims + MLR + output + release), so the authoring
/// endpoints (create / clone / edit / components + claims / apply-eligibility / archive) are gone. These two reads stay
/// as the audit trail of old data (pre-SB-1R documents included — the retired scope binding still parses as
/// <c>LegacyScope</c>) until the separate repository / class-map clean-up. Canonical under
/// <c>/api/crm/content-composition/content-sets</c>.
/// </summary>
[Authorize]
public sealed class ContentSetsController : CustomBaseController
{
    private const string Base = "api/crm/content-composition/content-sets";
    private const string Retired = "WP-KP-4 — content sets are retired (read-only audit trail); use the knowledge path studio.";

    private readonly IMediator _mediator;

    public ContentSetsController(IMediator mediator) => _mediator = mediator;

    [Obsolete(Retired)]
    [HttpGet(Base)]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListContentSetsQuery(status, search, includeArchived), cancellationToken));

    [Obsolete(Retired)]
    [HttpGet(Base + "/{contentSetId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid contentSetId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetContentSetQuery(contentSetId), cancellationToken));
}
