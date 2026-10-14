namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0155 FU05 — <b>PlanningSession</b>: the thin staging aggregate of the MicroTarget Visit Planning Engine
/// (D-PERSISTENCE = C, LOCKED). It holds a rep's <i>selection</i> + last <i>generation state</i> + <i>provenance</i>
/// for one MOD-0165 CyclePeriod — <b>not the schedule itself</b>. The real plan lives as FU01
/// <see cref="PlannedVisit"/> atoms after apply; this record only links to them through
/// <see cref="CommittedPlannedVisitIds"/>, so there is never a second source-of-truth for the plan. Legacy
/// <c>TempClient</c> staging is the direct precedent.
/// <para><b>This is NOT an engine and NOT a schedule store.</b> It computes nothing, packs nothing and stores no slot.
/// The transient supply-vs-demand summary is NEVER persisted here (D-SUPPLY-DEMAND-SHAPE = A) — only a coarse
/// <see cref="PlanningSessionGenerationState.SupplyDemandStatus"/> flag.</para>
/// <para>Tenant-owned (<see cref="EntityBase"/>); TenantId is server-resolved and never accepted from a payload.
/// Weeks are DERIVED from the CyclePeriod calendar (D-WEEK-MODEL = A) — no week rows are stored.</para>
/// </summary>
public sealed class PlanningSession : EntityBase
{
    /// <summary>The MOD-0165 CyclePeriod this session plans (D-PERIOD-MODEL = A). Weeks are derived, not stored.</summary>
    public Guid CyclePeriodId { get; set; }

    /// <summary>The rep this plan is for — a STRING id (no fake FK; MOD-0288 owns the master), the FU01
    /// <see cref="PlannedVisitResourceRef.ResourceId"/> shape. Single-rep in v1 (D-MULTI-REP = A).</summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary><see cref="PlanningSessionResourceTypes"/> — person / user / employee. Carried onto every atom's Resource.</summary>
    public string ResourceType { get; set; } = PlanningSessionResourceTypes.Person;

    /// <summary>Display snapshot for the rep; never a query/match key.</summary>
    public string? ResourceDisplayName { get; set; }

    /// <summary><see cref="PlanningSessionStatus"/> — draft / generated / committed / archived (no reverse, §12).</summary>
    public string Status { get; set; } = PlanningSessionStatus.Draft;

    /// <summary>The manual selection (accounts + pharmacies + doctors, segment-filtered).</summary>
    public PlanningSessionSelection Selection { get; set; } = new();

    /// <summary>Last generation metadata — NOT the scheduled slots (those become FU01 atoms at apply).</summary>
    public PlanningSessionGenerationState GenerationState { get; set; } = new();

    /// <summary>Segment/campaign/strategy origin snapshot (never authored FKs — the consent MatchedConsentId precedent).</summary>
    public PlanningSessionProvenance Provenance { get; set; } = new();

    /// <summary>The FU01 atom ids written at apply — the link to the real schedule (provenance only; the atoms are the
    /// truth). Empty until the session is committed.</summary>
    public List<Guid> CommittedPlannedVisitIds { get; set; } = new();

    /// <summary>Optional MANUAL visiting sequence (target ids, first→last) chosen by the rep on Details. Persisted on
    /// apply so the committed plan reproduces the manual, constraint-honored order; empty ⇒ the engine optimum.</summary>
    public List<Guid> ManualVisitOrder { get; set; } = new();

    /// <summary>Chosen plan week's Monday (yyyy-MM-dd). Persisted from Create/Edit so Details/Edit resolve the saved week.
    /// WP-VP-3A — only "the week the screen opens on"; it no longer restricts generation (the horizon is the period).</summary>
    public string? TargetWeekStart { get; set; }

    /// <summary>
    /// WP-VP-3A (MK-3) — the period plan's STORED weeks: only weeks that were approved (and possibly reopened since). Every
    /// other week of the period is derived (past / draft / empty) and never stored. An older document has no such field and
    /// reads as an empty list.
    /// </summary>
    public List<PlanningWeek> Weeks { get; set; } = new();

