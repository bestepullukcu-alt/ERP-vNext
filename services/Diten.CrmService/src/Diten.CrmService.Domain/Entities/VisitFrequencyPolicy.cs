namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0165 FU03 — Visit Frequency / Call-Cycle Policy. Answers <b>one</b> question: "how often should this target be
/// visited?" (RequiredVisitCount per PeriodType). It deliberately does NOT answer "should it be visited today?",
/// "when was the last visit?", "due/overdue?", "which route/order?", "what to show?" or "is consent OK?" — those live
/// in MOD-0155 / MOD-0164 / MOD-0162 / MOD-0151 consumers.
/// <para>
/// This is its OWN aggregate (SoR = MOD-0165, co-authored by MOD-0167). Frequency is never a flat field on
/// Contact / Account / Campaign / KnowledgeContent — a target has many policies over time and by source, so a flat
/// field would collapse provenance and effective windows into one wrong value. Closing a policy is a soft
/// <see cref="FrequencyPolicyStatus.Archived"/> transition; there is no hard delete. <see cref="EntityBase.Id"/> is
/// the PolicyId and <see cref="PolicyCode"/> is the stable business key (rename is done through
/// <see cref="PolicyName"/> only).
/// </para>
/// </summary>
public sealed class VisitFrequencyPolicy : EntityBase
{
    /// <summary>Stable business key, unique per tenant among non-archived policies. Never renamed; display renaming
    /// is done through <see cref="PolicyName"/>.</summary>
    public string PolicyCode { get; set; } = string.Empty;

    public string PolicyName { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>What the policy targets (<see cref="FrequencyTargetType"/>). Governs specificity in resolution.</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>Identity of the target within <see cref="TargetType"/>. Never empty. For <c>segment</c> this is only
    /// stored as a target — membership is NEVER computed here (MOD-0167 boundary).</summary>
    public Guid TargetId { get; set; }

    /// <summary>Optional MOD-0048 business-unit context. When set, the resolve provider only selects the policy for a
    /// request carrying the same business unit.</summary>
    public string? BusinessUnit { get; set; }

    public Guid? TerritoryNodeId { get; set; }

    /// <summary>Provenance/context for a campaign-sourced policy (MOD-0165-FU02 boundary — no campaign CRUD here).</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>Provenance/context for a segment-sourced policy (MOD-0167 boundary — no segment membership here).</summary>
    public Guid? SegmentId { get; set; }

    /// <summary>Optional MOD-0290 brand context. Absent for non-pharma policies (which stay fully valid).</summary>
    public Guid? BrandId { get; set; }

    public Guid? ProductId { get; set; }

    /// <summary>Cycle context for cycle-based frequency (call-cycle calendar owner is external).</summary>
    public Guid? CycleId { get; set; }

    public Guid? CyclePeriodId { get; set; }

    /// <summary><see cref="FrequencyType"/> — weekly / biweekly / monthly / cycle-based / custom.</summary>
    public string FrequencyType { get; set; } = string.Empty;

    /// <summary>How many visits are required per <see cref="PeriodType"/> window. Must be &gt; 0.</summary>
    public int RequiredVisitCount { get; set; }

    /// <summary><see cref="FrequencyPeriodType"/> — day / week / month / quarter / cycle / campaign-period / custom.</summary>
    public string PeriodType { get; set; } = string.Empty;

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    /// <summary>Deterministic tie-break weight. Smaller value wins. Required (≥ 1). Suggested bands live in
    /// <see cref="FrequencyPriorityBands"/>, but the value is authored, never auto-defaulted.</summary>
    public int Priority { get; set; }

    /// <summary><see cref="FrequencySource"/> — provenance (campaign / segmentation / manual / …). Audit-visible.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary><see cref="FrequencyPolicyStatus"/> — draft / active / inactive / archived. Only <c>active</c> is
    /// selectable by the resolve provider.</summary>
    public string Status { get; set; } = FrequencyPolicyStatus.Draft;

    public string? Notes { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    /// <summary>Who soft-deleted the policy (WP-FREQ-A). Pairs with <see cref="EntityBase.DeletedAt"/>. Soft-delete is
    /// DISTINCT from archive: archive keeps the row as readable history (status=archived, still listed), whereas
    /// soft-delete (<see cref="EntityBase.IsDeleted"/>) removes it from the working set (list + resolve).</summary>
    public string? DeletedBy { get; set; }

    /// <summary>WP-FREQ-DET-C — additive, embedded audit trail of the status/weight lifecycle (created / published /
    /// deactivated / reactivated / weight-changed / archived). Appended by the command handlers alongside the existing
    /// CreatedAt / UpdatedAt / ArchivedAt stamps; it changes NO write semantics. Carries NO Guid FK (same document, so
    /// no string-Guid class-map is needed), and it is initialised to an empty list so pre-existing policies (documents
    /// with no <c>Events</c> element) round-trip to an empty trail — the DETAY DURUM AKIŞI then backfills from the
    /// timestamps. Never a source of truth for resolve/CRUD; a read-side projection only.</summary>
    public List<VisitFrequencyPolicyEvent> Events { get; set; } = new();

    /// <summary>Effective at a given instant: EffectiveFrom ≤ at ≤ EffectiveTo (open end when EffectiveTo is null).</summary>
    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);
}

/// <summary>WP-FREQ-DET-C — one entry in the policy's embedded audit trail (<see cref="VisitFrequencyPolicy.Events"/>).
/// It is an owned value object (no Guid FK, no identity of its own), so it needs no string-Guid class map and old
/// documents that predate the trail simply deserialize to an empty list. A settable-property class (the same shape as
/// every other stored embedded CRM value object, e.g. <see cref="VisitReportSample"/>) so AutoMap round-trips it and a
/// missing <c>Events</c> element is safe.</summary>
public sealed class VisitFrequencyPolicyEvent
{
    /// <summary><see cref="FrequencyPolicyEventType"/> — created / published / deactivated / reactivated /
    /// weight-changed / archived.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>When the transition happened.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>Actor who caused it (provenance; null when the principal carries no usable identity).</summary>
    public string? By { get; set; }

