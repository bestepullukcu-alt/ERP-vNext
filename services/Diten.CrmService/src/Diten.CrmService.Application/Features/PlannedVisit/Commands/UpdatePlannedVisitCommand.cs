using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Commands;

/// <summary>
/// Edits a plan. <c>VisitCode</c> is absent — the stable business key is never renamed — and the lifecycle status is not
/// an input here (it moves only through confirm / cancel / archive). An archived plan accepts nothing (409). A past
/// <c>PlannedDate</c> is refused unless the plan is still <c>draft</c> (V7). The journey/stage are re-validated on every
/// write, whether they were rep-entered or strategy-default-filled (V17/V18).
/// </summary>
public sealed record UpdatePlannedVisitCommand(
    Guid PlannedVisitId,
    string TargetType,
    Guid TargetId,
    string? PlannedDate,
    string? PlannedStartTime,
    string? PlannedEndTime,
    int? PlannedDurationMinutes,
    string ResourceId,
    string ResourceType,
    string? ResourceDisplayName,
    string? PositionCode,
    Guid? PositionId,
    string VisitPurpose,
    string VisitType,
    string? Objective,
    string? Notes,
    string? BusinessUnit,
    Guid? TerritoryNodeId,
    Guid? TerritoryModelId,
    Guid? CampaignId,
    Guid? ContentEngagementJourneyId,
    Guid? ContentEngagementJourneyStageId,
    int? ExpectedVersion,
    string? ContentSource = null,
    Guid? StrategyTemplateId = null,
    Guid? SegmentId = null) : IRequest<Response<bool>>;
