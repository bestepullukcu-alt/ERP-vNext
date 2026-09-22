using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ContentComposition.ContentSetRevisions.ContentSetRevisionPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// SCMM-15 (CAND-CAP-0011, DEC-SCMM-04 C3) — ContentSetRevision (frozen manifest + in-domain review) HTTP surface.
/// Canonical under <c>/api/crm/content-composition/content-set-revisions</c> (covered by the existing gateway
/// content-composition route). A thin adapter over the ContentSetRevision CQRS — no business logic here. Submit freezes a
/// draft (manage capability); the review decision is a SEPARATE permission so separation-of-duties is enforced by role as
/// well as by the runtime reviewer≠author guard. There is no render / release / withdraw here (SCMM-16 / SCMM-17).
/// </summary>
[Authorize]
public sealed class ContentSetRevisionsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ContentSetRevisionsController(IMediator mediator) => _mediator = mediator;

    private const string Base = "api/crm/content-composition/content-set-revisions";

    [HttpGet(Base)]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List([FromQuery] Guid contentSetId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ListContentSetRevisionsQuery(contentSetId), cancellationToken));

    [HttpGet(Base + "/{revisionId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid revisionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetContentSetRevisionByIdQuery(revisionId), cancellationToken));

    [HttpPost(Base)]
    [HasPermission(Perms.Submit)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitContentSetRevisionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new SubmitContentSetForReviewCommand(request.ContentSetId, request.ExpectedVersion), cancellationToken));

    [HttpPost(Base + "/{revisionId:guid}/review-decision")]
    [HasPermission(Perms.Review)]
    public async Task<IActionResult> ReviewDecision(
        Guid revisionId, [FromBody] RecordReviewDecisionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RecordReviewDecisionCommand(revisionId, request.Decision, request.Reason), cancellationToken));
}
