using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.Path;
using Diten.CrmService.Application.Features.Knowledge.Path.Commands;
using Diten.CrmService.Application.Features.Knowledge.Path.Queries;
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
    [HasPermission(Perms.ReadFallback)]
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
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(
        Guid pathId, [FromQuery] DateTimeOffset? effectiveAt, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetKnowledgePathQuery(pathId, effectiveAt), cancellationToken));

    [HttpPost("api/crm/knowledge/paths")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateKnowledgePathRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateKnowledgePathCommand(
                request.PathCode, request.PathName, request.SubjectId, request.Objective, request.PathVersion,
                request.EffectiveFrom, request.Description, request.TopicId, request.AudienceProfileId,
                request.LanguageCode, request.PathStatus, request.EffectiveTo, request.Source),
            cancellationToken));

    [HttpPut("api/crm/knowledge/paths/{pathId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid pathId, [FromBody] UpdateKnowledgePathRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateKnowledgePathCommand(
                pathId, request.PathName, request.SubjectId, request.Objective, request.PathVersion,
                request.EffectiveFrom, request.Description, request.TopicId, request.AudienceProfileId,
                request.LanguageCode, request.PathStatus, request.EffectiveTo, request.Source,
                StepsProvided: request.Steps is not null, request.ExpectedVersion),
            cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/publish")]
    [HasPermission(Perms.ManageFallback)] // canonical crm.knowledge.path.publish (F-RBAC); fallback collapses to manage
    public async Task<IActionResult> Publish(
        Guid pathId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new PublishKnowledgePathCommand(pathId, expectedVersion), cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/new-version")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> NewVersion(
        Guid pathId, [FromBody] CreateKnowledgePathVersionRequest? request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateKnowledgePathVersionCommand(pathId, request?.NewPathVersion), cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(
        Guid pathId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveKnowledgePathCommand(pathId, expectedVersion), cancellationToken));

    // ---------------- embedded steps (sub-resource of a path) ----------------

    [HttpGet("api/crm/knowledge/paths/{pathId:guid}/steps")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ListSteps(
        Guid pathId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] DateTimeOffset? effectiveAt = null,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetKnowledgePathStepsQuery(pathId, includeArchived, effectiveAt), cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/steps")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> AddStep(
        Guid pathId, [FromBody] AddKnowledgePathStepRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new AddKnowledgePathStepCommand(
                pathId, request.StepOrder, request.StepCode, request.StepTitle, request.StepType, request.ContentId,
                request.IsRequired, request.VersionPinPolicy, request.CompletionRule, request.PrerequisiteStepId,
                request.ConceptNodeId, request.EstimatedDurationMinutes, request.Notes,
                MapBranch(request.BranchConditions), request.ExpectedVersion),
            cancellationToken));

    [HttpPut("api/crm/knowledge/paths/{pathId:guid}/steps/{stepId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> UpdateStep(
        Guid pathId, Guid stepId, [FromBody] UpdateKnowledgePathStepRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateKnowledgePathStepCommand(
                pathId, stepId, request.StepOrder, request.StepCode, request.StepTitle, request.StepType,
                request.ContentId, request.IsRequired, request.VersionPinPolicy, request.CompletionRule,
                request.PrerequisiteStepId, request.ConceptNodeId, request.EstimatedDurationMinutes, request.Notes,
                MapBranch(request.BranchConditions), request.ExpectedVersion),
            cancellationToken));

    [HttpPost("api/crm/knowledge/paths/{pathId:guid}/steps/{stepId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> ArchiveStep(
        Guid pathId, Guid stepId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveKnowledgePathStepCommand(pathId, stepId, expectedVersion), cancellationToken));

    private static IReadOnlyList<KnowledgePathBranchConditionInput>? MapBranch(
        IReadOnlyList<KnowledgePathBranchConditionRequest>? conditions)
        => conditions?
            .Select(c => new KnowledgePathBranchConditionInput(c.ConditionCode, c.Description, c.TargetStepId))
            .ToList();
}