    /// <summary>Old value for a <c>weight-changed</c> (band code, resolved from the contract) or a status transition.
    /// Null for point events (created / archived).</summary>
    public string? FromValue { get; set; }

    /// <summary>New value for a <c>weight-changed</c> or status transition. Null for point events.</summary>
    public string? ToValue { get; set; }
}

/// <summary>WP-FREQ-DET-C — the audit-trail event vocabulary. Structural (in-domain), mirrors the lifecycle the mockup
/// DURUM AKIŞI renders. <c>next-eval</c> is NOT an event type: it is a derived, future timeline entry produced only by
/// the analysis read model, never persisted.</summary>
public static class FrequencyPolicyEventType
{
    public const string Created = "created";
    public const string Published = "published";
    public const string Deactivated = "deactivated";
    public const string Reactivated = "reactivated";
    public const string WeightChanged = "weight-changed";
    public const string Archived = "archived";

    /// <summary>Not a stored event — a derived future timeline entry (the "Sonraki değerlendirme" row).</summary>
    public const string NextEval = "next-eval";
}

/// <summary>Policy lifecycle. Hard delete does not exist; a policy is closed with inactive/archived.</summary>
public static class FrequencyPolicyStatus
{
    public const string Draft = "draft";
    public const string Active = "active";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Active, Inactive, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();

    /// <summary>Only an active policy is ever chosen by the resolve provider. Draft/inactive/archived are read-only
    /// history for resolve purposes.</summary>
    public static bool IsResolvable(string? value)
        => !string.IsNullOrWhiteSpace(value) && string.Equals(value.Trim(), Active, StringComparison.OrdinalIgnoreCase);
}

/// <summary>What a frequency policy can target. Structural vocabulary (not tenant vocabulary), so it is validated
/// in-domain — the same way <see cref="AvailabilityWeekday"/> is — rather than through MOD-0048.</summary>
public static class FrequencyTargetType
{
    public const string Account = "account";
    public const string Contact = "contact";
    public const string AccountContactLink = "account-contact-link";
    public const string Segment = "segment";
    public const string TerritoryNode = "territory-node";
    public const string CampaignTarget = "campaign-target";
    public const string ConceptNode = "concept-node";
    public const string AudienceProfile = "audience-profile";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Account, Contact, AccountContactLink, Segment, TerritoryNode, CampaignTarget, ConceptNode, AudienceProfile
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();

    /// <summary>Specificity rank for the resolution tie-break — <b>smaller = more specific = wins</b>. A single
    /// contact at a single location (account-contact-link) is the most specific field target; a segment is the
    /// broadest.</summary>
    public static int Specificity(string? targetType) => (targetType?.Trim().ToLowerInvariant()) switch
    {
        AccountContactLink => 1,
        Contact => 2,
        CampaignTarget => 3,
        Account => 4,
        TerritoryNode => 5,
        ConceptNode => 6,
        AudienceProfile => 7,
        Segment => 8,
        _ => 99
    };
}

/// <summary>Frequency shape vocabulary. In-domain (structural, not tenant vocabulary).</summary>
public static class FrequencyType
{
    public const string Weekly = "weekly";
    public const string Biweekly = "biweekly";
    public const string Monthly = "monthly";
    public const string CycleBased = "cycle-based";
    public const string Custom = "custom";

