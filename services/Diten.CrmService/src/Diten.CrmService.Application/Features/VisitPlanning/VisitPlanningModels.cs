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
    DateTimeOffset GeneratedAt,
    // WP-VP-FIX-1 (C3, additive) — whether the working calendar answered (resolved / unresolved + reason) and the run's
    // non-working days (yyyy-MM-dd, every generated week): weekends + holidays, or the Sat/Sun fallback when unresolved.
    PlanningCalendarStatusDto? CalendarStatus = null,
    IReadOnlyList<string>? NonWorkingDates = null,
    // WP-VP-3A (additive) — EVERY week of the period with its derived status (past / approved / draft / empty) and visit
    // count; WeekNumber on a slot is the index into this list.
    IReadOnlyList<PlanningWeekDto>? Weeks = null,
    // WP-VP-3B (additive) — half days (yyyy-MM-dd), the visits moved to a later week, every week's capacity and the
    // period in minutes (VisitPlanningCapacityModels.cs).
    IReadOnlyList<string>? HalfDayDates = null,
    IReadOnlyList<ShiftedVisitPreview>? Shifted = null,
    IReadOnlyList<WeekCapacityDto>? WeekCapacity = null,
    PeriodCapacityDto? PeriodCapacity = null,
    // WP-VP-3C (K-7, additive) — per product: how many doctors' plans tell it; the doctors left without any product;
    // the rep's portfolio state (always undefined: no portfolio data yet).
    IReadOnlyList<ProductDistributionDto>? ProductDistribution = null,
    int DoctorsWithoutProducts = 0,
    string PortfolioStatus = PortfolioStatuses.Undefined);

/// <summary>WP-VP-3A — one week of the period plan. <see cref="Status"/> is derived (<see cref="PlanningWeekCalendar"/>);
/// <see cref="StoredStatus"/> is the stored approve state (approved / reopened, null when never approved).</summary>
public sealed record PlanningWeekDto(
    string WeekStart,
    int IsoWeek,
    string From,
    string To,
    string Status,
    int? VisitCount,
    string? StoredStatus = null,
    DateTimeOffset? ApprovedAt = null,
    string? ApprovedBy = null,
    IReadOnlyList<PlanningWeekHistoryDto>? History = null);

/// <summary>WP-VP-3A (MK-4) — one approve / reopen of a week.</summary>
public sealed record PlanningWeekHistoryDto(DateTimeOffset At, string? By, string Action, string? Reason);

/// <summary>WP-VP-3A — the reopen answer: the week's new state and what happened to its visits.</summary>
public sealed record PlanningWeekReopenResult(
    Guid PlanningSessionId,
    string WeekStart,
    string Status,
    IReadOnlyList<Guid> CancelledPlannedVisitIds,
    IReadOnlyList<Guid> KeptPlannedVisitIds);

