namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0155 FU01 — <b>PlannedVisit</b>: the field team's planning atom. It answers exactly one question — <i>"WHO is to
/// be visited, WHEN, for WHAT purpose, by WHICH resource, in WHICH tenant?"</i> — and stores (never computes) the
/// richer context the later FUs will fill in.
/// <para><b>This is NOT an engine (D8).</b> It produces no plan, orders nothing, computes no distance/duration, advances
/// no content stage, closes no visit. Frequency is READ (how often), consent is ASKED (may we contact), the journey
/// stage is BOUND (optional) — but none is computed and none of their record payload is copied (D5).</para>
/// <para><b>Rich foundation, storage only (2026-08-29).</b> The embedded <see cref="Slot"/> (motor-filled sequence/slot,
/// D12), <see cref="Content"/> (content-position provenance, D10), <see cref="Selection"/> (selection origin, D11) and
/// <see cref="Availability"/> (per-contact availability snapshot, D13) blocks are born NULL/empty and are never populated
/// or calculated by FU01 — later FUs (FU03 route optimizer, FU05 packing motor, FU04 content execution) write them.
/// <see cref="PlannedDurationMinutes"/> is stored-not-computed (D14).</para>
/// <para><b>Time.</b> <see cref="PlannedDate"/> is the single time axis (D1) and is a <see cref="DateOnly"/> on purpose:
/// a second co-sorted DateTimeOffset field trips the "cannot sort with keys that are parallel arrays" 500. The optional
/// wall-clock window (<see cref="PlannedStartTime"/> / <see cref="PlannedEndTime"/>) is a string "HH:mm", not an
/// instant.</para>
/// <para>Tenant-owned (<see cref="EntityBase"/>); TenantId is server-resolved and never accepted from a payload.</para>
/// </summary>
public sealed class PlannedVisit : EntityBase
{
    // ── Identity + target (WHO) ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Stable business key, unique within the tenant among non-archived (and non-deleted) rows. Never renamed.</summary>
    public string VisitCode { get; set; } = string.Empty;

    /// <summary><see cref="PlannedVisitTargetType"/> — account / contact / account-contact-link / pharmacy.</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>Resolved by <see cref="TargetType"/>. <c>Guid.Empty</c> is forbidden. For <c>pharmacy</c> it is the id of
    /// an Account whose account-type is <c>pharmacy</c> (a pharmacy is a first-class Account, D9).</summary>
    public Guid TargetId { get; set; }

    /// <summary>Navigation copy, DERIVED, never client-supplied: <c>account</c>/<c>pharmacy</c> → TargetId;
    /// <c>account-contact-link</c> → the link's AccountId.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>Derived: <c>contact</c> → TargetId; <c>account-contact-link</c> → the link's ContactId.</summary>
    public Guid? ContactId { get; set; }

    /// <summary>Derived, only for the <c>account-contact-link</c> target (= TargetId).</summary>
    public Guid? AccountContactLinkId { get; set; }

    // ── Time (WHEN) — the plan window IS the effective window (D1) ────────────────────────────────────────────────────

    /// <summary>The planned day. The FU's SINGLE time axis (D1); a <see cref="DateOnly"/> to avoid the parallel-arrays
    /// 500 (§19.3/2).</summary>
    public DateOnly PlannedDate { get; set; }

    /// <summary>Optional local wall-clock start, "HH:mm". Given together with <see cref="PlannedEndTime"/> or both empty.</summary>
    public string? PlannedStartTime { get; set; }

    /// <summary>Optional local wall-clock end, "HH:mm"; strictly after <see cref="PlannedStartTime"/>.</summary>
    public string? PlannedEndTime { get; set; }

    /// <summary>Optional planned duration in minutes (> 0). <b>Computed elsewhere (D14)</b> — FU01 only STORES a manual
    /// override; it never derives it from content.</summary>
    public int? PlannedDurationMinutes { get; set; }

    // ── Resource (WHO VISITS) — embedded value object (D4) ───────────────────────────────────────────────────────────

    /// <summary>The assigned field resource. <see cref="PlannedVisitResourceRef.ResourceId"/> is a STRING (no fake FK).</summary>
    public PlannedVisitResourceRef Resource { get; set; } = new();

    /// <summary>Snapshot of the covering Position's code (audit/display); the Position master is never copied.</summary>
    public string? PositionCode { get; set; }

    /// <summary>Snapshot of the covering Position id; carried, not validated in this FU.</summary>
    public Guid? PositionId { get; set; }

    // ── Purpose (WHY) ────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary><see cref="PlannedVisitPurpose"/>. Deterministically mapped to the MOD-0164 consent Purpose.</summary>
    public string VisitPurpose { get; set; } = string.Empty;

