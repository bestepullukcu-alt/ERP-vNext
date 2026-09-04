using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.PlannedVisit.Queries;

/// <summary>
/// Lists plans for the tenant, narrowed by the supported filters. Archived rows are hidden unless
/// <paramref name="IncludeArchived"/> is true. The <c>resourceId</c> filter is an EXPLICIT narrowing, not an ambient
/// "only my plans" scope — that ABAC rule cannot be faked before MOD-0018-FU15 (§8.6/F-ABAC).
/// </summary>
public sealed record ListPlannedVisitsQuery(
    string? PlannedDateFrom = null,
    string? PlannedDateTo = null,
    string? ResourceId = null,
    string? TargetType = null,
    Guid? TargetId = null,
    string? PlanStatus = null,
    string? VisitPurpose = null,
    Guid? TerritoryNodeId = null,
    Guid? CampaignId = null,
    bool IncludeArchived = false) : IRequest<Response<PlannedVisitListDto>>;
