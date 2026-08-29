using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementGovernancePolicyPack;

/// <summary>
/// MOD-0029-FU31 — the code-based default governance policy pack for MOD-0029 Document Control (GMG-QMS-SOP-0001).
/// A tenant that has just been bootstrapped starts with an EMPTY policy universe; the evaluators then fall through to
/// their safe defaults and every governance screen shows nothing. This manifest is the SOP-aligned minimum baseline
/// the seeder can apply (additive, idempotent) so those screens start populated and the evaluators have concrete
/// policies to resolve. It is a machine-readable projection of the SOP; it is NOT a compliance claim.
/// </summary>
public sealed record GovernancePolicyPackManifestModel(
    string PackKey,
    string PackName,
    string PackVersion,
    string AppliesToModule,
    string SopReference,
    IReadOnlyList<RetentionPolicyDefinition> RetentionPolicies,
    IReadOnlyList<GDocPPolicyDefinition> GDocPCorrectionPolicies,
    IReadOnlyList<SignaturePolicyDefinition> SignaturePolicies);

/// <summary>MOD-0029-FU31 — a single default retention policy definition (mirrors <c>DocumentRetentionPolicy</c> fields).</summary>
public sealed record RetentionPolicyDefinition(
    string PolicyKey,
    string PolicyName,
    RetentionSubjectType SubjectType,
    int MinimumRetentionYears,
    RetentionTrigger RetentionTrigger,
    bool RetainWhileEffective,
    int? RetainAfterRetirementYears,
    int? RetainAfterSupersessionYears,
    bool IsPermanentRetention,
    string RegulatoryBasis,
    string? RetentionClass = null);

/// <summary>MOD-0029-FU31 — a single default GDocP correction policy definition (mirrors <c>DocumentGDocPCorrectionPolicy</c>).</summary>
public sealed record GDocPPolicyDefinition(
    string PolicyKey,
    string PolicyName,
    GDocPSubjectType SubjectType,
    string FieldPathPattern,
    bool RequiresCorrectionReason,
    bool RequiresEvidenceReference,
    bool RequiresReview,
    bool RequiresDeviationReferenceForHighRisk,
    bool AllowCorrectionAfterApproval,
    bool AllowCorrectionAfterEffective,
    bool IsBackdatingSensitive,
    bool IsStatusSensitive,
    bool IsEvidenceSensitive,
    string? Notes = null);

/// <summary>MOD-0029-FU31 — a single default signature policy definition (mirrors <c>DocumentSignaturePolicy</c>).</summary>
public sealed record SignaturePolicyDefinition(
    string PolicyKey,
    string PolicyName,
    SignableSubjectType SignableSubjectType,
    SignatureMeaning SignatureMeaning,
    bool RequiresReAuthentication,
    bool RequiresSecondFactor,
    bool RequiresMeaningStatement,
    bool RequiresRepositoryAssessment,
    bool RequiresObjectFingerprint,
    bool RequiresManifestation,
    IReadOnlyList<RepositoryType> AllowedRepositoryTypes,
    bool AllowInterimRepositorySignature,
    string InterimRepositoryBoundaryStatement);

/// <summary>MOD-0029-FU31 — the outcome of applying (or previewing) one policy definition against a tenant.</summary>
public enum PolicyPackItemStatus
{
    /// <summary>The policy key is absent; Apply would create it (or Preview flags it as creatable).</summary>
    Missing = 0,

    /// <summary>The policy key already exists with compatible core fields; skipped (idempotent).</summary>
    SkippedExisting = 1,

    /// <summary>The policy key exists but its core fields diverge from the default; NOT overwritten (reported).</summary>
    Conflict = 2,

    /// <summary>Apply created the policy.</summary>
    Created = 3
}

/// <summary>MOD-0029-FU31 — per-policy pack outcome line.</summary>
public sealed record PolicyPackItemOutcome(
    string Family,
    string PolicyKey,
    PolicyPackItemStatus Status,
    Guid? CreatedPolicyId,
    string? Message);

/// <summary>MOD-0029-FU31 — the result of a preview or an apply. Tenant-scoped; never mutates existing records.</summary>
public sealed record GovernancePolicyPackApplicationResult(
    string PackKey,
    string PackVersion,
    Guid TenantId,
    string ApplicationStatus, // Preview | Applied | AppliedWithWarnings
    int CreatedCount,
    int SkippedExistingCount,
    int ConflictCount,
    IReadOnlyList<string> WarningMessages,
    IReadOnlyList<Guid> CreatedRetentionPolicyIds,
    IReadOnlyList<Guid> CreatedGDocPPolicyIds,
    IReadOnlyList<Guid> CreatedSignaturePolicyIds,
    IReadOnlyList<PolicyPackItemOutcome> Items);
