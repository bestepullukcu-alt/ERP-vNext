using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementMasterRegister;

// MOD-0029-FU06 — Document Master Register (GMG-QMS-SOP-0001 §18 LOG-0001, §20) contracts, permission constants,
// reason codes and wire mapping, kept in one file (Golden Reference Compact convention, mirroring
// DocumentManagementTemplateMastersModels.cs / DocumentManagementControlledDocumentsModels.cs).

/// <summary>
/// MOD-0029-FU06 — RECOMMENDED Layer 1 RBAC keys for the Master Register. NOT seeded in this FU (no AuthService
/// change): the controller reuses the already-seeded controlled-documents view/create keys so the register works at
/// runtime. FU06A/FU07 should seed these dedicated keys and switch the controller over.
/// </summary>
public static class DocumentMasterRegisterPermissions
{
    public const string View = "platform.document-management.master-register.view";
    public const string Manage = "platform.document-management.master-register.manage";
    public const string Link = "platform.document-management.master-register.link";
    public const string AuditView = "platform.document-management.master-register.audit.view";
}

public static class MasterRegisterReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string DuplicatePermanentUid = "DUPLICATE_PERMANENT_UID";
    public const string DuplicateDocumentCode = "DUPLICATE_DOCUMENT_CODE";
    public const string RecordControlledConflict = "RECORD_CONTROLLED_CONFLICT";
    public const string VariantParentMissing = "VARIANT_PARENT_MISSING";
    public const string AlreadyLinked = "ALREADY_LINKED";
    public const string ScopeMismatch = "SCOPE_MISMATCH";
    public const string ScopeOwnerMismatch = "SCOPE_OWNER_MISMATCH";
    public const string CollectionInstanceMismatch = "COLLECTION_INSTANCE_MISMATCH";
    public const string FolderScopeMismatch = "FOLDER_SCOPE_MISMATCH";
    public const string CorporateAccessRequired = "CORPORATE_ACCESS_REQUIRED";
    public const string LegacyLinkReconciliationRequired = "LEGACY_LINK_RECONCILIATION_REQUIRED";
    public const string ProtectedFieldChange = "PROTECTED_FIELD_CHANGE";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

// ── inputs ───────────────────────────────────────────────────────────────────

/// <summary>
/// MOD-0029-FU06 — create a manual register entry. PermanentUid/DocumentCode are OPTIONAL here (allocation is FU07);
/// when supplied manually the entry is stored with <c>IsSystemAllocated=false</c> provenance.
/// </summary>
public sealed record CreateMasterRegisterEntryInput(
    string DocumentTitle,
    string DocumentClass,
    string Criticality,
    string? DocumentType,
    string? PermanentUid,
    string? DocumentCode,
    string? LegacyCode,
    string? ProcessOwnerRole,
    Guid? ProcessOwnerUserId,
    Guid? AuthorUserId,
    string? OwnerFunction,
    Guid? OwnerCompanyId,
    string? GoverningLanguage,
    int? ReviewCycleMonths,
    string? RetentionClass,
    bool IsControlledDocument,
    bool IsRecord,
    bool IsExternalDocument,
    bool IsTemplate,
    bool IsVariant,
    string? ParentDocumentUid,
    string? ParentDocumentCode,
    string? SourceSystem,
    string? SourceLegacyId);

/// <summary>
/// MOD-0029-FU06 — metadata-only update. Deliberately EXCLUDES the protected allocation/lifecycle/gate fields
/// (PermanentUid, DocumentCode, LifecycleStatus, EffectiveDate, CurrentVersionLabel, release-gate/approval-evidence
/// results). Those are set only by dedicated later services (FU07/FU08/FU10).
/// </summary>
public sealed record UpdateMasterRegisterMetadataInput(
    string DocumentTitle,
    string DocumentClass,
    string Criticality,
    string? DocumentType,
    string? LegacyCode,
    string? ProcessOwnerRole,
    Guid? ProcessOwnerUserId,
    Guid? AuthorUserId,
    string? OwnerFunction,
    Guid? OwnerCompanyId,
    string? GoverningLanguage,
    int? ReviewCycleMonths,
    string? RetentionClass,
    string? ApprovedRepositoryId,
    string? ApprovedRepositoryName,
    string? ApprovedRepositoryPath,
    string? ParentDocumentUid,
    string? ParentDocumentCode);

public sealed record LinkControlledDocumentInput(Guid ControlledDocumentId, string ReconciliationReason);

// ── output models ──────────────────────────────────────────────────────────────

public sealed record MasterRegisterListItemModel(
    Guid Id,
    string? PermanentUid,
    string? DocumentCode,
    string DocumentTitle,
    string DocumentClass,
    string Criticality,
    string LifecycleStatus,
    string RegisterStatus,
    bool IsSystemAllocated,
    Guid? ControlledDocumentId,
    Guid? OwnerCompanyId,
    DateTimeOffset? EffectiveDate,
    DateTimeOffset? NextReviewDueDate,
    DateTimeOffset CreatedAt,
    // "Record" | "ControlledDocument" — drives the list Kind column + client-side Kind filter.
    string DocumentKind);