    /// <summary>
    /// WP-VP-4E — the rep's day pins, per draft week (<see cref="PlanningDayPin.WeekStart"/>): "this visit / this
    /// institution goes on that day". Written through the existing selection update (no new command); the engine places
    /// the pinned visits first and spreads the rest around them. Kept when a week is approved or reopened. An older
    /// document has no such field and reads as an empty list.
    /// </summary>
    public List<PlanningDayPin> DayPins { get; set; } = new();

    /// <summary>
    /// WP-VP-4L (2) — the rep's per-week EXTRA visits (<see cref="PlanningWeekExtra.WeekStart"/>): "visit this target in
    /// this week too", over and above its frequency. Kept per week like the day pins (a draft week has no stored
    /// <see cref="PlanningWeek"/> record), written through the existing selection update (no new command). An older
    /// document has no such field and reads as an empty list.
    /// </summary>
    public List<PlanningWeekExtra> WeekExtras { get; set; } = new();

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // ── Lifecycle helpers ────────────────────────────────────────────────────────────────────────────────────────────

    public bool IsDraft() => string.Equals(Status, PlanningSessionStatus.Draft, StringComparison.Ordinal);
    public bool IsGenerated() => string.Equals(Status, PlanningSessionStatus.Generated, StringComparison.Ordinal);
    public bool IsCommitted() => string.Equals(Status, PlanningSessionStatus.Committed, StringComparison.Ordinal);
    public bool IsArchived() => string.Equals(Status, PlanningSessionStatus.Archived, StringComparison.Ordinal);

    /// <summary>WP-VP-3A — the stored week starting on <paramref name="weekStart"/> (yyyy-MM-dd), or null.</summary>
    public PlanningWeek? WeekOf(string weekStart)
        => Weeks.FirstOrDefault(w => string.Equals(w.WeekStart, weekStart, StringComparison.Ordinal));

    /// <summary>WP-VP-3A — at least one week is currently approved.</summary>
    public bool HasApprovedWeek() => Weeks.Any(w => w.IsApproved());

    /// <summary>WP-VP-3A (D3) — the plan has targets (doctors, accounts or pharmacies).</summary>
    public bool HasTargets()
        => Selection.SelectedContacts.Count > 0
           || Selection.SelectedAccountIds.Count > 0
           || Selection.SelectedPharmacyIds.Count > 0;
}

/// <summary>
/// WP-VP-3A (MK-3, MK-4) — one STORED week of a period plan: approved (its visits were written as PlannedVisit atoms and
/// are frozen) or reopened (approved once, then reopened with a reason). <see cref="WeekStart"/> is the Monday
/// (yyyy-MM-dd). The history keeps every approve / reopen with who, when and why.
/// </summary>
public sealed class PlanningWeek
{
    public string WeekStart { get; set; } = string.Empty;

    /// <summary><see cref="PlanningWeekStatus"/> — approved / reopened.</summary>
    public string Status { get; set; } = PlanningWeekStatus.Approved;

    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }

    /// <summary>The atoms this week's approvals wrote (a reopened week keeps the ids; their cancelled state is on the atom).</summary>
    public List<Guid> PlannedVisitIds { get; set; } = new();

    /// <summary>The manual visiting order the week was approved with (target ids); empty ⇒ the engine optimum.</summary>
    public List<Guid> ManualVisitOrder { get; set; } = new();

    public List<PlanningWeekHistoryEntry> History { get; set; } = new();

    public bool IsApproved() => string.Equals(Status, PlanningWeekStatus.Approved, StringComparison.Ordinal);
    public bool IsReopened() => string.Equals(Status, PlanningWeekStatus.Reopened, StringComparison.Ordinal);
    public bool IsLegacy() => string.Equals(Status, PlanningWeekStatus.Legacy, StringComparison.Ordinal);
}

