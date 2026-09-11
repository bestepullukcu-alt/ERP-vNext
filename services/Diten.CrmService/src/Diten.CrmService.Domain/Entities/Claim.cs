namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// SCMM-12 (CAND-CAP-0011, docx §4) — a Claim: a piece of GOVERNED WORDING (<see cref="ClaimText"/>) with its
/// qualifiers, applicability and evidence references. It is the content/meaning artifact (DEC-SCMM-03 D3a) — independent
/// of eligibility (permission) and template (presentation). CAND-CAP-0011 owned; it lives in the CrmService deployment,
/// in the ContentComposition namespace next to Eligibility, distinct from MOD-0162 knowledge.
/// <para>
/// <see cref="ClaimCode"/> is the stable business key shared across versions; <see cref="ClaimVersion"/> is the business
/// version (never the technical <see cref="EntityBase.Version"/> concurrency token). The approval lifecycle is
/// draft → approved; an APPROVED claim freezes its governed body (text / qualifiers / applicability / evidence /
/// component refs) — a change needs a new version. <b>Claim approval is NOT assembly approval</b> (assembly approval is
/// SCMM-15/17): approving a claim never approves any content assembly that uses it.
/// </para>
/// <para>
/// <see cref="EvidenceRefs"/> are OPAQUE strings: MOD-0031 (shared evidence) is review/planned (the C1 blocker), so this
/// slice stores evidence references without resolving or validating them — a MOD-0031 contract is never invented here
/// (resolution is a documented follow). <see cref="ComponentRefs"/> are by-id references to reusable components
/// (MOD-0162 <c>KnowledgeContent</c>, D02c reuse) — stored as provenance; the component aggregate is NOT rebuilt and is
/// not touched, and deep assembly is SCMM-14.
/// </para>
/// </summary>
public sealed class Claim : EntityBase
{
    /// <summary>Stable business key, shared across the versions of one logical claim.</summary>
    public string ClaimCode { get; set; } = string.Empty;

    public string ClaimName { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>The governed wording. Frozen once the claim is approved (change ⇒ new version).</summary>
    public string ClaimText { get; set; } = string.Empty;

    /// <summary>Qualifiers attached to the wording (config / reference strings; sector-neutral). Frozen on approval.</summary>
    public List<string> Qualifiers { get; set; } = new();

    /// <summary>Where the claim applies: product / market / audience references (+ optional eligibility policy). Frozen on approval.</summary>
    public ClaimApplicability Applicability { get; set; } = new();

    /// <summary>OPAQUE evidence references (MOD-0031 resolution deferred — C1 blocker). Frozen on approval.</summary>
    public List<string> EvidenceRefs { get; set; } = new();

    /// <summary>By-id references to reusable components (MOD-0162 KnowledgeContent, D02c reuse). Provenance only; not
    /// validated against the component aggregate in this slice (deep assembly is SCMM-14). Frozen on approval.</summary>
    public List<Guid> ComponentRefs { get; set; } = new();

    /// <summary>Business version (NOT <see cref="EntityBase.Version"/>, the concurrency token).</summary>
    public string ClaimVersion { get; set; } = string.Empty;

    /// <summary><see cref="ClaimStatuses"/> — draft / approved / inactive / archived.</summary>
    public string Status { get; set; } = ClaimStatuses.Draft;

    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? ArchivedBy { get; set; }

    public bool IsArchived() => ArchivedAt is not null;

    public bool IsApproved()
        => string.Equals(Status, ClaimStatuses.Approved, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// SCMM-12 (CAND-CAP-0011) — where a claim applies. Product / market / audience values are OPAQUE config references
/// (sector-neutral). <see cref="EligibilityPolicyId"/> is an OPTIONAL by-id reference to a CAND-CAP-0011 eligibility
/// policy (SCMM-11) — provenance only; this slice performs no eligibility evaluation (that is the resolver, and
/// assembly-time evaluation is SCMM-14). Embedded value object.
/// </summary>
public sealed class ClaimApplicability
{
    public List<string> ProductRefs { get; set; } = new();
    public List<string> MarketRefs { get; set; } = new();
    public List<string> AudienceRefs { get; set; } = new();
    public Guid? EligibilityPolicyId { get; set; }
}

/// <summary>Claim lifecycle. Hard delete does not exist; closing a claim is the archive endpoint. Approval freezes the
/// governed body — a change to an approved claim needs a new version.</summary>
public static class ClaimStatuses
{
    public const string Draft = "draft";
    public const string Approved = "approved";
    public const string Inactive = "inactive";
    public const string Archived = "archived";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Approved, Inactive, Archived };

    public static bool IsValid(string? value)
        => !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToLowerInvariant());

    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? Draft : value.Trim().ToLowerInvariant();
}

/// <summary>Canonical SCMM-12 claim reason / outcome codes surfaced on write outcomes and audit.</summary>
public static class ClaimReasonCodes
{
    public const string Created = "claim_created";
    public const string Updated = "claim_updated";
    public const string Approved = "claim_approved";
    public const string Archived = "claim_archived";
    public const string DuplicateCode = "claim_duplicate_code";
    public const string ApprovedFrozen = "claim_approved_frozen";
}
