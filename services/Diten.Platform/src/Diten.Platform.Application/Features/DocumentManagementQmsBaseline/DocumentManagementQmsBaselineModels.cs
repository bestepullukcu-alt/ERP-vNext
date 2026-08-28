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

    /// <summary>MOD-0028-FU08 — MarkEffective blocked because the source register/package is still Draft/not-for-execution.</summary>
    public const string PackageNotApproved = "PACKAGE_NOT_APPROVED";
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
    string? OutlineCode = null,
    // QMS register import extension — governance identity pending. Columns are additive and optional so every XLSX/fixture
    // construction keeps compiling and behaving identically). Populated only by the CSV/flat-JSON parsers.
    string? FolderId = null,
    string? ParentFolderId = null,
    string? RegisterFullPath = null,
    string? DepartmentDomain = null,
    string? FolderType = null,
    string? ExampleDocuments = null,
    string? OwningDepartments = null,
    string? ControlledByGqms = null,
    string? SourceOfTruth = null,
    string? OwnerFunction = null,
    string? AccessProfile = null,
    string? RetentionClass = null,
    string? ChangeControlRequired = null,
    string? GqmsScopeLink = null,
    string? LegacyCode = null,
    string? ProvisioningWave = null,
    int? ProvisioningOrder = null);

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
    string DefinitionHash,
    // QMS register import extension — governance identity pending. Metadata is additive and excluded from the structural
    // DefinitionHash on purpose — it is descriptive, not structural). Null for legacy path-hash imports.
    string? RegisterFolderId = null,
    string? RegisterParentFolderId = null,
    string? RegisterFullPath = null,
    string? DepartmentDomain = null,
    string? FolderType = null,
    string? ExampleDocuments = null,
    string? OwningDepartments = null,
    string? ControlledByGqms = null,
    string? SourceOfTruth = null,
    string? OwnerFunction = null,
    string? AccessProfile = null,
    string? RetentionClass = null,
    string? ChangeControlRequired = null,
    string? GqmsScopeLink = null,
    string? LegacyCode = null,
    string? ProvisioningWave = null,
    int? ProvisioningOrder = null);

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

/// <summary>
/// MOD-0028-FU08 — resolves whether a source register/package status permits a baseline to be marked Effective.
/// A null/blank status (legacy imports, manual baselines) is permissive; an explicitly draft / "do not execute"
/// register is blocked. Case-insensitive; substring match so "Draft — do not execute until approved" is caught.
/// </summary>
public static class BaselinePackageStatus
{
    public static bool AllowsEffective(string? sourcePackageStatus)
    {
        if (string.IsNullOrWhiteSpace(sourcePackageStatus))
        {
            return true;
        }

        var s = sourcePackageStatus.Trim().ToLowerInvariant();
        if (s.Contains("do not execute", StringComparison.Ordinal)
            || s.Contains("not for execution", StringComparison.Ordinal)
            || s.Contains("not-for-execution", StringComparison.Ordinal))
        {
            return false;
        }

        // A bare "draft" register status blocks going effective; "approved"/"effective"/"active" allow it.
        return !s.Equals("draft", StringComparison.Ordinal)
            && !s.StartsWith("draft ", StringComparison.Ordinal)
            && !s.StartsWith("draft-", StringComparison.Ordinal)
            && !s.StartsWith("draft—", StringComparison.Ordinal)
            && !s.StartsWith("draft:", StringComparison.Ordinal);
    }
}

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
    int VersionToken = 0,
    // MOD-0028-FU08 lifecycle projection (additive).
    string? SourcePackageStatus = null,
    DateTimeOffset? ApprovedAt = null,
    string? ApprovedBy = null,
    string? ApprovalReference = null,
    DateTimeOffset? EffectiveAt = null,
    string? EffectiveBy = null,
    DateTimeOffset? SupersededAt = null,
    Guid? SupersedesBaselineReleaseId = null,
    Guid? SupersededByBaselineReleaseId = null,
    bool CanApprove = false,
    bool CanMarkEffective = false,
    bool CanInstantiate = false);

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
    int VersionToken = 0,
    // QMS register import extension — governance identity pending; additive projection, null for legacy path-hash imports.
    string? RegisterFolderId = null,
    string? RegisterParentFolderId = null,
    string? DepartmentDomain = null,
    string? FolderType = null,
    string? ControlledByGqms = null,
    string? SourceOfTruth = null,
    string? OwnerFunction = null,
    string? AccessProfile = null,
    string? RetentionClass = null,
    string? ChangeControlRequired = null,
    string? GqmsScopeLink = null,
    string? LegacyCode = null,
    string? ProvisioningWave = null,
    int? ProvisioningOrder = null,
    string? ExampleDocuments = null,
    string? OwningDepartments = null);

public sealed record ManualQmsBaselineRequestModel(
    string BaselineVersion,
    string? Name,
    string? ChangeSummary,
    DateTimeOffset? EffectiveDate,
    // MOD-0028-FU08 — optional explicit source/lineage key. When blank, it is derived from the name (or a unique
    // per-baseline key when the name is also blank), preserving the legacy behaviour.
    string? SourceBaselineKey = null);

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
