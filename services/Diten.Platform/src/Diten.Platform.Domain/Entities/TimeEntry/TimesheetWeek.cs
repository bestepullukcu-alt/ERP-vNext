using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.TimeEntry;

namespace Diten.Platform.Domain.Entities.TimeEntry;

/// <summary>
/// MOD-0280-FU01 (pack §4.3, D5, D6) — one revision of one person's ISO week. Revision 1 is the week itself; a
/// correction of an approved week is a NEW row with <see cref="CorrectionOfRevision"/> set, and the approved row it
/// corrects stays in force (<see cref="InForce"/>) until the correction is approved (the MeetingMinutesVersion
/// pattern: append-only, reason required).
///
/// <para><b>Two storage-level rules</b> (see the schema manifest): exactly one in-force revision per
/// (tenant, user, week), and at most one OPEN revision (Draft or Submitted) per (tenant, user, week). Both are
/// partial unique indexes over the two booleans below, so "two open corrections" cannot exist even under a race.</para>
///
/// <para>Business revision is <see cref="RevisionNumber"/>; <see cref="BaseEntity.Version"/> stays the concurrency
/// token (module-pack-standard §14).</para>
/// </summary>
public sealed class TimesheetWeek : TenantScopedEntity
{
    public required Guid UserId { get; set; }

    /// <summary>ISO 8601 week, e.g. <c>2026-W40</c>.</summary>
    public required string WeekKey { get; set; }

    /// <summary>The Monday of <see cref="WeekKey"/>.</summary>
    public required DateOnly WeekStartDate { get; set; }

    /// <summary>The tenant zone the week was first written in (R3: the tenant's zone, v1).</summary>
    public required string TimeZoneId { get; set; }

    public required int RevisionNumber { get; set; }

    public TimesheetWeekStatus Status { get; set; } = TimesheetWeekStatus.Draft;

    /// <summary>True on exactly one approved revision per (tenant, user, week) — partial unique index.</summary>
    public bool InForce { get; set; }

    /// <summary>True while the revision is Draft or Submitted — partial unique index: one open revision per week.
    /// Kept as a stored flag (not a status $in) because a partial filter must be an equality the index can match.</summary>
    public bool IsOpen { get; set; } = true;

    public int? CorrectionOfRevision { get; set; }

    public string? CorrectionReason { get; set; }

    /// <summary>Snapshot of the primary seat's legal entity at submit (§20 per-entity retention later).</summary>
    public Guid? LegalEntityId { get; set; }

    /// <summary>Resolved and stored at submit (D6); never contains <see cref="UserId"/>.</summary>
    public List<Guid> ApproverCandidateUserIds { get; set; } = [];

    public TimesheetApproverResolution? ApproverResolution { get; set; }

    /// <summary>The ONE person MOD-0023 actually assigned the approval to (its first candidate after its own
    /// resolution). The approvals list and the read-only week are open to this person only: the other candidates
    /// cannot see or decide the approval in MOD-0023, so they do not see the week here either. Multi-candidate pools,
    /// claim and delegation are a MOD-0023 follow-up (pack §20).</summary>
    public Guid? AssignedApproverUserId { get; set; }

    /// <summary>The MOD-0023 instance of the CURRENT submission. Cleared on withdraw/reject; the previous id stays in
    /// the audit trail.</summary>
    public Guid? WorkflowInstanceId { get; set; }

    /// <summary>How many times this revision has been submitted. Part of the MOD-0023 idempotency key, so a
    /// withdrawn or rejected revision that is submitted again opens a NEW approval instead of being handed back the
    /// old, closed one.</summary>
    public int SubmissionCount { get; set; }

    /// <summary>Days whose total exceeds the 660-minute flag threshold (A3) — shown to the approver.</summary>
    public List<DateOnly> FlaggedDates { get; set; } = [];

    public DateTimeOffset? SubmittedAtUtc { get; set; }

    /// <summary><see cref="SubmittedAtUtc"/> as UTC ticks — what lists SORT by. A DateTimeOffset is stored as a
    /// <c>[ticks, offset]</c> array, and Mongo does not order arrays by their first element (BL-030).</summary>
    public long? SubmittedAtUtcTicks { get; set; }
    public Guid? SubmittedByUserId { get; set; }

    public DateTimeOffset? WithdrawnAtUtc { get; set; }

    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }

    public DateTimeOffset? LastRejectedAtUtc { get; set; }
    public Guid? LastRejectedByUserId { get; set; }
    public string? LastRejectionReason { get; set; }

    public DateTimeOffset? ReopenedAtUtc { get; set; }
    public Guid? ReopenedByUserId { get; set; }
    public string? ReopenReason { get; set; }

    /// <summary>A time-admin reopen of a week older than the edit window (A9). It holds until the week is APPROVED, not
    /// merely submitted: a reopened week that is rejected must stay editable, or the person could never fix it.</summary>
    public bool ReopenActive { get; set; }

    public DateTimeOffset? SupersededAtUtc { get; set; }

    /// <summary>F12 — set once the approved revision's task totals were written. Approved with this still null means the
    /// finalizer wrote the week and then failed on the totals: the next read, approvals page or sweep recomputes them.
    /// Never read as "approved" — <see cref="Status"/> says that.</summary>
    public DateTimeOffset? TotalsAppliedAtUtc { get; set; }

    /// <summary>Set when the finalizer refused a MOD-0023 outcome — today only a decision made by the person about
    /// their OWN week (pack §13). The week goes back to Draft and this code tells the person why.</summary>
    public string? FinalizationBlockedReason { get; set; }

    /// <summary>Cached at submit.</summary>
    public int TotalMinutes { get; set; }
}