public sealed record MasterRegisterDetailModel(
    Guid Id,
    string? PermanentUid,
    string? DocumentCode,
    string? LegacyCode,
    bool IsSystemAllocated,
    string DocumentTitle,
    string DocumentType,
    string DocumentClass,
    string Criticality,
    string? ProcessOwnerRole,
    Guid? ProcessOwnerUserId,
    Guid? AuthorUserId,
    string? OwnerFunction,
    Guid? OwnerCompanyId,
    string? GoverningLanguage,
    string? ApprovedRepositoryId,
    string? ApprovedRepositoryName,
    string? ApprovedRepositoryPath,
    string? CurrentVersionLabel,
    int? CurrentVersionNumber,
    string LifecycleStatus,
    string RegisterStatus,
    DateTimeOffset? EffectiveDate,
    int? ReviewCycleMonths,
    DateTimeOffset? NextReviewDueDate,
    DateTimeOffset? LastPeriodicReviewDate,
    string? RetentionClass,
    bool IsControlledDocument,
    bool IsRecord,
    bool IsExternalDocument,
    bool IsTemplate,
    bool IsVariant,
    string? ParentDocumentUid,
    string? ParentDocumentCode,
    string? ParentVersionLabel,
    string? SourceSystem,
    string? SourceLegacyId,
    Guid? ControlledDocumentId,
    Guid? TemplateDocumentId,
    Guid? TemplateMasterId,
    Guid? TemplateVariantId,
    // Extension-point read-through (FU10 populates; null in FU06).
    string? LastReleaseGateEvaluationStatus,
    DateTimeOffset? LastReleaseGateEvaluationAt,
    string? ApprovalEvidenceStatus,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    string DocumentScope,
    Guid ScopeOwnerId,
    Guid CorporateOwnerId,
    Guid CollectionInstanceId,
    Guid FolderId,
    string LinkScopeCompatibilityStatus,
    DateTimeOffset? ControlledDocumentLinkedAt,
    string? ControlledDocumentLinkedBy,
    string? ControlledDocumentLinkReason);

public sealed record MasterRegisterSummaryModel(
    int Total,
    IReadOnlyDictionary<string, int> ByRegisterStatus,
    IReadOnlyDictionary<string, int> ByLifecycleStatus,
    IReadOnlyDictionary<string, int> ByCriticality,
    IReadOnlyDictionary<string, int> ByClass,
    int WithPermanentUid,
    int WithoutPermanentUid,
    int LinkedToControlledDocument);

// ── wire helpers (string ⇄ enum; case-insensitive parse) ─────────────────────────

public static class MasterRegisterWire
{
    public static ControlledDocumentClass? ParseClass(string? value) =>
        Enum.TryParse<ControlledDocumentClass>(value, true, out var v) ? v : null;

    public static DocumentCriticality? ParseCriticality(string? value) =>
        Enum.TryParse<DocumentCriticality>(value, true, out var v) ? v : null;

    public static DocumentType? ParseDocumentType(string? value) =>
        Enum.TryParse<DocumentType>(value, true, out var v) ? v : null;

    public static DocumentRegisterStatus? ParseRegisterStatus(string? value) =>
        Enum.TryParse<DocumentRegisterStatus>(value, true, out var v) ? v : null;

    public static ControlledDocumentLifecycleStatus? ParseLifecycleStatus(string? value) =>
        Enum.TryParse<ControlledDocumentLifecycleStatus>(value, true, out var v) ? v : null;

    public static MasterRegisterListFilter ToFilter(
        string? registerStatus, string? lifecycleStatus, string? criticality, string? documentClass, Guid? ownerCompanyId) =>
        new(ParseRegisterStatus(registerStatus), ParseLifecycleStatus(lifecycleStatus),
            ParseCriticality(criticality), ParseClass(documentClass),
            ownerCompanyId == Guid.Empty ? null : ownerCompanyId);

    public static MasterRegisterListItemModel ToListItem(DocumentMasterRegisterEntry e) => new(
        e.Id, e.PermanentUid, e.DocumentCode, e.DocumentTitle,
        e.DocumentClass.ToString(), e.Criticality.ToString(),
        e.LifecycleStatus.ToString(), e.RegisterStatus.ToString(),
        e.IsSystemAllocated, e.ControlledDocumentId, e.OwnerCompanyId,
        e.EffectiveDate, e.NextReviewDueDate, e.CreatedAt,
        e.IsRecord ? "Record" : "ControlledDocument");

    public static MasterRegisterDetailModel ToDetail(DocumentMasterRegisterEntry e) => new(
        e.Id, e.PermanentUid, e.DocumentCode, e.LegacyCode, e.IsSystemAllocated,
        e.DocumentTitle, e.DocumentType.ToString(), e.DocumentClass.ToString(), e.Criticality.ToString(),
        e.ProcessOwnerRole, e.ProcessOwnerUserId, e.AuthorUserId, e.OwnerFunction, e.OwnerCompanyId, e.GoverningLanguage,
        e.ApprovedRepositoryId, e.ApprovedRepositoryName, e.ApprovedRepositoryPath,
        e.CurrentVersionLabel, e.CurrentVersionNumber, e.LifecycleStatus.ToString(), e.RegisterStatus.ToString(),
        e.EffectiveDate, e.ReviewCycleMonths, e.NextReviewDueDate, e.LastPeriodicReviewDate, e.RetentionClass,
        e.IsControlledDocument, e.IsRecord, e.IsExternalDocument, e.IsTemplate, e.IsVariant,
        e.ParentDocumentUid, e.ParentDocumentCode, e.ParentVersionLabel, e.SourceSystem, e.SourceLegacyId,
        e.ControlledDocumentId, e.TemplateDocumentId, e.TemplateMasterId, e.TemplateVariantId,
        e.LastReleaseGateEvaluationStatus, e.LastReleaseGateEvaluationAt, e.ApprovalEvidenceStatus,
        e.CreatedAt, e.CreatedBy, e.UpdatedAt, e.UpdatedBy,
        e.DocumentScope.ToString(), e.ScopeOwnerId, e.CorporateOwnerId, e.CollectionInstanceId, e.FolderId,
        e.LinkScopeCompatibilityStatus.ToString(), e.ControlledDocumentLinkedAt,
        e.ControlledDocumentLinkedBy, e.ControlledDocumentLinkReason);
}
