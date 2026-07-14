using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementReconciliation;

/// <summary>MOD-0028-FU09 permission constants (reuse FU02 baseline keys; no new seed in this FU).</summary>
public static class ReconciliationPermissions
{
    public const string View = "platform.document-management.qms-baselines.view";
    public const string Manage = "platform.document-management.qms-baselines.publish";
}

public static class ReconciliationReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string ProviderUnavailable = "PROVIDER_UNAVAILABLE";
}

/// <summary>What the reconciliation compares.</summary>
public enum ReconciliationScope
{
    DefinitionToInstance = 0,
    DefinitionToProvider = 1,
    InstanceToProvider = 2
}

/// <summary>An expected node built from the register-backed CollectionDefinition (or provisioned CollectionInstance).</summary>
public sealed record ExpectedNode(
    string? RegisterFolderId,
    string? RegisterParentFolderId,
    string Name,
    string FullPath,
    string? ParentFullPath,
    string? AccessProfile,
    string? FolderType,
    string? RetentionClass,
    Guid? CollectionDefinitionId,
    Guid? CollectionInstanceId);

/// <summary>A node read back from a platform provider (in-house instances, or an external provider tree).</summary>
public sealed record ReadBackNode(
    string PlatformFolderId,
    string? PlatformParentId,
    string Name,
    string FullPath,
    string? ParentFullPath,
    string? RegisterFolderId,
    DateTimeOffset? CreatedAt,
    string? CreatedBy,
    IReadOnlyDictionary<string, string?> Metadata,
    Guid? CollectionInstanceId = null);

/// <summary>One detected difference; not yet persisted (dry-run) or the source of a persisted deviation (apply).</summary>
public sealed record DeviationDetail(
    CollectionDeviationType DeviationType,
    DeviationSeverity Severity,
    string? RegisterFolderId,
    Guid? CollectionInstanceId,
    string ExpectedFullPath,
    string? ActualFullPath,
    string Description,
    string Recommendation);

public sealed record ReconciliationSummary(
    int ExpectedCount,
    int ActualCount,
    int MatchedCount,
    int MissingCount,
    int ExtraCount,
    int RenamedCount,
    int MovedCount,
    int MetadataMismatchCount,
    int DeviationCount,
    int BlockingDeviationCount);

public sealed record ReconciliationResult(
    Guid BaselineReleaseId,
    string BaselineStatus,
    string Scope,
    string Provider,
    bool DryRun,
    ReconciliationSummary Summary,
    IReadOnlyList<DeviationDetail> Deviations)
{
    public bool IsClean => Summary.DeviationCount == 0;
}

public sealed record ReconciliationRequest(
    Guid BaselineReleaseId,
    ReconciliationScope Scope,
    ProvisioningPlatformProvider Provider,
    bool DryRun);
