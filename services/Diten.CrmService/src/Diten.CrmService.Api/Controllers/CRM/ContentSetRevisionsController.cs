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

    // SCMM-16B — render an approved revision to a PDF, store it through MOD-0262-FU01, and bind the pointer. Idempotent
    // (a re-render returns the existing artifact). The response carries the content id / checksum, never the object key.
    [HttpPost(Base + "/{revisionId:guid}/render")]
    [HasPermission(Perms.Render)]
    public async Task<IActionResult> Render(Guid revisionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RenderContentSetRevisionCommand(revisionId), cancellationToken));

    // SCMM-16B — stream the rendered PDF. The content id is resolved from the revision (never a client input, so FU01's
    // non-leakage holds); another tenant's revision or an unrendered revision is 404.
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

    // SCMM-17 — release a rendered revision's artifact (manifest-bound). Separation of duties: the reviewer cannot release
    // (403). Idempotent; a withdrawn revision cannot be re-released (409).
    [HttpPost(Base + "/{revisionId:guid}/release")]
    [HasPermission(Perms.Release)]
    public async Task<IActionResult> Release(Guid revisionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ReleaseContentSetRevisionCommand(revisionId), cancellationToken));

    // SCMM-17 — managed withdrawal of a released revision (a state change, never a deletion of the stored bytes). The
    // reason is required; withdrawal is terminal.
    [HttpPost(Base + "/{revisionId:guid}/withdraw")]
    [HasPermission(Perms.Withdraw)]
    public async Task<IActionResult> Withdraw(
        Guid revisionId, [FromBody] WithdrawContentSetRevisionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new WithdrawContentSetRevisionCommand(revisionId, request.Reason), cancellationToken));
}
