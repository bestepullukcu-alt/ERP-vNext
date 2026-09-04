using Diten.CrmService.Api.Models.CRM;
using Diten.CrmService.Application.Features.PlannedVisit.Commands;
using Diten.CrmService.Application.Features.PlannedVisit.Queries;
using Diten.CrmService.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Perms = Diten.CrmService.Application.Features.PlannedVisit.PlannedVisitPermissions;

namespace Diten.CrmService.Api.Controllers.CRM;

/// <summary>
/// MOD-0155 FU01 — PlannedVisit: the field team's planning atom.
/// <para>There is <b>no DELETE, no PATCH and no bulk-delete</b> anywhere in this controller (§8.2): a plan is cancelled
/// and/or archived. <c>confirm</c> takes the separate <c>crm.planned-visit.confirm</c> permission so the author and the
/// confirmer can differ; <c>cancel</c> and <c>archive</c> take <c>manage</c>. Under the documented DEV-ONLY fallback,
/// manage and confirm collapse onto one territory key, so SoD cannot be enforced in dev (F-RBAC).</para>
/// </summary>
[Authorize]
public sealed class PlannedVisitsController : CustomBaseController
{
    private readonly IMediator _mediator;

    public PlannedVisitsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("api/crm/planned-visits/contract")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Contract(CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(new GetPlannedVisitContractQuery(), cancellationToken));

    [HttpGet("api/crm/planned-visits")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> List(
        [FromQuery] string? plannedDateFrom,
        [FromQuery] string? plannedDateTo,
        [FromQuery] string? resourceId,
        [FromQuery] string? targetType,
        [FromQuery] Guid? targetId,
        [FromQuery] string? planStatus,
        [FromQuery] string? visitPurpose,
        [FromQuery] Guid? territoryNodeId,
        [FromQuery] Guid? campaignId,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
        => CreateActionResultInstance(await _mediator.Send(
            new ListPlannedVisitsQuery(
                plannedDateFrom, plannedDateTo, resourceId, targetType, targetId,
                planStatus, visitPurpose, territoryNodeId, campaignId, includeArchived),
            cancellationToken));

    [HttpGet("api/crm/planned-visits/{plannedVisitId:guid}")]
    [HasPermission(Perms.ReadFallback)]
    public async Task<IActionResult> Get(Guid plannedVisitId, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new GetPlannedVisitByIdQuery(plannedVisitId), cancellationToken));

    [HttpPost("api/crm/planned-visits")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePlannedVisitRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CreatePlannedVisitCommand(
                request.VisitCode, request.TargetType, request.TargetId, request.PlannedDate,
                request.PlannedStartTime, request.PlannedEndTime, request.PlannedDurationMinutes,
                request.ResourceId, request.ResourceType, request.ResourceDisplayName,
                request.PositionCode, request.PositionId,
                request.VisitPurpose, request.VisitType, request.Objective, request.Notes,
                request.BusinessUnit, request.TerritoryNodeId, request.TerritoryModelId, request.CampaignId,
                request.ContentEngagementJourneyId, request.ContentEngagementJourneyStageId,
                request.PlanStatus, request.Source, request.ContentSource,
                request.StrategyTemplateId, request.SegmentId),
            cancellationToken));

    [HttpPut("api/crm/planned-visits/{plannedVisitId:guid}")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Update(
        Guid plannedVisitId, [FromBody] UpdatePlannedVisitRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new UpdatePlannedVisitCommand(
                plannedVisitId, request.TargetType, request.TargetId, request.PlannedDate,
                request.PlannedStartTime, request.PlannedEndTime, request.PlannedDurationMinutes,
                request.ResourceId, request.ResourceType, request.ResourceDisplayName,
                request.PositionCode, request.PositionId,
                request.VisitPurpose, request.VisitType, request.Objective, request.Notes,
                request.BusinessUnit, request.TerritoryNodeId, request.TerritoryModelId, request.CampaignId,
                request.ContentEngagementJourneyId, request.ContentEngagementJourneyStageId,
                request.ExpectedVersion, request.ContentSource, request.StrategyTemplateId, request.SegmentId),
            cancellationToken));

    /// <summary>Confirms a plan. The consent guard is fail-closed HERE (D6); blocked/unknown/filter-not-applied is 409.</summary>
    [HttpPost("api/crm/planned-visits/{plannedVisitId:guid}/confirm")]
    // Canonical crm.planned-visit.confirm (F-RBAC); under the DEV-ONLY fallback it collapses onto manage.
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Confirm(
        Guid plannedVisitId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ConfirmPlannedVisitCommand(plannedVisitId, expectedVersion), cancellationToken));

    /// <summary>Cancels a plan. A cancellation reason is required (V21).</summary>
    [HttpPost("api/crm/planned-visits/{plannedVisitId:guid}/cancel")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Cancel(
        Guid plannedVisitId, [FromBody] CancelPlannedVisitRequest request, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new CancelPlannedVisitCommand(plannedVisitId, request.CancellationReason, request.ExpectedVersion),
            cancellationToken));

    /// <summary>Archives a plan. Terminal — there is no unarchive endpoint.</summary>
    [HttpPost("api/crm/planned-visits/{plannedVisitId:guid}/archive")]
    [HasPermission(Perms.ManageFallback)]
    public async Task<IActionResult> Archive(
        Guid plannedVisitId, [FromQuery] int? expectedVersion, CancellationToken cancellationToken)
        => CreateActionResultInstance(await _mediator.Send(
            new ArchivePlannedVisitCommand(plannedVisitId, expectedVersion), cancellationToken));
}
