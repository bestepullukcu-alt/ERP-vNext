using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitPlanning.Queries;

/// <summary>Runs a dry-run preview of the session's plan (①–⑦). Persists NOTHING — the session status is unchanged and
/// no atom is written. The transient <see cref="VisitPlanPreview"/> (incl. the SupplyDemandSummary) is returned only.</summary>
public sealed record GeneratePlanPreviewQuery(
    Guid PlanningSessionId,
    string? VisitPurpose = null,
    string? VisitType = null,
    double? StartLat = null,
    double? StartLong = null,
    // Optional manual visiting order (target ids). Present ⇒ the preview honors this sequence; null ⇒ engine optimum.
    IReadOnlyList<Guid>? ManualVisitOrder = null) : IRequest<Response<VisitPlanPreview>>;

/// <summary>Lists the tenant's staging sessions (the console's "my draft plans"). Optionally narrowed by rep / period /
/// status.</summary>
public sealed record ListPlanningSessionsQuery(
    Guid? CyclePeriodId = null,
    string? ResourceId = null,
    string? Status = null) : IRequest<Response<PlanningSessionListDto>>;

/// <summary>Reads one staging session by id (tenant-scoped; cross-tenant → 404).</summary>
public sealed record GetPlanningSessionByIdQuery(Guid PlanningSessionId) : IRequest<Response<PlanningSessionDto>>;
