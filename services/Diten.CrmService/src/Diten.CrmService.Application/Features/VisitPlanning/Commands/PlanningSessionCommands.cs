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
    string? TargetWeekStart = null,
    // WP-VP-4E — one draft week's day pins: null = keep every week's pins; { weekStart, pins: [] } = clear that week's.
    DayPinsInput? DayPins = null) : IRequest<Response<bool>>;

/// <summary>WP-VP-4E — the day pins of ONE draft week (they replace that week's pins; a null / empty list clears them).</summary>
public sealed record DayPinsInput(string? WeekStart, IReadOnlyList<DayPinInput>? Pins);

/// <summary>WP-VP-4E — one day pin on the wire: the visit target (contact + contact id for a doctor, pharmacy / account +
/// account id otherwise), the day (yyyy-MM-dd) and the scope (visit | institution; absent = visit).</summary>
public sealed record DayPinInput(string? TargetType, Guid TargetId, Guid? ContactId, string? Date, string? Scope);

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
    IReadOnlyList<Guid>? ManualVisitOrder = null,
    // WP-VP-3A — a Monday (yyyy-MM-dd): approve ONLY this week (its atoms + the week stored as approved; the session is
    // NOT committed). Absent ⇒ today's whole-period apply that commits the session (to be removed with Faz 4).
    string? WeekStart = null) : IRequest<Response<VisitPlanApplyResult>>;

/// <summary>WP-VP-3A (MK-4) — reopens an approved week: its visits without a report / outcome are cancelled
/// (<c>week_reopened</c>), reported ones stay; the week becomes <c>reopened</c> and the reason goes into its history.
/// Same permissions as apply (apply AND planned-visit.manage).</summary>
public sealed record ReopenPlanningWeekCommand(
    Guid PlanningSessionId,
    string WeekStart,
    string? Reason,
    int? ExpectedVersion) : IRequest<Response<PlanningWeekReopenResult>>;

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
public sealed record SelectedContactInput(
    Guid ContactId,
    Guid? AccountId,
    Guid? AccountContactLinkId,
    // WP-VP-3C (K-7, S-4) — the rep's product pick for this doctor: null = keep what is stored, [] = clear, a list = set.
    IReadOnlyList<SelectedProductInput>? Products = null);

/// <summary>WP-VP-3C — one picked product: MDM Global Product id, a display code, the role (promo / non-promo; null =
/// promo).</summary>
public sealed record SelectedProductInput(Guid ProductId, string? ProductCode, string? Role);
