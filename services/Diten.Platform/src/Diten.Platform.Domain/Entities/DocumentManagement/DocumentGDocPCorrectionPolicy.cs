using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU21 — declares what a correction to a given subject type / field must carry (GMG-QMS-SOP-0001 §21):
/// whether evidence is mandatory, whether a second person must review it, and whether corrections are permitted at
/// all once the record has been approved or made effective.
///
/// MOST RESTRICTIVE WINS: several policies may match one field. The evaluator ORs every "requires" flag and ANDs
/// every "allow" flag across all matching active policies, so adding a policy can only ever tighten control, never
/// loosen it. When NO policy matches, a safe default applies (reason always required, high-risk correction types
/// require a deviation reference, regulated timestamp corrections require review).
///
/// A retired policy stops applying to new corrections but is never deleted, so historic classifications stay
/// explainable.
/// </summary>
public sealed class DocumentGDocPCorrectionPolicy : TenantScopedEntity
{
    public required string PolicyKey { get; set; }
    public required string PolicyName { get; set; }
    public GDocPCorrectionPolicyStatus PolicyStatus { get; set; } = GDocPCorrectionPolicyStatus.Draft;

    public GDocPSubjectType SubjectType { get; set; } = GDocPSubjectType.Other;

    /// <summary>Field matcher. Supports a trailing/leading <c>*</c> wildcard; <c>*</c> alone matches every field.</summary>
    public required string FieldPathPattern { get; set; }

    // ── Requirements (OR-ed across matching policies) ────────────────────────────────────────────────────
    public bool RequiresCorrectionReason { get; set; } = true;
    public bool RequiresEvidenceReference { get; set; }
    public bool RequiresReview { get; set; }
    public bool RequiresDeviationReferenceForHighRisk { get; set; } = true;

    // ── Permissions (AND-ed across matching policies — a single "no" wins) ───────────────────────────────
    public bool AllowCorrectionAfterApproval { get; set; } = true;
    public bool AllowCorrectionAfterEffective { get; set; } = true;

    // ── Sensitivity hints that raise the risk classification ────────────────────────────────────────────
    public bool IsBackdatingSensitive { get; set; }
    public bool IsStatusSensitive { get; set; }
    public bool IsEvidenceSensitive { get; set; }

    public string? Notes { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Simple glob match on the field path: exact, <c>*</c>, <c>prefix*</c> or <c>*suffix</c>.</summary>
    public bool Matches(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(FieldPathPattern) || FieldPathPattern == "*")
        {
            return true;
        }

        var pattern = FieldPathPattern.Trim();
        var startsWildcard = pattern.StartsWith('*');
        var endsWildcard = pattern.EndsWith('*');
        var core = pattern.Trim('*');

        if (core.Length == 0)
        {
            return true;
        }

        return startsWildcard && endsWildcard
            ? fieldPath.Contains(core, StringComparison.OrdinalIgnoreCase)
            : startsWildcard
                ? fieldPath.EndsWith(core, StringComparison.OrdinalIgnoreCase)
                : endsWildcard
                    ? fieldPath.StartsWith(core, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(fieldPath, core, StringComparison.OrdinalIgnoreCase);
    }
}
