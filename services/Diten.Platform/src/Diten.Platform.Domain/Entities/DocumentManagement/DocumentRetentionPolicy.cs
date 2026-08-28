using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU15 — a document retention policy (GMG-QMS-SOP-0001 §22). Declares HOW LONG a class of regulated
/// record must be kept and WHICH DATE starts the clock. Retention is determined by the LONGEST APPLICABLE
/// requirement, so several policies may match one subject and the evaluator takes the maximum.
///
/// SOP baseline this models: controlled documents and superseded versions are retained while effective PLUS at
/// least 10 years after retirement/supersession; approval records, review comments and impact assessments are
/// retained at least 10 years. The identifier allocation ledger is a permanent record (UIDs/codes are never
/// reused), which <see cref="IsPermanentRetention"/> expresses.
///
/// BOUNDARY: a policy computes a due date. It NEVER deletes, purges or archives anything — FU15 has no
/// destruction engine. The Retention Schedule itself is expected to be governed as a controlled document via the
/// FU06 register; this aggregate is its machine-readable projection.
/// </summary>
public sealed class DocumentRetentionPolicy : TenantScopedEntity
{
    public required string PolicyKey { get; set; }
    public required string PolicyName { get; set; }
    public RetentionPolicyStatus PolicyStatus { get; set; } = RetentionPolicyStatus.Draft;

    /// <summary>Which kind of regulated record this policy governs.</summary>
    public RetentionSubjectType SubjectType { get; set; } = RetentionSubjectType.Other;

    /// <summary>Optional narrowing: when set, the policy only applies to subjects carrying this retention class.</summary>
    public string? RetentionClass { get; set; }

    // ── Retention period (SOP §22 "longest applicable requirement") ──────────────────────────────────────
    public int MinimumRetentionYears { get; set; }
    public RetentionTrigger RetentionTrigger { get; set; } = RetentionTrigger.CreationDate;

    /// <summary>SOP §22: an effective controlled document is retained regardless of any elapsed period.</summary>
    public bool RetainWhileEffective { get; set; }

    public int? RetainAfterRetirementYears { get; set; }
    public int? RetainAfterSupersessionYears { get; set; }

    /// <summary>
    /// Permanent record — never disposition eligible under any elapsed period. Used for the identifier
    /// allocation ledger (UID/code must never be reused) and for litigation hold records themselves.
    /// </summary>
    public bool IsPermanentRetention { get; set; }

    // ── Provenance of the requirement ────────────────────────────────────────────────────────────────────
    public string? RegulatoryBasis { get; set; }
    public string? Jurisdiction { get; set; }

    /// <summary>Marks this policy as a candidate in the longest-applicable comparison. Informational.</summary>
    public bool IsLongestApplicableCandidate { get; set; } = true;

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>
    /// The effective retention period in years: the LONGEST of the declared minimum and any post-retirement /
    /// post-supersession requirement (SOP §22 longest applicable requirement).
    /// </summary>
    public int EffectiveRetentionYears() => Math.Max(
        Math.Max(MinimumRetentionYears, RetainAfterRetirementYears ?? 0),
        RetainAfterSupersessionYears ?? 0);
}
