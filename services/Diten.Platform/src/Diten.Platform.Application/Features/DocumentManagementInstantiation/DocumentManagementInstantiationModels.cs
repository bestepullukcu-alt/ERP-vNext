using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementInstantiation;

public static class DocumentManagementInstantiationPermissions
{
    public const string BaselineReleasesView = "platform.document-management.baseline-releases.view";
    public const string BaselinesInstantiate = "platform.document-management.baselines.instantiate";
    public const string InstantiationsDryRun = "platform.document-management.instantiations.dry-run";
    public const string InstantiationsExecute = "platform.document-management.instantiations.execute";
    public const string CollectionInstancesView = "platform.document-management.collection-instances.view";
    public const string CollectionInstancesCreate = "platform.document-management.collection-instances.create";
    public const string CollectionInstancesRetry = "platform.document-management.collection-instances.retry";
}

public static class DocumentManagementInstantiationReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Conflict = "CONFLICT";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string RetryUnavailable = "RETRY_UNAVAILABLE";
    public const string DependencyUnavailable = "DEPENDENCY_UNAVAILABLE";

    /// <summary>MOD-0028-FU08 — baseline is not Effective (nor legacy Published), so it cannot be instantiated.</summary>
    public const string BaselineNotEffective = "BASELINE_NOT_EFFECTIVE";
}

public sealed record InstantiationScopeRequest(
    Guid CompanyId,
    Guid? PlantId,
    Guid? BusinessUnitId,
    string? InstanceToken);

public sealed record InstantiationSelectionRequest(
    InstantiationSelectionMode SelectionMode,
    IReadOnlyList<string> SelectedCanonicalIds,
    bool IncludeDescendants,
    bool IncludeRequiredAncestors)
{
    public static InstantiationSelectionRequest Default { get; } =
        new(InstantiationSelectionMode.FullTree, [], true, true);
}

public sealed record InstantiationOutcomeModel(
    string NodeKey,
    string CanonicalId,
    string Status,
    string ReasonCode,
    string Message,
    bool Retryable);

public sealed record InstantiationDiagnosticsModel(
    bool Valid,
    bool Blocked,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    int NodesToCreate,
    int NodesToSkip,
    int Conflicts,
    string SelectionMode,
    IReadOnlyList<string> SelectedCanonicalIds,
    IReadOnlyList<string> IncludedCanonicalIds,
    IReadOnlyList<string> IncludedAncestors,
    IReadOnlyList<string> IncludedDescendants,
    int ExcludedCanonicalIdsCount,
    IReadOnlyList<string> BlockedSelections,
    IReadOnlyList<InstantiationOutcomeModel> Outcomes);

public sealed record InstantiationResultModel(
    Guid OperationId,
    Guid BaselineReleaseId,
    Guid CompanyId,
    string? InstanceToken,
    string OperationType,
    string Status,
    int Created,
    int Skipped,
    int Failed,
    int Total,
    string CorrelationId,
    IReadOnlyList<InstantiationOutcomeModel> Outcomes,
    InstantiationDiagnosticsModel? Diagnostics = null);

public sealed record InstantiationPrerequisitesModel(
    IReadOnlyList<PublishedBaselineReleaseOptionModel> PublishedReleases,
    bool HasPublishedRelease,
    bool Mod0220ValidatorRequired,
    bool LocalSmokeFallbackEnabled,
    bool RetryEnabled);

public sealed record PublishedBaselineReleaseOptionModel(
    Guid Id,
    string BaselineReleaseId,
    string BaselineVersion,
    string? ChangeSummary,
    DateTimeOffset? EffectiveDate,
    int DefinitionCount,
    DateTimeOffset? PublishedAt,
    IReadOnlyList<CollectionDefinitionOptionModel> Definitions);

public sealed record CollectionDefinitionOptionModel(
    string CanonicalId,
    string? ParentCanonicalId,
    string Name,
    string FullPath,
    int DisplayOrder);

public sealed record CollectionInstanceStatusChangeResultModel(
    Guid Id,
    int AffectedCount);

public sealed record CollectionInstanceListItemModel(
    Guid Id,
    string InstanceKey,
    Guid CompanyId,
    Guid BaselineReleaseId,
    string CanonicalId,
    string? ParentCanonicalId,
    string Name,
    string FullPath,
    int DisplayOrder,
    string InstanceStatus,
    string? InstanceToken,
    DateTimeOffset LastChangeAt,
    int VersionToken);

public sealed record CollectionInstanceDetailModel(
    Guid Id,
    string InstanceKey,
    Guid CompanyId,
    Guid BaselineReleaseId,
    string CanonicalId,
    string? ParentCanonicalId,
    string Name,
    string FullPath,
    int DisplayOrder,
    string CollectionScopeType,
    string InstanceStatus,
    IReadOnlyList<ScopeBindingModel> ScopeBindings,
    string? InstanceToken,
    string SourceDefinitionHash,
    DateTimeOffset LastChangeAt,
    int VersionToken);

public sealed record ScopeBindingModel(
    string OrgBindingScopeType,
    Guid OrgBindingScopeId,
    string ScopeSourceModule,
    string BindingStatus,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset? LastValidatedAt);

public sealed record InstantiationPlan(
    Guid OperationId,
    Guid TenantId,
    Guid BaselineReleaseId,
    Guid CompanyId,
    Guid? PlantId,
    Guid? BusinessUnitId,
    string? InstanceToken,
    InstantiationSelectionMode SelectionMode,
    IReadOnlyList<string> SelectedCanonicalIds,
    bool IncludeDescendants,
    bool IncludeRequiredAncestors,
    bool Blocked,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> IncludedAncestors,
    IReadOnlyList<string> IncludedDescendants,
    int ExcludedCanonicalIdsCount,
    IReadOnlyList<string> BlockedSelections,
    IReadOnlyList<InstantiationPlanNode> Nodes,
    string CorrelationId);

public sealed record InstantiationPlanNode(
    string NodeKey,
    string InstanceKey,
    string CanonicalId,
    string? ParentCanonicalId,
    string Name,
    string FullPath,
    int DisplayOrder,
    string SourceDefinitionHash,
    bool Exists);

internal static class InstantiationEnumNames
{
    public static string ToWire(this InstantiationOutcomeStatus status) => status.ToString().ToUpperInvariant();
    public static string ToWire(this InstantiationOperationStatus status) => status.ToString().ToUpperInvariant();
    public static string ToWire(this InstantiationOperationType type) => type.ToString().ToUpperInvariant();
    public static string ToWire(this InstantiationSelectionMode type) =>
        type == InstantiationSelectionMode.SelectedBranches ? "SELECTED_BRANCHES" : "FULL_TREE";
}
