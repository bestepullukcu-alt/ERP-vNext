using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.VisitPlanning.Commands;

/// <summary>Creates a staging session for a rep + period (born <c>draft</c>). TenantId is server-resolved and never a
/// payload field. The selection may be empty at create and filled later through
/// <see cref="UpdatePlanningSessionSelectionCommand"/>.</summary>
public sealed record CreatePlanningSessionCommand(
    Guid CyclePeriodId,
    string ResourceId,
    string? ResourceType,
    string? ResourceDisplayName,
    IReadOnlyList<Guid>? SelectedAccountIds,
    IReadOnlyList<Guid>? SelectedPharmacyIds,
    IReadOnlyList<SelectedContactInput>? SelectedContacts,
    Guid? SegmentId,
    Guid? CampaignId,
    Guid? StrategyTemplateId,
    // Chosen plan week's Monday (yyyy-MM-dd) — persisted so Details/Edit resolve the saved week.
    string? TargetWeekStart = null) : IRequest<Response<Guid>>;

/// <summary>Edits a session's selection (and optionally moves its status FORWARD — draft→generated after a preview, or
/// →archived). The status machine has NO reverse transition (§12): a backward or same-rank move is a 409.</summary>
public sealed record UpdatePlanningSessionSelectionCommand(
    Guid PlanningSessionId,
    IReadOnlyList<Guid>? SelectedAccountIds,
    IReadOnlyList<Guid>? SelectedPharmacyIds,
    IReadOnlyList<SelectedContactInput>? SelectedContacts,
    Guid? SegmentId,
    Guid? CampaignId,
    Guid? StrategyTemplateId,
    string? RequestedStatus,
    int? ExpectedVersion,
    // Chosen plan week's Monday (yyyy-MM-dd) — persisted so Details/Edit resolve the saved week.
    string? TargetWeekStart = null) : IRequest<Response<bool>>;

/// <summary>Applies the session: generates the plan, writes the FU01 atoms atomically and flips the session to
/// <c>committed</c>. Requires BOTH <c>crm.visit-plan.apply</c> AND FU01 <c>crm.planned-visit.manage</c> at the endpoint.</summary>
public sealed record ApplyPlanningSessionCommand(
    Guid PlanningSessionId,
    string? VisitPurpose,
    string? VisitType,
    double? StartLat,
    double? StartLong,
    int? ExpectedVersion,
    // Optional manual visiting order (target ids) — persisted on the session and used to write the atoms in this order.
    IReadOnlyList<Guid>? ManualVisitOrder = null) : IRequest<Response<VisitPlanApplyResult>>;

/// <summary>Re-plans a subset (doctor missed / "I can go day X"): re-runs the route for the affected contacts and
/// updates ONLY their atoms IN PLACE (D-REPLAN = A). The session is not reopened.</summary>
public sealed record ReplanPlanningSessionCommand(
    Guid PlanningSessionId,
    IReadOnlyList<Guid> AffectedContactIds,
    string? VisitPurpose,
    string? VisitType,
    double? StartLat,
    double? StartLong,
    // Optional manual visiting order (target ids) — the affected-subset route honors it; null ⇒ engine optimum.
    IReadOnlyList<Guid>? ManualVisitOrder = null) : IRequest<Response<VisitPlanApplyResult>>;

/// <summary>One manually-picked doctor on the wire.</summary>
public sealed record SelectedContactInput(Guid ContactId, Guid? AccountId, Guid? AccountContactLinkId);
