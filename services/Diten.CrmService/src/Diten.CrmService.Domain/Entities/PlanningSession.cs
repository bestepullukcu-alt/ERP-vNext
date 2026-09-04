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

    /// <summary>Chosen plan week's Monday (yyyy-MM-dd). Persisted from Create/Edit so Details/Edit resolve the saved week.</summary>
    public string? TargetWeekStart { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // ── Lifecycle helpers ────────────────────────────────────────────────────────────────────────────────────────────

    public bool IsDraft() => string.Equals(Status, PlanningSessionStatus.Draft, StringComparison.Ordinal);
    public bool IsGenerated() => string.Equals(Status, PlanningSessionStatus.Generated, StringComparison.Ordinal);
    public bool IsCommitted() => string.Equals(Status, PlanningSessionStatus.Committed, StringComparison.Ordinal);
    public bool IsArchived() => string.Equals(Status, PlanningSessionStatus.Archived, StringComparison.Ordinal);
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