/// <summary>WP-VP-FIX-1 — the working-calendar outcome of a run: <see cref="PlanningCalendarStatuses"/> + the reason code
/// / text when the Sat/Sun fallback ran instead.</summary>
public sealed record PlanningCalendarStatusDto(string Status, string? ReasonCode, string? Reason);

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
    string? ContactSpecialty = null,
    // WP-SB-3b — the products this visit tells (projected over the doctor's earlier visits). Additive; no play /
    // campaign id inside.
    IReadOnlyList<Diten.CrmService.Application.Features.VisitContentSequence.VisitContentItem>? ContentItems = null,
    // WP-VP-3A (additive) — the slot's week (Monday yyyy-MM-dd); IsFixed = an already-approved visit shown as it was
    // written (not re-generated, S-1); the target's cadence (frequencyStatus resolved / conflict / unknown and the visits
    // the WHOLE period needs).
    string? WeekStart = null,
    bool IsFixed = false,
    string? FrequencyStatus = null,
    int? RequiredVisitCount = null,
    // WP-VP-3C (K-7, additive) — the products the role limits left out of this visit (they lead the next one) and the
    // visit's product warnings (no_products / no_approved_content / ambiguous_journey …). Each content item also
    // carries its source and order.
    IReadOnlyList<OverflowProductPreview>? OverflowProducts = null,
    IReadOnlyList<string>? ProductWarnings = null);

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
    string? ConsentReason,
    // WP-SB-3b — the doctor's next visit products (resolver v2 items). Additive.
    IReadOnlyList<Diten.CrmService.Application.Features.VisitContentSequence.VisitContentItem>? Items = null,
    // WP-VP-3A (additive) — the doctor's cadence: status + the visits the whole period needs.
    string? FrequencyStatus = null,
    int? RequiredVisitCount = null,
    // WP-VP-3C (K-7, additive) — the doctor's next visit's products (with source) and its duration from that list.
    IReadOnlyList<VisitProductPreview>? Products = null,
    int? DurationMinutes = null);

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
    int UnscheduledCount,
    // WP-VP-3A (additive) — set when a single week was approved: its Monday and stored status ("approved"). For a week
    // approval CommittedPlannedVisitIds lists the atoms THIS approval wrote.
    string? WeekStart = null,
    string? WeekStatus = null);

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
    string? UpdatedBy,
    // WP-VP-2 (B-8, additive) — the selected accounts / pharmacies with their names (the id arrays above are unchanged).
    IReadOnlyList<PlanningSessionNamedRefDto>? SelectedAccounts = null,
    IReadOnlyList<PlanningSessionNamedRefDto>? SelectedPharmacies = null,
    // WP-VP-3A (additive) — every week of the period with its status. Approved weeks carry their visit count; for the
    // other weeks the detail cannot know without generating, so it says draft when the plan has targets, empty when it
    // has none (VisitCount null) — the exact per-week draft / empty comes from the preview.
    IReadOnlyList<PlanningWeekDto>? Weeks = null,
    // WP-VP-4A (additive) — today's week (when today is inside the period) and the first week after today's week that
    // is not approved and not past (null when none, and always null for an old committed plan: it has no drafts).
    string? CurrentWeekStart = null,
    string? NextDraftWeekStart = null);

public sealed record PlanningSessionContactDto(
    Guid ContactId,
    Guid? AccountId,
    Guid? AccountContactLinkId,
    // WP-VP-2 (B-8, additive) — read-time names.
    string? ContactDisplayName = null,
    string? AccountDisplayName = null,
    // WP-VP-4A (E4-3C-B1, additive) — the rep's stored product pick for this doctor (3C); empty when none.
    IReadOnlyList<PlanningSessionProductDto>? Products = null);

/// <summary>WP-VP-4A — one product of a doctor's stored pick. <c>ProductName</c> is not read from MDM on a plan read
/// (the stored <c>ProductCode</c> is the display); <c>Role</c> promo / non-promo (a pick without a role reads promo,
/// K-7d).</summary>
public sealed record PlanningSessionProductDto(Guid ProductId, string? ProductCode, string? ProductName, string Role);

/// <summary>WP-VP-2 (B-8) — a selected account / pharmacy with its read-time name (null when it cannot be found).</summary>
public sealed record PlanningSessionNamedRefDto(Guid Id, string? DisplayName);

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
    string? TargetWeekStart = null,
    // WP-VP-FIX-1 (D1, additive) — the list's "N doctors · M pharmacies" column.
    int SelectedPharmacyCount = 0,
    // WP-VP-3A (D3, additive) — no target at all (the "empty draft" badge; only such a plan may be archived).
    bool IsEmpty = false,
    int ApprovedWeekCount = 0,
    // WP-VP-4A (brief §1, additive) — distinct doctors / pharmacies of the selection, and the period's weeks that are
    // neither past nor approved (counted without generating; 0 for an old committed or an archived plan).
    int DoctorCount = 0,
    int PharmacyCount = 0,
    int DraftWeekCount = 0);