/// <summary>WP-VP-3A (MK-4) — one approve / reopen of a week.</summary>
public sealed class PlanningWeekHistoryEntry
{
    public DateTimeOffset At { get; set; }
    public string? By { get; set; }

    /// <summary><see cref="PlanningWeekActions"/> — approve / reopen.</summary>
    public string Action { get; set; } = PlanningWeekActions.Approve;

    /// <summary>The reopen reason (required, ≥ 10 characters); null for an approve.</summary>
    public string? Reason { get; set; }
}

/// <summary>WP-VP-3A — the STORED week statuses (a week that was never approved is not stored).</summary>
public static class PlanningWeekStatus
{
    public const string Approved = "approved";
    public const string Reopened = "reopened";

    /// <summary>WP-VP-4A — READ-ONLY, never stored: a week of an old whole-period (<c>committed</c>) plan that holds its
    /// written visits. It reads as approved (frozen) with <c>storedStatus = legacy</c> and has no history.</summary>
    public const string Legacy = "legacy";
}

/// <summary>WP-VP-3A — the history actions of a week.</summary>
public static class PlanningWeekActions
{
    public const string Approve = "approve";
    public const string Reopen = "reopen";
}

/// <summary>WP-VP-3A — the DERIVED week status the screens show (past / approved / draft / empty).</summary>
public static class PlanningWeekDisplayStatus
{
    public const string Past = "past";
    public const string Approved = "approved";
    public const string Draft = "draft";
    public const string Empty = "empty";
}

/// <summary>The manual selection (§4.3a). Segment only FILTERED the universe; the pick is a human's.</summary>
public sealed class PlanningSessionSelection
{
    public List<Guid> SelectedAccountIds { get; set; } = new();
    public List<Guid> SelectedPharmacyIds { get; set; } = new();

    /// <summary>The manual doctor picks (segment-filtered). Each carries its owning account for coordinate + link context.</summary>
    public List<PlanningSessionSelectedContact> SelectedContacts { get; set; } = new();

    /// <summary>The segment applied as the eligible-universe filter (D-SEGMENT-FILTER). Never a hard membership store.</summary>
    public Guid? SegmentId { get; set; }

    public Guid? CampaignId { get; set; }
}

/// <summary>One manually-selected doctor. <see cref="AccountId"/> gives the clinic whose coordinates route the visit.</summary>
public sealed class PlanningSessionSelectedContact
{
    public Guid ContactId { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? AccountContactLinkId { get; set; }

    /// <summary>WP-VP-3C (K-7, S-4) — the rep's product pick for this doctor (≤ <see cref="PlanningSessionProductLimits.MaxPerDoctor"/>).
    /// Written through the existing selection update (null = keep, [] = clear); apply copies the resulting list onto the
    /// planned visits. Empty on a session written before 3C.</summary>
    public List<PlanningSessionSelectedProduct> Products { get; set; } = new();
}

/// <summary>WP-VP-3C — one product the rep picked for a doctor. <see cref="Role"/> is <c>promo</c> / <c>non-promo</c>;
/// null reads as promo (K-7d: MDM carries no role). <see cref="ProductCode"/> is a display snapshot.</summary>
public sealed class PlanningSessionSelectedProduct
{
    public Guid ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? Role { get; set; }
}

/// <summary>WP-VP-4E — one day pin of a draft week. <c>TargetType</c> / <c>TargetId</c> name the visit target (a doctor:
/// <c>contact</c> + the contact id; a pharmacy / an institution: its account id); <c>ContactId</c> is the doctor when the
/// target is one. <c>Scope</c>: <c>visit</c> = only that visit; <c>institution</c> = the target's institution group that
/// week (its doctors + linked pharmacies — the day balancer's group rule). <c>Date</c> yyyy-MM-dd inside the week.</summary>
public sealed class PlanningDayPin
{
    public string WeekStart { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid? ContactId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string Scope { get; set; } = PlanningDayPinScopes.Visit;

    /// <summary>WP-VW-W2 (BE-b) — a VISIT pin's start time ("HH:mm" on the 15-minute grid): the visit sits at that time of
    /// its day. Null = a day pin (4E), as every pin stored before it (an older pin reads without it).</summary>
    public string? StartTime { get; set; }
}

/// <summary>WP-VP-4L (2) — one extra visit of a draft week: the target (a doctor: <c>contact</c> + the contact id; a
/// pharmacy / an institution: its account id), the doctor and the institution it belongs to.</summary>
public sealed class PlanningWeekExtra
{
    public string WeekStart { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? AccountId { get; set; }
}

public static class PlanningDayPinScopes
{
    public const string Visit = "visit";
    public const string Institution = "institution";