    public static readonly IReadOnlyList<string> All = new[] { Weekly, Biweekly, Monthly, CycleBased, Custom };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

/// <summary>Period vocabulary the required visit count is measured over. In-domain (structural).</summary>
public static class FrequencyPeriodType
{
    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";
    public const string Quarter = "quarter";
    public const string Cycle = "cycle";
    public const string CampaignPeriod = "campaign-period";
    public const string Custom = "custom";

    public static readonly IReadOnlyList<string> All = new[] { Day, Week, Month, Quarter, Cycle, CampaignPeriod, Custom };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

/// <summary>Provenance of a frequency policy. Audit-visible. In-domain (structural).</summary>
public static class FrequencySource
{
    public const string Campaign = "campaign";
    public const string Segmentation = "segmentation";
    public const string Manual = "manual";
    public const string LegacyImport = "legacy-import";
    public const string BusinessRule = "business-rule";
    public const string ManagerOverride = "manager-override";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Campaign, Segmentation, Manual, LegacyImport, BusinessRule, ManagerOverride, Other
    };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string value) => value.Trim().ToLowerInvariant();
}

/// <summary>One suggested priority band as a client-facing pair: a stable <paramref name="Code"/> a UI localizes and
/// the authored <paramref name="Value"/> (smaller wins). WP-FREQ-B additive contract surface.</summary>
public sealed record FrequencyPriorityBand(string Code, int Value);

/// <summary>Suggested priority bands (smaller wins). WP-FREQ-F1 replaces the earlier 10 specificity bands with the
/// user-approved 5 conceptual weight tiers ("who wins a conflict") — override-all &lt; campaign-level &lt; standard &lt;
/// baseline &lt; last-resort. These are RECOMMENDATIONS a UI can surface as named bands; the stored
/// <see cref="VisitFrequencyPolicy.Priority"/> is always authored explicitly and never silently defaulted, and the
/// resolver still ties on the raw integer weight — the tiers are authoring/display labels only.</summary>
public static class FrequencyPriorityBands
{
    public const int OverrideAll = 100;
    public const int CampaignLevel = 300;
    public const int Standard = 500;
    public const int Baseline = 700;
    public const int LastResort = 900;

    /// <summary>The suggested bands as a client-facing (code, value) list — the SAME constants above, in ascending
    /// weight order (smaller wins). Exposed additively on the FU03 contract (WP-FREQ-B) so an authoring UI can render
    /// priority as named bands WITHOUT hardcoding the numbers; it changes no behaviour (Priority is still authored and
    /// validated as a positive integer, never auto-defaulted from this list). A UI localizes each entry by its
    /// code.</summary>
    public static readonly IReadOnlyList<FrequencyPriorityBand> All = new[]
    {
        new FrequencyPriorityBand("override-all", OverrideAll),
        new FrequencyPriorityBand("campaign-level", CampaignLevel),
        new FrequencyPriorityBand("standard", Standard),
        new FrequencyPriorityBand("baseline", Baseline),
        new FrequencyPriorityBand("last-resort", LastResort),
    };

    /// <summary>WP-FREQ-DET-C — the band code for an authored weight, or null when the weight matches no suggested band
    /// (the value stays an authored integer, so a non-band weight has no code). Used by the audit trail to record a
    /// <c>weight-changed</c> event's from/to as the stable band code the UI localizes; a UI falls back to the raw
    /// number when this is null.</summary>
    public static string? CodeForValue(int value)
    {
        foreach (var band in All)
        {
            if (band.Value == value)
            {
                return band.Code;
            }
        }

        return null;
    }

    /// <summary>Recommended tier from source + target (a manager override always wins; a campaign context sits at the
    /// campaign level; everything else defaults to standard). For UI defaulting only — never auto-applied.</summary>
    public static int Suggest(string? source, string? targetType)
    {
        var normalizedSource = source?.Trim().ToLowerInvariant();
        if (string.Equals(normalizedSource, FrequencySource.ManagerOverride, StringComparison.OrdinalIgnoreCase))
        {
            return OverrideAll;
        }

        var normalizedTarget = targetType?.Trim().ToLowerInvariant();
        if (normalizedTarget == FrequencyTargetType.CampaignTarget
            || string.Equals(normalizedSource, FrequencySource.Campaign, StringComparison.OrdinalIgnoreCase))
        {
            return CampaignLevel;
        }

        return Standard;
    }
}
