namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline;

/// <summary>
/// MOD-0028-FU02 permission constants (lowercase PKS-001 effective keys). The three <c>qms-baselines.*</c> keys are
/// FU02-native; the two <c>collection-definitions.*</c> keys are reused from the FU01 foundation
/// (<see cref="Diten.Platform.Application.Features.DocumentManagementContract.DocumentManagementPermissions"/>).
/// Seed/alias ownership for the new keys is a separate MOD-0018/security task (see pack §14 controlled gate).
/// </summary>
public static class QmsBaselinePermissions
{
    public const string Import = "platform.document-management.qms-baselines.import";
    public const string View = "platform.document-management.qms-baselines.view";
    public const string Publish = "platform.document-management.qms-baselines.publish";
    public const string Create = "platform.document-management.qms-baselines.create";
    public const string Validate = "platform.document-management.qms-baselines.validate";
    public const string CollectionDefinitionsCreate = "platform.document-management.collection-definitions.create";
    public const string CollectionDefinitionsEdit = "platform.document-management.collection-definitions.edit";
    public const string CollectionDefinitionsMove = "platform.document-management.collection-definitions.move";
    public const string CollectionDefinitionsDelete = "platform.document-management.collection-definitions.delete";
}

/// <summary>
/// FU02 controlled-failure reason codes. PERM_DENIED is owned by the FU01
/// <c>DocumentManagementReasonCodes</c> catalog and is emitted by the shared <c>HasPermissionAttribute</c>.
/// </summary>
public static class QmsBaselineReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Conflict = "CONFLICT";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
}

/// <summary>
/// One raw folder row parsed from the approved QMS workbook. Either <see cref="Path"/> (slash-separated full path)
/// or (<see cref="ParentPath"/> + <see cref="Name"/>) identifies the node. Metadata columns are optional.
/// </summary>
public sealed record QmsFolderImportRow(
    int SourceRowNumber,
    string? Path,
    string? ParentPath,
    string? Name,
    string? PurposeScope,
    string? RequiredByScope,
    bool? AllowsManualChildren,
    bool? TemplatesAllowed,
    string? AllowedDocClass,
    string? DefaultClassificationLevel,
    string? DefaultRetentionHint,
    bool? IsMandatory,
    bool? IsAutoProvisioned,
    bool? IsProtected,
    int? DisplayOrder,
    string? OutlineCode = null);

/// <summary>A validated, normalized definition node ready to materialize as a <c>CollectionDefinition</c>.</summary>
public sealed record QmsCollectionDefinitionDraft(
    string CanonicalId,
    string? ParentCanonicalId,
    string Name,
    string? PurposeScope,
    string? RequiredByScope,
    bool AllowsManualChildren,
    bool TemplatesAllowed,
    string? AllowedDocClass,
    string? DefaultClassificationLevel,
    string? DefaultRetentionHint,
    bool IsMandatory,
    bool IsAutoProvisioned,
    bool IsProtected,
    string PathSegment,
    string FullPath,
    int DisplayOrder,
    string DefinitionHash);

/// <summary>Validation summary returned by dry-run and commit.</summary>
public sealed record QmsBaselineImportSummary(
    int TotalRows,
    int ImportedDefinitionsCount,
    int SkippedRows,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> DuplicatePathConflicts,
    IReadOnlyList<string> InvalidHierarchyFindings,
    bool DryRun,
    bool Committed)
{
    public bool IsValid => Errors.Count == 0
        && DuplicatePathConflicts.Count == 0
        && InvalidHierarchyFindings.Count == 0;
}

/// <summary>Internal in-memory result of building+validating an import (not persisted by dry-run).</summary>
public sealed record QmsBaselineImportPlan(
    QmsBaselineImportSummary Summary,
    IReadOnlyList<QmsCollectionDefinitionDraft> Definitions);

/// <summary>Commit result: summary plus the created DRAFT baseline identity.</summary>
public sealed record QmsBaselineCommitResult(
    QmsBaselineImportSummary Summary,
    Guid? BaselineReleaseId,
    string? BaselineReleaseKey);

/// <summary>Baseline list/detail projection.</summary>
public sealed record QmsBaselineSummaryModel(
    Guid Id,
    string BaselineReleaseId,
    string BaselineVersion,
    string Status,
    string? SnapshotHash,
    Guid? ManifestId,
    int DefinitionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    string? ChangeSummary = null,
    DateTimeOffset? EffectiveDate = null,
    int VersionToken = 0);

/// <summary>Publish result projection.</summary>
public sealed record QmsBaselinePublishResult(
    Guid BaselineReleaseId,
    string Status,
    string SnapshotHash,
    Guid ManifestId,
    string ManifestVersion,
    int DefinitionCount);

/// <summary>Definition list/detail projection.</summary>
public sealed record QmsCollectionDefinitionModel(
    Guid Id,
    Guid BaselineReleaseId,
    string CanonicalId,
    string? ParentCanonicalId,
    string Name,
    string? PurposeScope,
    string? RequiredByScope,
    bool AllowsManualChildren,
    bool TemplatesAllowed,
    string? AllowedDocClass,
    string? DefaultClassificationLevel,
    string? DefaultRetentionHint,
    bool IsMandatory,
    bool IsAutoProvisioned,
    bool IsProtected,
    string PathSegment,
    string FullPath,
    int DisplayOrder,
    string Status,
    string DefinitionHash,
    int VersionToken = 0);

public sealed record ManualQmsBaselineRequestModel(
    string BaselineVersion,
    string? Name,
    string? ChangeSummary,
    DateTimeOffset? EffectiveDate);

public sealed record QmsCollectionDefinitionUpsertModel(
    string Name,
    string? ParentCanonicalId,
    string? PurposeScope,
    string? RequiredByScope,
    string? AllowedDocClass,
    string? DefaultClassificationLevel,
    string? DefaultRetentionHint,
    int DisplayOrder,
    bool AllowsManualChildren,
    bool TemplatesAllowed,
    bool IsMandatory,
    bool IsProtected,
    int VersionToken);

public sealed record QmsCollectionDefinitionMoveModel(
    string? ParentCanonicalId,
    int DisplayOrder,
    int VersionToken);

public sealed record QmsDraftTreeValidationResult(
    bool Valid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> DuplicateSiblingFindings,
    IReadOnlyList<string> OrphanParentFindings,
    IReadOnlyList<string> InvalidHierarchyFindings);
