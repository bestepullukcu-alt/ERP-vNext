using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Path;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Application.Features.Knowledge.Path.Queries;
using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.Path.KnowledgePathPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU04 — KnowledgePath authoring with EMBEDDED steps (D2). Canonical under <c>/api/crm/knowledge/paths</c>;
/// steps are the path's sub-resource (<c>/paths/{id}/steps…</c>) — there is no flat <c>/path-steps</c> family and no
/// DELETE/PATCH (closing is archive). Publish is a separate endpoint + permission (D4, SoD). Under the documented
/// DEV-ONLY fallback publish collapses onto manage; the canonical <c>crm.knowledge.path.publish</c> is defined but not
/// seeded (F-RBAC).
/// </summary>
[Authorize]
public sealed class KnowledgePathsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public KnowledgePathsController(IMediator mediator) => _mediator = mediator;

    // ---------------- paths ----------------

    [HttpGet("api/crm/knowledge/paths")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? subjectId,
        [FromQuery] Guid? topicId,
        [FromQuery] Guid? audienceProfileId,
        [FromQuery] string? language,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? pathCode,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListKnowledgePathsQuery(
                subjectId, topicId, audienceProfileId, language, status, effectiveAt, pathCode, search,
                includeArchived),
            cancellationToken));

    [HttpGet("api/crm/knowledge/paths/{pathId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Get(
        Guid pathId, [FromQuery] DateTimeOffset? effectiveAt, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetKnowledgePathQuery(pathId, effectiveAt), cancellationToken));

    [HttpPost("api/crm/knowledge/paths")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Create(
        [FromBody] CreateKnowledgePathRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateKnowledgePathCommand(
                request.PathCode, request.PathName, request.SubjectId, request.Objective, request.PathVersion,
                request.EffectiveFrom, request.Description, request.TopicId, request.AudienceProfileId,
                request.LanguageCode, request.PathStatus, request.EffectiveTo, request.Source,
                ChainTemplateId: request.ChainTemplateId, CountryCode: request.CountryCode),
            cancellationToken));

    [HttpPut("api/crm/knowledge/paths/{pathId:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Update(
        Guid pathId, [FromBody] UpdateKnowledgePathRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateKnowledgePathCommand(
                pathId, request.PathName, request.SubjectId, request.Objective, request.PathVersion,
                request.EffectiveFrom, request.Description, request.TopicId, request.AudienceProfileId,
                request.LanguageCode, request.PathStatus, request.EffectiveTo, request.Source,
                StepsProvided: request.Steps is not null, request.ExpectedVersion, request.CountryCode),
            cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/publish")]
    [HasPermission(Perms.Publish)] // canonical crm.knowledge.path.publish (WP-SCMM-05-S1)
    public async Task<IActionResult> Publish(
        Guid pathId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new PublishKnowledgePathCommand(pathId, expectedVersion), cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/new-version")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> NewVersion(
        Guid pathId, [FromBody] CreateKnowledgePathVersionRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateKnowledgePathVersionCommand(pathId, request?.NewPathVersion), cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/archive")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> Archive(
        Guid pathId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveKnowledgePathCommand(pathId, expectedVersion), cancellationToken));

    // ---------------- embedded steps (sub-resource of a path) ----------------

    [HttpGet("api/crm/knowledge/paths/{pathId:guid}/steps")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> ListSteps(
        Guid pathId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] DateTimeOffset? effectiveAt = null,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetKnowledgePathStepsQuery(pathId, includeArchived, effectiveAt), cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/steps")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> AddStep(
        Guid pathId, [FromBody] AddKnowledgePathStepRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new AddKnowledgePathStepCommand(
                pathId, request.StepOrder, request.StepCode, request.StepTitle, request.StepType, request.ContentId,
                request.IsRequired, request.VersionPinPolicy, request.CompletionRule, request.PrerequisiteStepId,
                request.ConceptNodeId, request.EstimatedDurationMinutes, request.Notes,
                MapBranch(request.BranchConditions), request.ExpectedVersion, MapArrangement(request.Arrangement)),
            cancellationToken));

    [HttpPut("api/crm/knowledge/paths/{pathId:guid}/steps/{stepId:guid}")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> UpdateStep(
        Guid pathId, Guid stepId, [FromBody] UpdateKnowledgePathStepRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateKnowledgePathStepCommand(
                pathId, stepId, request.StepOrder, request.StepCode, request.StepTitle, request.StepType,
                request.ContentId, request.IsRequired, request.VersionPinPolicy, request.CompletionRule,
                request.PrerequisiteStepId, request.ConceptNodeId, request.EstimatedDurationMinutes, request.Notes,
                MapBranch(request.BranchConditions), request.ExpectedVersion, MapArrangement(request.Arrangement)),
            cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/steps/{stepId:guid}/archive")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> ArchiveStep(
        Guid pathId, Guid stepId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveKnowledgePathStepCommand(pathId, stepId, expectedVersion), cancellationToken));

    // ---------------- WP-KP-1 studio: chain binding + claims (sub-resources of a path) ----------------

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/bind-chain")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> BindChain(
        Guid pathId, [FromBody] BindKnowledgePathChainRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new BindKnowledgePathChainCommand(
                pathId, request.ChainTemplateId, request.CountryCode, request.LanguageCode, request.ExpectedVersion),
            cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/claims")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> AddClaim(
        Guid pathId, [FromBody] AddKnowledgePathClaimRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new AddKnowledgePathClaimCommand(
                pathId, request.ClaimId, MapArrangement(request.Arrangement), request.ExpectedVersion),
            cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/claims/{claimId:guid}/arrange")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> ArrangeClaim(
        Guid pathId, Guid claimId, [FromBody] ArrangeKnowledgePathClaimRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArrangeKnowledgePathClaimCommand(pathId, claimId, request.Position, request.ExpectedVersion),
            cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/claims/{claimId:guid}/remove")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> RemoveClaim(
        Guid pathId, Guid claimId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new RemoveKnowledgePathClaimCommand(pathId, claimId, expectedVersion), cancellationToken));

    // ---------------- WP-KP-2 review: revision + MLR round (one channel), notes, history ----------------

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/submit-review")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> SubmitReview(Guid pathId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new SubmitKnowledgePathReviewCommand(pathId), cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/withdraw-review")]
    [HasPermission(Perms.Manage)]
    public async Task<IActionResult> WithdrawReview(Guid pathId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new WithdrawKnowledgePathReviewCommand(pathId), cancellationToken));

    // A reviewer needs only read here: MOD-0023 decides who may act on the task (its candidates + SoD).
    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/revisions/{revisionId:guid}/decision")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> Decide(
        Guid pathId, Guid revisionId, [FromBody] KnowledgePathDecisionRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new DecideKnowledgePathRevisionCommand(pathId, revisionId, request.Decision, request.Comment), cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/revisions/{revisionId:guid}/notes")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> AddNote(
        Guid pathId, Guid revisionId, [FromBody] KnowledgePathNoteRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new AddKnowledgePathRevisionNoteCommand(pathId, revisionId, request.PageRef, request.BlockRef, request.StepRef,
                request.X, request.Y, request.Text), cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/revisions/{revisionId:guid}/notes/{noteId:guid}/resolve")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> ResolveNote(Guid pathId, Guid revisionId, Guid noteId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ResolveKnowledgePathRevisionNoteCommand(pathId, revisionId, noteId,
                PermissionClaims.HasPermission(User, Perms.Manage) || PermissionClaims.HasPermission(User, Perms.ManageFallback)),
            cancellationToken));

    [HttpGet("api/crm/knowledge/paths/{pathId:guid}/revisions")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> ListRevisions(Guid pathId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new ListKnowledgePathRevisionsQuery(pathId), cancellationToken));

    [HttpGet("api/crm/knowledge/paths/{pathId:guid}/revisions/{revisionId:guid}")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> GetRevision(Guid pathId, Guid revisionId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetKnowledgePathRevisionQuery(pathId, revisionId), cancellationToken));

    [HttpGet("api/crm/knowledge/paths/{pathId:guid}/review-history")]
    [HasPermission(Perms.Read)]
    public async Task<IActionResult> ReviewHistory(Guid pathId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetKnowledgePathReviewHistoryQuery(pathId), cancellationToken));

    private static KnowledgePathArrangementInput? MapArrangement(KnowledgePathArrangementRequest? arrangement)
        => arrangement is null
            ? null
            : new KnowledgePathArrangementInput(arrangement.ChainStepId, arrangement.BranchCode, arrangement.Position);

    private static IReadOnlyList<KnowledgePathBranchConditionInput>? MapBranch(
        IReadOnlyList<KnowledgePathBranchConditionRequest>? conditions)
        => conditions?
            .Select(c => new KnowledgePathBranchConditionInput(c.ConditionCode, c.Description, c.TargetStepId))
            .ToList();
}