    /// <summary><see cref="PlannedVisitType"/> — field-visit / remote-visit / phone / digital-detailing / event.</summary>
    public string VisitType { get; set; } = string.Empty;

    public string? Objective { get; set; }
    public string? Notes { get; set; }

    // ── Context keys (WHERE / WHICH) — reference-only, never mutated ──────────────────────────────────────────────────

    public string? BusinessUnit { get; set; }
    public Guid? TerritoryNodeId { get; set; }
    public Guid? TerritoryModelId { get; set; }

    /// <summary>Context key only — no campaign-target CRUD / cycle math / campaign result here.</summary>
    public Guid? CampaignId { get; set; }

    // ── Lifecycle + provenance ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary><see cref="PlannedVisitStatus"/> — draft / planned / confirmed / cancelled / archived.</summary>
    public string PlanStatus { get; set; } = PlannedVisitStatus.Draft;

    /// <summary><see cref="PlannedVisitSource"/> — FU01 writes only <c>manual</c>; the rest are reserved.</summary>
    public string Source { get; set; } = PlannedVisitSource.Manual;

    /// <summary>Required on the <c>cancelled</c> transition; not a create/edit field.</summary>
    public string? CancellationReason { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // ── Embedded storage-only blocks (born null/empty; FU01 never computes them) ─────────────────────────────────────

    /// <summary>Motor-filled sequence/slot (D12): born null, written by FU03/FU05.</summary>
    public PlannedVisitScheduleSlot Slot { get; set; } = new();

    /// <summary>Frequency provenance (D5) — DERIVED at write time from the MOD-0165 resolver, never authored.</summary>
    public PlannedVisitFrequencyProvenance? Frequency { get; set; }

    /// <summary>Consent provenance (D5/D6) — DERIVED at write time from the MOD-0164 evaluator, never authored.</summary>
    public PlannedVisitConsentProvenance? Consent { get; set; }

    /// <summary>Content-position, the SINGLE source of truth (D10): derive-default from strategy + manual override.</summary>
    public PlannedVisitContentRef? Content { get; set; }

    /// <summary>Selection origin (D11) — DERIVED snapshot, SelectionMode is always <c>manual</c> in FU01.</summary>
    public PlannedVisitSelectionProvenance? Selection { get; set; }

    /// <summary>Per-contact availability snapshot at plan time (D13). A WARNING signal in FU01, not a hard block.</summary>
    public PlannedVisitAvailabilitySnapshot? Availability { get; set; }

    // ── Lifecycle helpers ────────────────────────────────────────────────────────────────────────────────────────────

    public bool IsDraft() => string.Equals(PlanStatus, PlannedVisitStatus.Draft, StringComparison.Ordinal);
    public bool IsPlanned() => string.Equals(PlanStatus, PlannedVisitStatus.Planned, StringComparison.Ordinal);
    public bool IsConfirmed() => string.Equals(PlanStatus, PlannedVisitStatus.Confirmed, StringComparison.Ordinal);
    public bool IsCancelled() => string.Equals(PlanStatus, PlannedVisitStatus.Cancelled, StringComparison.Ordinal);
    public bool IsArchived() => string.Equals(PlanStatus, PlannedVisitStatus.Archived, StringComparison.Ordinal);

    /// <summary>An ACTIVE plan (planned/confirmed) holds a slot — the only rows the overlap + same-day-type guards
    /// consider. draft/cancelled/archived rows never block another plan.</summary>
    public bool IsActivePlan() => IsPlanned() || IsConfirmed();

    /// <summary>The plan window IS the effective window (D1): is this plan effective on the given day?</summary>
    public bool IsEffectiveOn(DateOnly date) => PlannedDate == date;
}

/// <summary>The assigned field resource (D4). <see cref="ResourceId"/> is a STRING because ERP-vNext has no CRM-validated
/// Person/Employee master (MOD-0288 reserved) — a Guid here would be a dead FK. Same pattern as MOD-0151
/// <c>TerritoryResourceRef</c>.</summary>
public sealed class PlannedVisitResourceRef
{
    /// <summary>The id in the owning master (MOD-0288 Person / MOD-0018 User / HCM Employee). NOT validated (D4).</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary><see cref="PlannedVisitResourceTypes"/> — person / user / employee.</summary>
    public string ResourceType { get; set; } = string.Empty;

    /// <summary>Display snapshot only; never a query/match key.</summary>
    public string? DisplayName { get; set; }
}

/// <summary>Motor-filled sequence/slot (D12). Null-born in FU01; FU03/FU05 populate it. FU01 never computes/sorts it.</summary>
public sealed class PlannedVisitScheduleSlot
{
    /// <summary>Packed visit order within the day/route (legacy <c>Order</c>). Neutral name (NOT <c>RouteOrder</c>).</summary>
    public int? SequenceOrder { get; set; }

