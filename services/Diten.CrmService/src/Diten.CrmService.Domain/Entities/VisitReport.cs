namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0155 FU02 — <b>VisitReport</b>: the immutable record of an EXECUTED visit. It is the execution counterpart of the
/// FU01 <see cref="PlannedVisit"/> plan atom — a <i>separate</i> aggregate (D-REPORT-PERSISTENCE = A) linked to the plan
/// by <see cref="PlannedVisitId"/>, so the plan stays a plan and the report is the compliance record of what actually
/// happened (FU01 §2.3: plan and execution are NOT one document).
/// <para><b>This is NOT an engine (D8).</b> FU02 computes no schedule, no route, no capacity and — critically — <b>no next
/// content stage</b> (that arithmetic is FU04's <c>nextIndex = prior + 1</c>). It RECORDS the ACTUAL content presented
/// (incl. the actual <see cref="VisitReportContentActuals.StageIndex"/>, the value that closes the loop, §4.4) and writes
/// NO advanced cursor onto the plan atom (D-STAGE-ADVANCE = B). The FU05-side read switch is F-STAGE-READ, out of scope.</para>
/// <para><b>Immutability (pharma compliance, D-EDIT-WINDOW).</b> A submitted report is editable only for a short
/// correction window; after that it is immutable in place and corrections are append-only <see cref="Amendments"/> —
/// never a silent in-place edit. The <see cref="ReportStatus"/> machine is <c>draft → submitted → amended</c>, no reverse.</para>
/// <para><b>Time.</b> <see cref="ExecutedAt"/> is a lone <see cref="DateTimeOffset"/> (the execution instant); it is never
/// co-sorted with a second DateTimeOffset — that is the CRM parallel-arrays 500. Any date pairing (e.g. a reschedule
/// intent) uses a <see cref="DateOnly"/> like FU01's <c>PlannedDate</c>.</para>
/// <para>Tenant-owned (<see cref="EntityBase"/>); TenantId is server-resolved and never accepted from a payload.</para>
/// </summary>
public sealed class VisitReport : EntityBase
{
    /// <summary>The FU01 <see cref="PlannedVisit"/> atom this report executes. 1:1 — one report per visit; corrections are
    /// append-only <see cref="Amendments"/>, never a second report.</summary>
    public Guid PlannedVisitId { get; set; }

    /// <summary><see cref="VisitExecutionOutcome"/> — <c>completed</c> · <c>missed</c> · <c>rescheduled</c>
    /// (<c>cancelled</c> stays FU01's existing command). The report-side source of truth (D-EXECUTION-STATUS = A).</summary>
    public string ExecutionOutcome { get; set; } = string.Empty;

    /// <summary><see cref="VisitReportStatus"/> — <c>draft</c> · <c>submitted</c> · <c>amended</c> (§12; no reverse).</summary>
    public string ReportStatus { get; set; } = VisitReportStatus.Draft;

    /// <summary>The ACTUAL content presented (may differ from the FU04-planned stage). Born null until a completed visit's
    /// report is filled; carries the actual <see cref="VisitReportContentActuals.StageIndex"/> that FU04/FU05 read next
    /// cycle (§4.4).</summary>
    public VisitReportContentActuals? ContentActuals { get; set; }

    /// <summary>Samples / materials handed over. The item type is reference-data-driven (F-RD).</summary>
    public List<VisitReportSample> Samples { get; set; } = new();

    /// <summary>Doctor feedback + outcome code (ref-data) + follow-up flag. Null on a missed/rescheduled report.</summary>
    public VisitReportFeedback? Feedback { get; set; }

    /// <summary>A reason code for a <c>missed</c>/<c>rescheduled</c> outcome (in-domain, §4.1 ③). Null for completed.</summary>
    public string? ReasonCode { get; set; }

    /// <summary>The new intended day when the outcome is <c>rescheduled</c> — a <see cref="DateOnly"/> (never a second
    /// co-sorted DateTimeOffset). The ACTUAL re-plan write stays FU05's job; FU02 only captures the intent.</summary>
    public DateOnly? RescheduleToDate { get; set; }

    public string? RescheduleNotes { get; set; }

    /// <summary>The reporting rep (FU01 <see cref="PlannedVisitResourceRef.ResourceId"/> shape — a STRING, no fake FK;
    /// MOD-0288 owns the Person/Position master).</summary>
    public string ReportedByResourceId { get; set; } = string.Empty;

    /// <summary>When the visit actually happened (the execution instant). A lone DateTimeOffset — never co-sorted.</summary>
    public DateTimeOffset ExecutedAt { get; set; }

    /// <summary>Compliance timestamps (D-EDIT-WINDOW).</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    public DateTimeOffset? AmendedAt { get; set; }

