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

    /// <summary>Effective at a given instant: EffectiveFrom ≤ at ≤ EffectiveTo (open end when EffectiveTo is null).</summary>
    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);
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

/// <summary>Suggested default priority bands (smaller wins). These are RECOMMENDATIONS a UI can surface; the stored
/// <see cref="VisitFrequencyPolicy.Priority"/> is always authored explicitly and never silently defaulted.</summary>
public static class FrequencyPriorityBands
{
    public const int ManagerOverride = 100;
    public const int CampaignTarget = 200;
    public const int AccountContactLink = 300;
    public const int Contact = 400;
    public const int Account = 500;
    public const int Segment = 600;
    public const int TerritoryNode = 700;
    public const int ConceptNode = 750;
    public const int AudienceProfile = 775;
    public const int BusinessRule = 800;

    /// <summary>Recommended band from source + target (manager-override always wins the band). For UI defaulting only.</summary>
    public static int Suggest(string? source, string? targetType)
    {
        if (string.Equals(source?.Trim(), FrequencySource.ManagerOverride, StringComparison.OrdinalIgnoreCase))
        {
            return ManagerOverride;
        }

        return (targetType?.Trim().ToLowerInvariant()) switch
        {
            FrequencyTargetType.CampaignTarget => CampaignTarget,
            FrequencyTargetType.AccountContactLink => AccountContactLink,
            FrequencyTargetType.Contact => Contact,
            FrequencyTargetType.Account => Account,
            FrequencyTargetType.Segment => Segment,
            FrequencyTargetType.TerritoryNode => TerritoryNode,
            FrequencyTargetType.ConceptNode => ConceptNode,
            FrequencyTargetType.AudienceProfile => AudienceProfile,
            _ => BusinessRule
        };
    }
}
