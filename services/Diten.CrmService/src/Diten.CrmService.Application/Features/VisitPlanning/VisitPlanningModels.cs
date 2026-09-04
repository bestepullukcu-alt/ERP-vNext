namespace Diten.CrmService.Application.Features.VisitPlanning;

// ---------------------------------------------------------------------------------------------------------------
// MOD-0155 FU05 — the generation / preview / apply DTOs of the MicroTarget Visit Planning Engine, in ONE file (the
// same one-file exception the RouteOptimization / VisitContentSequence models use). TenantId appears in NO payload:
// it is server-resolved. Dates are ISO "yyyy-MM-dd" STRINGS and times are "HH:mm" strings (inherited from FU01/FU03)
// — never DateTimeOffset (the CRM parallel-arrays trap). The SupplyDemandSummary is TRANSIENT (D-SUPPLY-DEMAND-SHAPE
// = A): it is recomputed on every preview and NEVER persisted on the session.
// ---------------------------------------------------------------------------------------------------------------

/// <summary>The options a generation run needs beyond the session's own selection. All optional — the engine fills
/// sensible in-domain defaults (medical-visit / field-visit) so a preview needs no ceremony.</summary>
public sealed record VisitPlanGenerationOptions(
    string? VisitPurpose = null,
    string? VisitType = null,
    string? BusinessUnit = null,
    double? StartLat = null,
    double? StartLong = null,
    DateTimeOffset? EffectiveAt = null,
    // Optional MANUAL visiting sequence (target ids, first→last). When present the per-week route is scheduled in this
    // order (constraint-honored) instead of the greedy optimum; null ⇒ engine optimum. Applies WITHIN each week only.
    IReadOnlyList<Guid>? ManualVisitOrder = null);

/// <summary>The transient dry-run answer of a preview (§4.1 ⑧). Persists NOTHING. It carries the proposed Day/Week grid,
/// the unschedulable warning list, the supply-vs-demand summary and the per-doctor content preview.</summary>
public sealed record VisitPlanPreview(
    Guid PlanningSessionId,
    Guid CyclePeriodId,
    string ResourceId,
    string PeriodStart,
    string PeriodEnd,
    int WeekCount,
    IReadOnlyList<PlannedSlotPreview> Scheduled,
    IReadOnlyList<UnscheduledPreview> Unscheduled,
    IReadOnlyList<DoctorContentPreview> Content,
    IReadOnlyList<TerritoryWarning> TerritoryWarnings,
    SupplyDemandSummary SupplyDemand,
    DateTimeOffset GeneratedAt);

/// <summary>One proposed visit slot in the preview grid — route-ordered, week-tagged. Nothing here is persisted until
/// apply writes it onto an FU01 PlannedVisit atom.</summary>
public sealed record PlannedSlotPreview(
    Guid VisitRef,
    int WeekNumber,
    string TargetType,
    Guid TargetId,
    Guid? AccountId,
    Guid? ContactId,
    Guid? AccountContactLinkId,
    string PlannedDate,
    string? StartTime,
    string? EndTime,
    int SequenceOrder,
    int DurationMinutes,
    Guid? JourneyId,
    Guid? StageId,
    int? StageIndex,
    int PromoItemCount,
    int NonPromoItemCount,
    string ContentStatus,
    // Resolved from the Contact aggregate at preview time (never persisted here) so the UI shows the doctor's name/
    // specialty without depending on an account↔contact link existing. Null for account-level slots or unknown ids.
    string? ContactDisplayName = null,
    string? ContactSpecialty = null);

/// <summary>One visit that could not be feasibly placed — the supply-vs-demand WARNING materialised (FU03 unscheduled).
/// A warning the planner resolves, never a hard block (D-SUPPLY-DEMAND).</summary>
public sealed record UnscheduledPreview(
    int WeekNumber,
    string TargetType,
    Guid TargetId,
    Guid? ContactId,
    string Reason);

/// <summary>The per-doctor content preview (FU04): next stage + resolved visit duration. Read-only.</summary>
public sealed record DoctorContentPreview(
    Guid ContactId,
    Guid? AccountId,
    string ContentStatus,
    Guid? JourneyId,
    Guid? StageId,
    int? StageIndex,
    string? StageDisplayName,
    int PromoItemCount,
    int NonPromoItemCount,
    int VisitDurationMinutes,
    IReadOnlyList<string> ReasonCodes,
    string? ConsentStatus,
    bool ConsentBlocked,
    string? ConsentReason);

/// <summary>The TRANSIENT supply-vs-demand summary (D-SUPPLY-DEMAND-SHAPE = A). <see cref="Supply"/> is the
/// CyclePeriod-pinned CycleCapacity.TotalVisitNumber (visits the rep CAN do; null when the calendar could not resolve
/// it); <see cref="Demand"/> is the visits PLANNED. Over-plan surfaces a WARNING; the planner MAY still proceed —
/// NEVER a hard block. It is never persisted on the session; only the coarse <see cref="Status"/> flag is.</summary>
public sealed record SupplyDemandSummary(
    int? Supply,
    int Demand,
    int ScheduledCount,
    int UnscheduledCount,
    string Status,
    IReadOnlyList<string> ReasonCodes);

/// <summary>The apply result — the FU01 atom ids written (§4.1 ⑧). The session is now <c>committed</c>.</summary>
public sealed record VisitPlanApplyResult(
    Guid PlanningSessionId,
    string Status,
    IReadOnlyList<Guid> CommittedPlannedVisitIds,
    int ScheduledCount,
    int UnscheduledCount);

/// <summary>A read model of the staging session for the console's "my draft plans" list + detail.</summary>
public sealed record PlanningSessionDto(
    Guid PlanningSessionId,
    Guid CyclePeriodId,
    string ResourceId,
    string ResourceType,
    string? ResourceDisplayName,
    string Status,
    IReadOnlyList<Guid> SelectedAccountIds,
    IReadOnlyList<Guid> SelectedPharmacyIds,
    IReadOnlyList<PlanningSessionContactDto> SelectedContacts,
    Guid? SegmentId,
    Guid? CampaignId,
    DateTimeOffset? LastGeneratedAt,
    int ScheduledCount,
    int UnscheduledCount,
    string? SupplyDemandStatus,
    IReadOnlyList<Guid> CommittedPlannedVisitIds,
    IReadOnlyList<Guid> ManualVisitOrder,
    string? TargetWeekStart,
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public sealed record PlanningSessionContactDto(Guid ContactId, Guid? AccountId, Guid? AccountContactLinkId);

public sealed record PlanningSessionListDto(IReadOnlyList<PlanningSessionListItemDto> Items, int TotalCount);

public sealed record PlanningSessionListItemDto(
    Guid PlanningSessionId,
    Guid CyclePeriodId,
    string ResourceId,
    string? ResourceDisplayName,
    string Status,
    int SelectedContactCount,
    int ScheduledCount,
    string? SupplyDemandStatus,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    string? TargetWeekStart = null);
