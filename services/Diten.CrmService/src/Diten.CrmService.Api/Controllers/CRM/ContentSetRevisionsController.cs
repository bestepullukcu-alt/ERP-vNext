using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.ContentSetRevisionPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// SCMM-15 (CAND-CAP-0011) — ContentSetRevision READ surface. WP-KP-4 retired the content set: submit, review decision,
/// render, release and withdrawal are gone (the knowledge path revision carries MLR, output and release since WP-KP-2 /
/// KP-3). The list / detail / rendered-artifact reads stay as the audit trail of old revisions. Canonical under
/// <c>/api/crm/content-composition/content-set-revisions</c>.
/// </summary>
[Authorize]
public sealed class ContentSetRevisionsController : CustomBaseController
{
    private const string Base = "api/crm/content-composition/content-set-revisions";
    private const string Retired = "WP-KP-4 — content set revisions are retired (read-only audit trail); use knowledge path revisions.";

    private readonly IMediator _mediator;

    public ContentSetRevisionsController(IMediator mediator) => _mediator = mediator;

    [Obsolete(Retired)]
    [HttpGet(Base)]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List([FromQuery] Guid contentSetId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ListContentSetRevisionsQuery(contentSetId), cancellationToken));

    [Obsolete(Retired)]
    [HttpGet(Base + "/{revisionId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid revisionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetContentSetRevisionByIdQuery(revisionId), cancellationToken));

    // SCMM-16B — the rendered PDF of an old revision. The content id is resolved from the revision (never a client
    // input, so FU01's non-leakage holds); another tenant's revision or an unrendered revision is 404.
    [Obsolete(Retired)]
    [HttpGet(Base + "/{revisionId:guid}/artifact")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Artifact(Guid revisionId, CancellationToken cancellationToken)
    {
        var response = await _mediator.Send(new GetContentSetRevisionArtifactQuery(revisionId), cancellationToken);
        if (!response.IsSuccessful || response.Data is null)
        {
            return CreateActionResultInstance(response);
        }

        return File(response.Data.Content, response.Data.MediaType, response.Data.FileName);
    }
}