    /// <summary>Append-only corrections after the edit window (D-EDIT-WINDOW). The original stays intact.</summary>
    public List<VisitReportAmendment> Amendments { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // ── Lifecycle helpers ────────────────────────────────────────────────────────────────────────────────────────────

    public bool IsDraft() => string.Equals(ReportStatus, VisitReportStatus.Draft, StringComparison.Ordinal);
    public bool IsSubmitted() => string.Equals(ReportStatus, VisitReportStatus.Submitted, StringComparison.Ordinal);
    public bool IsAmended() => string.Equals(ReportStatus, VisitReportStatus.Amended, StringComparison.Ordinal);

    /// <summary>A submitted-or-amended report is the compliance record (immutable in place after the edit window).</summary>
    public bool IsFinalised() => IsSubmitted() || IsAmended();

    public bool IsCompleted() => string.Equals(ExecutionOutcome, VisitExecutionOutcome.Completed, StringComparison.Ordinal);

    /// <summary>Is this finalised report still inside its correction window (D-EDIT-WINDOW)? Beyond it an in-place edit is
    /// refused and a correction must be an append-only amendment.</summary>
    public bool IsWithinEditWindow(DateTimeOffset now)
        => SubmittedAt is { } submitted
           && now - submitted <= TimeSpan.FromMinutes(VisitReportLimits.EditWindowMinutes);
}

/// <summary>The ACTUAL content presented at a completed visit (may differ from the FU04-planned stage). The
/// <see cref="StageIndex"/> is the loop-closing value (§4.4): FU04/FU05 read the last COMPLETED report's StageIndex as
/// <c>PriorStageIndex</c> next cycle. Ids are snapshots — never validated or opened as FKs (FU01 D5 precedent).</summary>
public sealed class VisitReportContentActuals
{
    public Guid? JourneyId { get; set; }
    public Guid? StageId { get; set; }

    /// <summary>Ordinal position of the stage ACTUALLY presented. The value that advances the sequence next cycle (§4.4).</summary>
    public int? StageIndex { get; set; }

    public string? StageCode { get; set; }

    /// <summary>Did the actual presented stage match the FU04-planned stage? <c>false</c> means the rep diverged.</summary>
    public bool MatchedPlan { get; set; }

    public string? JourneyDisplayName { get; set; }
    public string? StageDisplayName { get; set; }
}

/// <summary>One sample / material handed over. <see cref="ItemType"/> is reference-data-driven (F-RD) — validated as a
/// non-empty bounded string, NOT against a hardcoded fallback list.</summary>
public sealed class VisitReportSample
{
    public string ItemType { get; set; } = string.Empty;

    /// <summary>Optional pointer to the item's master row (a product / SKU). Snapshot only — not validated as an FK.</summary>
    public Guid? ItemId { get; set; }

    public int Quantity { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Doctor feedback + the outcome code (ref-data) + the follow-up flag.</summary>
public sealed class VisitReportFeedback
{
    public string? DoctorFeedback { get; set; }

    /// <summary>The visit outcome code — reference-data-driven (F-RD), not a hardcoded enum.</summary>
    public string OutcomeCode { get; set; } = string.Empty;

    public bool FollowUpRequired { get; set; }
    public string? FollowUpNotes { get; set; }
}

/// <summary>An append-only correction after the edit window (D-EDIT-WINDOW). Records who/when/why and which fields
/// changed; the original report data is preserved — an amendment never silently rewrites history.</summary>
public sealed class VisitReportAmendment
{
    public DateTimeOffset At { get; set; }
    public string ByResourceId { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public List<string> ChangedFields { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────
// In-domain, fail-closed vocabularies. Sets live here (FU01 precedent); an out-of-set value → 400; a hardcoded fallback
// list is forbidden — every dropdown is fed from the contract endpoint. Outcome codes + sample/material types are the
// exception: they are REFERENCE-DATA-driven (MOD-0048, F-RD), so they are bounded-string validated, never enum-checked.
// ─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The execution outcome recorded on the report (D-EXECUTION-STATUS = A). <c>cancelled</c> is NOT here — it stays
/// FU01's existing command, so FU02 never touches the plan's PlanStatus machine.</summary>
public static class VisitExecutionOutcome
{
    public const string Completed = "completed";
    public const string Missed = "missed";
    public const string Rescheduled = "rescheduled";

    public static readonly IReadOnlyList<string> All = new[] { Completed, Missed, Rescheduled };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Report lifecycle (§12). <c>amended</c> is reached only by an append-only correction; there is no reverse.</summary>
public static class VisitReportStatus
{
    public const string Draft = "draft";
    public const string Submitted = "submitted";
    public const string Amended = "amended";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Submitted, Amended };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>In-domain reason codes for a <c>missed</c> / <c>rescheduled</c> outcome (§4.1 ③). Fail-closed like FU01's
/// vocabularies; an out-of-set value on a non-completed outcome is refused (400).</summary>
public static class VisitReportReasonCodes
{
    public const string DoctorUnavailable = "doctor_unavailable";
    public const string ClinicClosed = "clinic_closed";
    public const string RepUnavailable = "rep_unavailable";
    public const string RescheduledByDoctor = "rescheduled_by_doctor";
    public const string RescheduledByRep = "rescheduled_by_rep";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        DoctorUnavailable, ClinicClosed, RepUnavailable, RescheduledByDoctor, RescheduledByRep, Other
    };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();
}

/// <summary>Published ceilings for the write path, so a UI needs no hardcoded limit.</summary>
public static class VisitReportLimits
{
    public const int MaxResourceIdLength = 128;
    public const int MaxOutcomeCodeLength = 80;
    public const int MaxSampleItemTypeLength = 80;
    public const int MaxStageCodeLength = 80;
    public const int MaxDisplayNameLength = 200;
    public const int MaxFeedbackLength = 4000;
    public const int MaxNotesLength = 2000;
    public const int MaxReasonLength = 500;
    public const int MaxSamples = 100;
    public const int MinSampleQuantity = 1;
    public const int MaxSampleQuantity = 100000;

    /// <summary>The correction window (D-EDIT-WINDOW): a submitted report may be edited in place for this many minutes;
    /// after it, corrections are append-only amendments.</summary>
    public const int EditWindowMinutes = 60;
}
