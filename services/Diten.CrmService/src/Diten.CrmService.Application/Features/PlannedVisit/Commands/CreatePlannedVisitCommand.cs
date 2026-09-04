using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Commands;

/// <summary>
/// Creates a plan. TenantId is resolved server-side from the claim and never accepted from a payload. A plan may be
/// born <c>draft</c> or <c>planned</c>; <c>confirmed</c> is reached only through the separate confirm endpoint, where the
/// consent guard runs (D6). Motor-filled slot fields and the derived provenance blocks are NOT inputs — if a client
/// sends them they are ignored (V26); the journey/stage (fields 26/27) are the one exception and DO write into the
/// content-position ref (D10).
/// </summary>
public sealed record CreatePlannedVisitCommand(
    string VisitCode,
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
    string? PlanStatus,
    /// <summary>Plan source. FU01 writes only <c>manual</c> (default); a reserved value is refused (V20).</summary>
    string? Source = null,
    /// <summary>Content-position marker (D10). Optional and validated against the vocabulary; when a strategy chain
    /// default-filled 26/27 the UI passes <c>strategy</c>, otherwise the server treats a rep-entered journey as
    /// <c>manual</c>. A set-outside value is a 400 (AC-CONTENT-3).</summary>
    string? ContentSource = null,
    /// <summary>Snapshot provenance (D10/D11) — NOT validated, NOT an FK, NOT a form field. Kept so the default's origin
    /// and the target's selection origin stay auditable.</summary>
    Guid? StrategyTemplateId = null,
    Guid? SegmentId = null) : IRequest<Response<Guid>>;
