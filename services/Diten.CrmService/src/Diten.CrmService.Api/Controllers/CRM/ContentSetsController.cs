using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.ContentComposition.ContentSets;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.ContentComposition.ContentSets.ContentSetPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// SCMM-14 (CAND-CAP-0011) — content-set (assembly draft) authoring HTTP surface. Canonical under
/// <c>/api/crm/content-composition/content-sets</c>. A thin adapter over the ContentSet CQRS — no business logic here.
/// Mutable DRAFT authoring only: create / clone / edit / arrange components + claims / apply a non-blocking eligibility
/// snapshot. There is <b>no delete</b> (Archive), and <b>no freeze / approve / render / release</b> (SCMM-15/16/17).
/// </summary>
[Authorize]
public sealed class ContentSetsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ContentSetsController(IMediator mediator) => _mediator = mediator;

    private const string Base = "api/crm/content-composition/content-sets";

    [HttpGet(Base)]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListContentSetsQuery(status, search, includeArchived), cancellationToken));

    [HttpGet(Base + "/{contentSetId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(Guid contentSetId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetContentSetQuery(contentSetId), cancellationToken));

    [HttpPost(Base)]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContentSetDraftRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateContentSetDraftCommand(
                request.SetCode, request.SetName, request.ConceptChainTemplateId, request.Description,
                request.ContentScopeId),
            cancellationToken));

    [HttpPost(Base + "/{contentSetId:guid}/clone")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Clone(
        Guid contentSetId, [FromBody] CloneContentSetRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CloneContentSetToDraftCommand(contentSetId, request.NewSetCode, request.NewSetName), cancellationToken));

    [HttpPut(Base + "/{contentSetId:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Update(
        Guid contentSetId, [FromBody] UpdateContentSetRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateContentSetCommand(contentSetId, request.SetName, request.Description, request.Status),
            cancellationToken));

    [HttpPost(Base + "/{contentSetId:guid}/archive")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Archive(Guid contentSetId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ArchiveContentSetCommand(contentSetId), cancellationToken));

    // ---- component selection ----

    [HttpPost(Base + "/{contentSetId:guid}/components")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> AddComponent(
        Guid contentSetId, [FromBody] AddContentSetComponentRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new AddContentSetComponentCommand(
                contentSetId, request.KnowledgeContentId, request.TemplateStepId, request.Position, request.BranchId,
                request.Role),
            cancellationToken));

    [HttpPost(Base + "/{contentSetId:guid}/components/{selectionId:guid}/arrange")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> ArrangeComponent(
        Guid contentSetId, Guid selectionId, [FromBody] ArrangeContentSetComponentRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArrangeContentSetComponentCommand(
                contentSetId, selectionId, request.TemplateStepId, request.Position, request.BranchId),
            cancellationToken));

    [HttpPost(Base + "/{contentSetId:guid}/components/{selectionId:guid}/remove")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> RemoveComponent(
        Guid contentSetId, Guid selectionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RemoveContentSetComponentCommand(contentSetId, selectionId), cancellationToken));

    // ---- claim selection ----

    [HttpPost(Base + "/{contentSetId:guid}/claims")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> AddClaim(
        Guid contentSetId, [FromBody] AddContentSetClaimRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new AddContentSetClaimCommand(
                contentSetId, request.ClaimId, request.TemplateStepId, request.Position, request.BranchId),
            cancellationToken));

    [HttpPost(Base + "/{contentSetId:guid}/claims/{selectionId:guid}/arrange")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> ArrangeClaim(
        Guid contentSetId, Guid selectionId, [FromBody] ArrangeContentSetClaimRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArrangeContentSetClaimCommand(
                contentSetId, selectionId, request.TemplateStepId, request.Position, request.BranchId),
            cancellationToken));

    [HttpPost(Base + "/{contentSetId:guid}/claims/{selectionId:guid}/remove")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> RemoveClaim(
        Guid contentSetId, Guid selectionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RemoveContentSetClaimCommand(contentSetId, selectionId), cancellationToken));

    // ---- eligibility (non-blocking validation snapshot) ----

    [HttpPost(Base + "/{contentSetId:guid}/apply-eligibility")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> ApplyEligibility(Guid contentSetId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ApplyContentSetEligibilityCommand(contentSetId), cancellationToken));
}
