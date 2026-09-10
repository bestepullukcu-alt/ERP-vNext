namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// SCMM-11 (CAND-CAP-0011, RM4) — a versioned eligibility policy: a named set of <see cref="Conditions"/> over CONTEXT
/// dimensions (audience axes from <c>AudienceProfile.Dimensions</c>, product [MOD-0290 ref], market, channel, language,
/// period). It is the PERMISSION dimension (DEC-SCMM-03 D3a) — independent of meaning (concept) and presentation
/// (template). CAND-CAP-0011 owned; it lives in the CrmService deployment (no separate microservice) but in its own
/// capability namespace, distinct from MOD-0162 knowledge.
/// <para>
/// <see cref="PolicyCode"/> is the stable business key shared across versions; <see cref="PolicyVersion"/> is the
/// business version (never the technical <see cref="EntityBase.Version"/> concurrency token). A published policy freezes
/// its <see cref="Conditions"/> — a change needs a new version. The condition values are CONFIG / reference strings
/// (sector-neutral); nothing domain-specific is hardcoded. This aggregate stores the policy only — the deterministic
/// evaluation lives in the resolver (ResolveEligibilityQuery), never here.
/// </para>
/// </summary>
public sealed class EligibilityPolicy : EntityBase
{
    /// <summary>Stable business key, shared across the versions of one logical policy.</summary>
    public string PolicyCode { get; set; } = string.Empty;

    public string PolicyName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Business version (NOT <see cref="EntityBase.Version"/>, the concurrency token).</summary>
    public string PolicyVersion { get; set; } = string.Empty;

    /// <summary><see cref="EligibilityPolicyStatuses"/> — draft / review / approved / published / inactive / archived.</summary>
    public string Status { get; set; } = EligibilityPolicyStatuses.Draft;

    /// <summary>The conditions evaluated against the context. Frozen once published (change ⇒ new version).</summary>
    public List<EligibilityCondition> Conditions { get; set; } = new();

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    public bool IsPublished()
        => string.Equals(Status, EligibilityPolicyStatuses.Published, StringComparison.OrdinalIgnoreCase);

    /// <summary>Read-only helper — effective at the instant. Draws no eligibility conclusion (that is the resolver).</summary>
    public bool IsEffectiveAt(DateTimeOffset at)
        => EffectiveFrom <= at && (EffectiveTo is null || at <= EffectiveTo);
}

/// <summary>
/// SCMM-11 (CAND-CAP-0011, RM4) — one condition of an eligibility policy: an <see cref="Includes"/>/<see cref="Excludes"/>
/// test of a CONTEXT <see cref="Dimension"/> against a set of config <see cref="Values"/>. Embedded value object (no
/// TenantId, no Version, no repository). <see cref="Required"/> marks the dimension as mandatory context: if the context
/// carries no value for it, the evaluation is <c>Unresolved</c> (fail-closed — the gate cannot decide), never a silent
/// default. Values are opaque config / reference strings (sector-neutral).
/// </summary>
public sealed class EligibilityCondition
{
    /// <summary>Context axis key (an AudienceProfile axis code, or product / market / channel / language / period).</summary>
    public string Dimension { get; set; } = string.Empty;

    /// <summary><see cref="EligibilityMatchKinds"/> — includes (default) / excludes.</summary>
    public string Match { get; set; } = EligibilityMatchKinds.Includes;

    /// <summary>The config values this condition tests for (at least one).</summary>
    public List<string> Values { get; set; } = new();

    /// <summary>When true, the dimension MUST be present in the context; absent ⇒ Unresolved (fail-closed).</summary>
    public bool Required { get; set; }
}

/// <summary>Eligibility policy lifecycle. Hard delete does not exist; closing a policy is the archive endpoint.</summary>
public static class EligibilityPolicyStatuses
{
    public const string Draft = "draft";
    public const string Review = "review";
    public const string Approved = "approved";
    public const string Published = "published";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Review, Approved, Published, Inactive, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>How a condition tests its dimension. In-domain (structural); an unknown value is a 400 (fail-closed).</summary>
public static class EligibilityMatchKinds
{
    public const string Includes = "includes";
    public const string Excludes = "excludes";

    public static readonly IReadOnlyList<string> All = new[] { Includes, Excludes };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Includes : value.Trim().ToLowerInvariant();
}

/// <summary>Canonical SCMM-11 eligibility reason / outcome codes surfaced on write outcomes and evaluation results.</summary>
public static class EligibilityReasonCodes
{
    public const string PolicyCreated = "eligibility_policy_created";
    public const string PolicyUpdated = "eligibility_policy_updated";
    public const string PolicyPublished = "eligibility_policy_published";
    public const string PolicyArchived = "eligibility_policy_archived";
    public const string PolicyDuplicateCode = "eligibility_policy_duplicate_code";

    // Evaluation reasons (deterministic, surfaced on the disjoint result).
    public const string MissingRequiredContext = "missing_required_context";
    public const string NotIncluded = "not_included";
    public const string Excluded = "excluded";
    public const string PolicyNotEffective = "policy_not_effective";
}
