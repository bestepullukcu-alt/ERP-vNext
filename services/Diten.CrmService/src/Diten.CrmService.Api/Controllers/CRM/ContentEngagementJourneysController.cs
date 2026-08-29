using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Commands;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney
    .ContentEngagementJourneyPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0162 FU05 — ContentEngagementJourney authoring with EMBEDDED stages (S2). Canonical under
/// <c>/api/crm/knowledge/content-engagement-journeys</c> (inside the existing knowledge ocelot wildcard); stages are the
/// journey's sub-resource (<c>/{journeyId}/stages…</c>) — there is no flat <c>/content-engagement-journey-stages</c>
/// family and no DELETE/PATCH (closing is archive). Publish is a separate endpoint + permission (SoD). Under the
/// documented DEV-ONLY fallback publish collapses onto manage; the canonical
/// <c>crm.knowledge.content-engagement-journey.publish</c> is defined but not seeded (F-RBAC).
/// No endpoint recommends, scores, advances or reports progress — by construction.
/// </summary>
[Authorize]
public sealed class ContentEngagementJourneysController : CustomBaseController
{
    private readonly IMediator _mediator;

    public ContentEngagementJourneysController(IMediator mediator) => _mediator = mediator;

    // ---------------- journeys ----------------

    [HttpGet("api/crm/knowledge/content-engagement-journeys")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] Guid? subjectId,
        [FromQuery] Guid? topicId,
        [FromQuery] Guid? audienceProfileId,
        [FromQuery] string? language,
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? effectiveAt,
        [FromQuery] string? journeyCode,
        [FromQuery] Guid? knowledgePathId,
        [FromQuery] string? search,
        [FromQuery] bool includeArchived = true,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListContentEngagementJourneysQuery(
                subjectId, topicId, audienceProfileId, language, status, effectiveAt, journeyCode, knowledgePathId,
                search, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/knowledge/content-engagement-journeys/{journeyId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(
        Guid journeyId, [FromQuery] DateTimeOffset? effectiveAt, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetContentEngagementJourneyQuery(journeyId, effectiveAt), cancellationToken));

    [HttpPost("api/crm/knowledge/content-engagement-journeys")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreateContentEngagementJourneyRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateContentEngagementJourneyCommand(
                request.JourneyCode, request.JourneyName, request.SubjectId, request.Objective,
                request.JourneyVersion, request.EffectiveFrom, request.Description, request.TopicId,
                request.AudienceProfileId, request.LanguageCode, request.JourneyStatus, request.EffectiveTo,
                request.Source),
            cancellationToken));

    [HttpPut("api/crm/knowledge/content-engagement-journeys/{journeyId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid journeyId, [FromBody] UpdateContentEngagementJourneyRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateContentEngagementJourneyCommand(
                journeyId, request.JourneyName, request.SubjectId, request.Objective, request.JourneyVersion,
                request.EffectiveFrom, request.Description, request.TopicId, request.AudienceProfileId,
                request.LanguageCode, request.JourneyStatus, request.EffectiveTo, request.Source,
                StagesProvided: request.Stages is not null, request.ExpectedVersion),
            cancellationToken));

    /// <summary>Separate publish endpoint + canonical publish permission (SoD: author ≠ publisher).</summary>
    [HttpPost("api/crm/knowledge/content-engagement-journeys/{journeyId:guid}/publish")]
    [HasPermission(Perms.ManageFallback)] // canonical crm.knowledge.content-engagement-journey.publish (F-RBAC);
                                          // fallback collapses onto manage
    public async Task<IActionResult> Publish(
        Guid journeyId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new PublishContentEngagementJourneyCommand(journeyId, expectedVersion), cancellationToken));

    [HttpPost("api/crm/knowledge/content-engagement-journeys/{journeyId:guid}/new-version")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> NewVersion(
        Guid journeyId,
        [FromBody] CreateContentEngagementJourneyVersionRequest? request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreateContentEngagementJourneyVersionCommand(journeyId, request?.NewJourneyVersion),
            cancellationToken));

    [HttpPost("api/crm/knowledge/content-engagement-journeys/{journeyId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(
        Guid journeyId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveContentEngagementJourneyCommand(journeyId, expectedVersion), cancellationToken));

    // ---------------- embedded stages (sub-resource of the journey — S2) ----------------

    [HttpGet("api/crm/knowledge/content-engagement-journeys/{journeyId:guid}/stages")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> ListStages(
        Guid journeyId,
        [FromQuery] bool includeArchived = false,
        [FromQuery] DateTimeOffset? effectiveAt = null,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new GetContentEngagementJourneyStagesQuery(journeyId, includeArchived, effectiveAt), cancellationToken));

    [HttpPost("api/crm/knowledge/content-engagement-journeys/{journeyId:guid}/stages")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> AddStage(
        Guid journeyId,
        [FromBody] AddContentEngagementJourneyStageRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new AddContentEngagementJourneyStageCommand(
                journeyId, request.StageOrder, request.StageCode, request.StageName, request.StageObjective,
                request.RecommendedKnowledgePathId, request.IsRequired, request.Repeatable, request.StageType,
                request.PathVersionPinPolicy, request.MinVisitNumber, request.MaxVisitNumber, request.AdvancementRule,
                request.FallbackStageId, request.Notes, MapBranch(request.BranchConditions), request.ExpectedVersion),
            cancellationToken));

    [HttpPut("api/crm/knowledge/content-engagement-journeys/{journeyId:guid}/stages/{stageId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> UpdateStage(
        Guid journeyId,
        Guid stageId,
        [FromBody] UpdateContentEngagementJourneyStageRequest request,
        CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdateContentEngagementJourneyStageCommand(
                journeyId, stageId, request.StageOrder, request.StageCode, request.StageName, request.StageObjective,
                request.RecommendedKnowledgePathId, request.IsRequired, request.Repeatable, request.StageType,
                request.PathVersionPinPolicy, request.MinVisitNumber, request.MaxVisitNumber, request.AdvancementRule,
                request.FallbackStageId, request.Notes, MapBranch(request.BranchConditions), request.ExpectedVersion),
            cancellationToken));

    [HttpPost("api/crm/knowledge/content-engagement-journeys/{journeyId:guid}/stages/{stageId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> ArchiveStage(
        Guid journeyId, Guid stageId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchiveContentEngagementJourneyStageCommand(journeyId, stageId, expectedVersion), cancellationToken));

    private static IReadOnlyList<ContentEngagementJourneyBranchConditionInput>? MapBranch(
        IReadOnlyList<ContentEngagementJourneyBranchConditionRequest>? conditions)
        => conditions?
            .Select(c => new ContentEngagementJourneyBranchConditionInput(
                c.ConditionCode, c.Description, c.TargetStageId))
            .ToList();
}
