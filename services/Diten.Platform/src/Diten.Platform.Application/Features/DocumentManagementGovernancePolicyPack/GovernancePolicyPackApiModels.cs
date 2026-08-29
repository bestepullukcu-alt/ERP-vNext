using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementGovernancePolicyPack;

// MOD-0029-FU31A — API-facing models + reason codes for the governance policy pack preview / apply / history
// surface. Nothing here evaluates a policy or mutates a subject; these are read/summary projections.

public static class GovernancePolicyPackReasonCodes
{
    public const string PackNotFound = "GOVERNANCE_POLICY_PACK_NOT_FOUND";
    public const string ApplicationNotFound = "GOVERNANCE_POLICY_PACK_APPLICATION_NOT_FOUND";
    public const string ApplyFailed = "GOVERNANCE_POLICY_PACK_APPLY_FAILED";
    public const string TenantRequired = "GOVERNANCE_POLICY_PACK_TENANT_REQUIRED";
    public const string ConflictsDetected = "GOVERNANCE_POLICY_PACK_CONFLICTS_DETECTED";
    public const string PreviewFailed = "GOVERNANCE_POLICY_PACK_PREVIEW_FAILED";
}

/// <summary>MOD-0029-FU31A — one line of the preview's policy-definition summary.</summary>
public sealed record GovernancePolicyPackDefinitionSummary(
    string Family,
    string PolicyKey,
    string PolicyName,
    string Outcome);

/// <summary>MOD-0029-FU31A — what an apply WOULD do. Computed only; writes nothing (no policy, no history).</summary>
public sealed record GovernancePolicyPackPreviewModel(
    string PackKey,
    string PackName,
    string PackVersion,
    string SopReference,
    int TotalPolicyCount,
    int RetentionPolicyCount,
    int GDocPPolicyCount,
    int SignaturePolicyCount,
    int ExistingCount,
    int MissingCount,
    int ConflictCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<GovernancePolicyPackDefinitionSummary> PolicyDefinitions);

/// <summary>MOD-0029-FU31A — the outcome of an apply, including the persisted history row id.</summary>
public sealed record GovernancePolicyPackApplyModel(
    string PackKey,
    string PackVersion,
    Guid ApplicationId,
    DocumentGovernancePolicyPackApplicationStatus Status,
    int CreatedPolicyCount,
    int SkippedExistingCount,
    int ConflictCount,
    IReadOnlyList<string> CreatedPolicyKeys,
    IReadOnlyList<string> SkippedPolicyKeys,
    IReadOnlyList<string> ConflictPolicyKeys,
    IReadOnlyList<string> Warnings);

/// <summary>MOD-0029-FU31A — history list row.</summary>
public sealed record GovernancePolicyPackApplicationSummaryModel(
    Guid Id,
    string PackKey,
    string PackVersion,
    DocumentGovernancePolicyPackApplicationStatus Status,
    DateTimeOffset AppliedAt,
    string? AppliedBy,
    int CreatedPolicyCount,
    int SkippedExistingCount,
    int ConflictCount);

/// <summary>MOD-0029-FU31A — full history detail, including every key list and warning.</summary>
public sealed record GovernancePolicyPackApplicationDetailModel(
    Guid Id,
    string PackKey,
    string PackName,
    string PackVersion,
    string? SopReference,
    DocumentGovernancePolicyPackApplicationStatus Status,
    DateTimeOffset AppliedAt,
    string? AppliedBy,
    Guid? AppliedByUserId,
    string? AppliedByRole,
    int CreatedPolicyCount,
    int SkippedExistingCount,
    int ConflictCount,
    IReadOnlyList<string> WarningMessages,
    IReadOnlyList<string> ConflictMessages,
    IReadOnlyList<Guid> CreatedRetentionPolicyIds,
    IReadOnlyList<Guid> CreatedGDocPPolicyIds,
    IReadOnlyList<Guid> CreatedSignaturePolicyIds,
    IReadOnlyList<string> CreatedPolicyKeys,
    IReadOnlyList<string> SkippedPolicyKeys,
    IReadOnlyList<string> ConflictPolicyKeys,
    bool PreviewOnly);