    /// <summary>Packer-assigned "HH:mm" start on <see cref="PlannedVisit.PlannedDate"/> (never DateTimeOffset).</summary>
    public string? SlotStartTime { get; set; }

    /// <summary>Packer-assigned "HH:mm" end; strictly after <see cref="SlotStartTime"/>. Not the manual intent window.</summary>
    public string? SlotEndTime { get; set; }

    /// <summary>True only once a motor has actually packed a slot into this row (all three are set by that motor).</summary>
    public bool IsPacked => SequenceOrder is not null || !string.IsNullOrWhiteSpace(SlotStartTime);
}

/// <summary>Frequency provenance (D5): the decision + matched id + version + time, never the policy's record payload.</summary>
public sealed class PlannedVisitFrequencyProvenance
{
    public string FrequencyStatus { get; set; } = "unknown";
    public Guid? SelectedFrequencyPolicyId { get; set; }
    public string? SelectedPolicyCode { get; set; }
    public string? SelectedPolicyName { get; set; }
    public string? FrequencyType { get; set; }
    public int? RequiredVisitCount { get; set; }
    public string? PeriodType { get; set; }
    public string? SelectionReason { get; set; }
    public List<string> ReasonCodes { get; set; } = new();
    public DateTimeOffset ResolvedAt { get; set; }
}

/// <summary>Consent provenance (D5/D6): the verdict + matched ids + evaluator version + time, never the consent payload.</summary>
public sealed class PlannedVisitConsentProvenance
{
    /// <summary>D6 guard — when <c>false</c>, no eligibility inference may be drawn from this row.</summary>
    public bool FilterApplied { get; set; }

    public string EligibilityStatus { get; set; } = "unknown";
    public string Decision { get; set; } = string.Empty;
    public string Channel { get; set; } = "visit";
    public string Purpose { get; set; } = string.Empty;
    public Guid? MatchedConsentId { get; set; }
    public List<Guid> MatchedPreferenceIds { get; set; } = new();
    public List<string> ReasonCodes { get; set; } = new();
    public string SelectionReason { get; set; } = string.Empty;
    public string EvaluatorVersion { get; set; } = string.Empty;
    public DateTimeOffset EvaluatedAt { get; set; }
}

/// <summary>Content-position, the SINGLE source of truth (D10). <see cref="JourneyId"/>/<see cref="StageId"/> are the
/// editable surface of form fields 26/27; the rest are derived. The journey's full stage content is never copied (D5).</summary>
public sealed class PlannedVisitContentRef
{
    public Guid? JourneyId { get; set; }
    public Guid? StageId { get; set; }

    /// <summary>Ordinal position of the stage on the journey's ordered path — read by FU04 "next stage". FU01 never advances it.</summary>
    public int? StageIndex { get; set; }

    public string? StageCode { get; set; }

    /// <summary><see cref="PlannedVisitContentSource"/> — <c>strategy</c> (default-fill) or <c>manual</c> (rep override).</summary>
    public string ContentSource { get; set; } = PlannedVisitContentSource.Manual;

    /// <summary>Equivalent flag to <c>ContentSource == manual</c>: was the strategy default overridden by the rep?</summary>
    public bool IsOverridden { get; set; }

    /// <summary>The play (MOD-0167-FU04) that produced the default. Snapshot — NOT validated (D4); kept even after override.</summary>
    public Guid? StrategyTemplateId { get; set; }

    public string? JourneyDisplayName { get; set; }
    public string? StageDisplayName { get; set; }
    public DateTimeOffset ResolvedAt { get; set; }
}

/// <summary>Selection origin (D11): a snapshot of where this target was selected from. Segment FILTERS, selection is
/// MANUAL. None of these ids is validated or opened as an FK.</summary>
public sealed class PlannedVisitSelectionProvenance
{
    public Guid? SegmentId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? StrategyTemplateId { get; set; }

    /// <summary><see cref="PlannedVisitSelectionMode"/> — FU01 always writes <c>manual</c>.</summary>
    public string SelectionMode { get; set; } = PlannedVisitSelectionMode.Manual;

    public DateTimeOffset DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
}

/// <summary>Per-contact availability snapshot at plan time (D13). A WARNING in FU01, never a hard block (§12.5). Read
/// from MOD-0150 <c>ContactAvailability</c>; FU01 never computes availability.</summary>
public sealed class PlannedVisitAvailabilitySnapshot
{
    public string? Weekday { get; set; }
    public string? AvailableStartTime { get; set; }
    public string? AvailableEndTime { get; set; }
    public bool? AppointmentRequired { get; set; }