    public static bool IsKnown(string? scope) => scope is Visit or Institution;
}

public static class PlanningSessionProductLimits
{
    public const int MaxPerDoctor = 20;
}

/// <summary>Last generation metadata (§4.3b). The full <c>SupplyDemandSummary</c> is TRANSIENT (recomputed on preview,
/// never persisted here, D-SUPPLY-DEMAND-SHAPE = A); only a coarse status flag is kept.</summary>
public sealed class PlanningSessionGenerationState
{
    public DateTimeOffset? LastGeneratedAt { get; set; }
    public int ScheduledCount { get; set; }
    public int UnscheduledCount { get; set; }

    /// <summary><see cref="PlanningSessionSupplyDemandStatus"/> — a coarse ok / over-planned flag only.</summary>
    public string? SupplyDemandStatus { get; set; }
}

/// <summary>Segment/campaign/strategy origin snapshot. None of these ids is validated or opened as an FK.</summary>
public sealed class PlanningSessionProvenance
{
    public Guid? SegmentId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? StrategyTemplateId { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
}

/// <summary>Staging lifecycle (§12). <c>archived</c> is terminal; there is NO reverse transition.</summary>
public static class PlanningSessionStatus
{
    public const string Draft = "draft";
    public const string Generated = "generated";
    public const string Committed = "committed";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Generated, Committed, Archived };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>The forward-only rank of a status; a transition is legal only when it strictly increases the rank
    /// (draft→generated→committed→archived), so <c>committed</c> can never return to <c>draft</c> (§12).</summary>
    public static int Rank(string? value) => Normalize(value) switch
    {
        Draft => 0,
        Generated => 1,
        Committed => 2,
        Archived => 3,
        _ => -1
    };

    /// <summary>May the session move from <paramref name="from"/> to <paramref name="to"/>? Only strictly-forward moves
    /// are allowed; re-generation (generated→generated) is allowed because a preview may be re-run before apply.</summary>
    public static bool CanTransition(string? from, string? to)
    {
        var fromRank = Rank(from);
        var toRank = Rank(to);
        if (fromRank < 0 || toRank < 0)
        {
            return false;
        }

        // generated→generated (re-preview) is allowed; every other same-rank / backward move is not.
        if (toRank == fromRank)
        {
            return string.Equals(Normalize(from), Generated, StringComparison.Ordinal);
        }

        return toRank > fromRank;
    }
}

/// <summary>Coarse supply-vs-demand flag stored on the session (the full summary is transient).</summary>
public static class PlanningSessionSupplyDemandStatus
{
    public const string Ok = "ok";
    public const string OverPlanned = "over-planned";
    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> All = new[] { Ok, OverPlanned, Unknown };
}

/// <summary>Which master a <see cref="PlanningSession.ResourceId"/> belongs to — mirrors FU01
/// <c>PlannedVisitResourceTypes</c> (person / user / employee).</summary>
public static class PlanningSessionResourceTypes
{
    public const string Person = "person";
    public const string User = "user";
    public const string Employee = "employee";

    public static readonly IReadOnlyList<string> All = new[] { Person, User, Employee };

    public static bool IsKnown(string? value)
        => value is not null && All.Contains(value.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return IsKnown(v) ? v : Person;
    }
}