    /// <summary>Does the planned window fit the availability? FU01 = advisory only.</summary>
    public bool? WithinAvailableWindow { get; set; }

    public List<string> ReasonCodes { get; set; } = new();
    public DateTimeOffset CapturedAt { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
// In-domain, fail-closed vocabularies (D2). Sets live here (FU02/FU03/FU04/FU05 precedent); an out-of-set value → 400;
// a hardcoded fallback list is forbidden — every dropdown is fed from the contract endpoint.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Who is being visited. <c>pharmacy</c> is a first-class target (D9): a pharmacy is an Account whose
/// account-type is <c>pharmacy</c>, and its consent is asked with SubjectType=account.</summary>
public static class PlannedVisitTargetType
{
    public const string Account = "account";
    public const string Contact = "contact";
    public const string AccountContactLink = "account-contact-link";
    public const string Pharmacy = "pharmacy";

    public static readonly IReadOnlyList<string> All = new[] { Account, Contact, AccountContactLink, Pharmacy };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

public static class PlannedVisitPurpose
{
    public const string MedicalVisit = "medical-visit";
    public const string ProductInformation = "product-information";
    public const string Training = "training";
    public const string FollowUp = "follow-up";
    public const string Campaign = "campaign";
    public const string Service = "service";
    public const string Compliance = "compliance";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        MedicalVisit, ProductInformation, Training, FollowUp, Campaign, Service, Compliance, Other
    };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

public static class PlannedVisitType
{
    public const string FieldVisit = "field-visit";
    public const string RemoteVisit = "remote-visit";
    public const string Phone = "phone";
    public const string DigitalDetailing = "digital-detailing";
    public const string Event = "event";

    public static readonly IReadOnlyList<string> All = new[]
    {
        FieldVisit, RemoteVisit, Phone, DigitalDetailing, Event
    };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Plan lifecycle (§12.2). <c>archived</c> is terminal.</summary>
public static class PlannedVisitStatus
{
    public const string Draft = "draft";
    public const string Planned = "planned";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Planned, Confirmed, Cancelled, Archived };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>How a plan was born. FU01 writes only <c>manual</c>; the rest are reserved for FU03 / F-IMPORT / F-MIG.</summary>
public static class PlannedVisitSource
{
    public const string Manual = "manual";
    public const string Campaign = "campaign";
    public const string RoutePlan = "route-plan";
    public const string Import = "import";
    public const string Migration = "migration";

    public static readonly IReadOnlyList<string> All = new[] { Manual, Campaign, RoutePlan, Import, Migration };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Provenance-only selection mode (D11). FU01 writes only <c>manual</c>; <c>recommended</c>/<c>targeted</c> are
/// reserved for the FU05 motor. NOT a form dropdown.</summary>
public static class PlannedVisitSelectionMode
{
    public const string Manual = "manual";
    public const string Recommended = "recommended";
    public const string Targeted = "targeted";

    public static readonly IReadOnlyList<string> All = new[] { Manual, Recommended, Targeted };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);
}

/// <summary>ContentRef marker (D10): was the content position a strategy default-fill or a rep override?</summary>
public static class PlannedVisitContentSource
{
    public const string Strategy = "strategy";
    public const string Manual = "manual";

    public static readonly IReadOnlyList<string> All = new[] { Strategy, Manual };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Which master a <see cref="PlannedVisitResourceRef.ResourceId"/> belongs to. In-domain (structural).</summary>
public static class PlannedVisitResourceTypes
{
    public const string Person = "person";
    public const string User = "user";
    public const string Employee = "employee";

    public static readonly IReadOnlyList<string> All = new[] { Person, User, Employee };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Availability snapshot reason codes (D13) — same words as MOD-0151 <c>TerritoryReadinessReasonCodes</c>.</summary>
public static class PlannedVisitAvailabilityReasonCodes
{
    public const string OutsidePreferredWindow = "outside_preferred_window";
    public const string AppointmentRequired = "appointment_required";
    public const string ContactNotAvailableOnDay = "contact_not_available_on_day";
}

/// <summary>Published ceilings for the write path, so a UI needs no hardcoded limit.</summary>
public static class PlannedVisitLimits
{
    public const int MaxVisitCodeLength = 64;
    public const int MaxResourceIdLength = 128;
    public const int MaxDisplayNameLength = 200;
    public const int MaxPositionCodeLength = 60;
    public const int MaxBusinessUnitLength = 60;
    public const int MaxObjectiveLength = 1000;
    public const int MaxNotesLength = 2000;
    public const int MaxCancellationReasonLength = 500;
    public const int MinDurationMinutes = 1;
    public const int MaxDurationMinutes = 1440;
}
