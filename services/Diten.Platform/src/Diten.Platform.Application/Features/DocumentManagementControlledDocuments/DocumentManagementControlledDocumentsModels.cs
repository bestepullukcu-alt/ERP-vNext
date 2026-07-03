using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocuments;

// MOD-0029-FU01 — controlled-document / template / versioning / sharing contracts, permission constants,
// reason codes, feature flags, storage options and wire-enum mappings, kept in one file (Golden Reference
// Compact convention, mirroring DocumentManagementInstantiationModels.cs).

/// <summary>Layer 1 central RBAC catalog keys (seeded in AuthService DataSeeder; PKS-001 lowercase dotted).</summary>
public static class DocumentManagementControlledDocumentsPermissions
{
    public const string ControlledDocumentsView = "platform.document-management.controlled-documents.view";
    public const string ControlledDocumentsCreate = "platform.document-management.controlled-documents.create";
    public const string ControlledDocumentsVersionCreate = "platform.document-management.controlled-documents.version.create";
    public const string ControlledDocumentsVersionView = "platform.document-management.controlled-documents.version.view";
    public const string ControlledDocumentsShare = "platform.document-management.controlled-documents.share";
    public const string ControlledDocumentsAccessManage = "platform.document-management.controlled-documents.access.manage";
    public const string TemplatesView = "platform.document-management.templates.view";
    public const string TemplatesCreate = "platform.document-management.templates.create";
    public const string TemplatesVersionCreate = "platform.document-management.templates.version.create";
    public const string TemplatesShare = "platform.document-management.templates.share";
    public const string FolderDocumentsUpload = "platform.document-management.folder-documents.upload";
    public const string FolderDocumentsAccessManage = "platform.document-management.folder-documents.access.manage";
    public const string FolderSharesCreate = "platform.document-management.folder-shares.create";
    public const string FolderSharesView = "platform.document-management.folder-shares.view";
}

public static class ControlledDocumentReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Conflict = "CONFLICT";
    public const string NoContentChange = "NO_CONTENT_CHANGE";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string PermissionDenied = "PERM_DENIED";
    public const string StorageUnavailable = "STORAGE_UNAVAILABLE";
    public const string DependencyUnavailable = "DEPENDENCY_UNAVAILABLE";
    public const string FeatureDisabled = "FEATURE_DISABLED";
}

/// <summary>FU01 typed feature flags (reused FU01/FU02 pattern).</summary>
public sealed class ControlledDocumentsFeatureFlagOptions
{
    public const string SectionName = "DocumentManagement:ControlledDocuments:FeatureFlags";

    public bool ControlledDocumentsEnabled { get; set; } = true;        // mod0029.controlled_documents.enabled
    public bool TemplateSharingEnabled { get; set; } = true;            // mod0029.template_sharing.enabled
    public bool FolderShareCopyOnAdoptEnabled { get; set; }             // mod0029.folder_share_copy_on_adopt.enabled (off until copy lineage verified)
}

/// <summary>Phase 1 local content-storage config (config-driven root; never under wwwroot).</summary>
public sealed class ContentStorageOptions
{
    public const string SectionName = "DocumentManagement:ContentStorage";

    public string Provider { get; set; } = "local-filesystem";
    public string RootPath { get; set; } = string.Empty;
    public long MaxFileSizeBytes { get; set; } = 52_428_800; // 50 MB
    public List<string> AllowedExtensions { get; set; } =
        [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".csv", ".md", ".png", ".jpg", ".jpeg"];
    public List<string> AllowedMediaTypes { get; set; } = [];
}

// ── shared input value objects ───────────────────────────────────────────────

public sealed record FileUploadInput(string FileName, string? MediaType, string ContentBase64);

public sealed record AccessGrantInput(string Action, string TargetType, string TargetId);

public sealed record DocumentAccessPolicyInput(string? Source, IReadOnlyList<AccessGrantInput> Grants);

public sealed record TemplateFlagsInput(bool Reusable, bool Shareable, bool CopyableOnAdopt, bool ReferenceOnly);

public sealed record FolderPermissionsInput(
    bool CanViewFolderDocuments,
    bool CanUploadDocument,
    bool CanEditFolderDocuments,
    bool CanUploadNewVersion,
    bool CanShareFolderDocuments,
    bool CanManageFolderDocumentAccess);

// ── result models ────────────────────────────────────────────────────────────

public sealed record ContentRefModel(
    Guid ContentId,
    string FileName,
    string MediaType,
    long ByteSize,
    string Checksum);

public sealed record DocumentVersionModel(
    Guid Id,
    Guid DocumentId,
    int VersionNumber,
    string VersionStatus,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    string? ChangeSummary,
    ContentRefModel File);

public sealed record AccessGrantModel(string Action, string TargetType, string TargetId);

public sealed record AccessPolicyModel(string Source, IReadOnlyList<AccessGrantModel> Grants);

public sealed record ControlledDocumentListItemModel(
    Guid Id,
    string DocumentKey,
    string Title,
    string DocumentType,
    Guid CompanyId,
    Guid CollectionInstanceId,
    string CollectionPath,
    int CurrentVersionNumber,
    Guid? CurrentVersionId,
    string Status,
    bool Controlled,
    DateTimeOffset CreatedAt,
    bool IsFavorite = false);

public sealed record ControlledDocumentDetailModel(
    Guid Id,
    string DocumentKey,
    string Title,
    string DocumentType,
    string? Description,
    IReadOnlyList<string> Tags,
    Guid CompanyId,
    Guid OwnerCompanyId,
    Guid CollectionInstanceId,
    string CollectionPath,
    string? CanonicalId,
    bool Controlled,
    DateTimeOffset? EffectiveDate,
    DateTimeOffset? ReviewDate,
    DateTimeOffset? ExpiryDate,
    Guid? CurrentVersionId,
    int CurrentVersionNumber,
    string Status,
    AccessPolicyModel AccessPolicy,
    Guid? CopiedFromDocumentId,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record TemplateFlagsModel(bool Reusable, bool Shareable, bool CopyableOnAdopt, bool ReferenceOnly);

public sealed record TemplateListItemModel(
    Guid Id,
    string TemplateKey,
    string Title,
    Guid CompanyId,
    Guid? CollectionInstanceId,
    string? CollectionPath,
    int CurrentVersionNumber,
    string Status,
    TemplateFlagsModel Flags,
    DateTimeOffset CreatedAt);

public sealed record TemplateDetailModel(
    Guid Id,
    string TemplateKey,
    string Title,
    string? Description,
    IReadOnlyList<string> Tags,
    Guid CompanyId,
    Guid OwnerCompanyId,
    Guid? CollectionInstanceId,
    string? CollectionPath,
    string? CanonicalId,
    Guid? CurrentVersionId,
    int CurrentVersionNumber,
    string Status,
    TemplateFlagsModel Flags,
    AccessPolicyModel AccessPolicy,
    Guid? CopiedFromTemplateId,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record ShareResultModel(
    Guid ShareId,
    string ItemKind,
    Guid ItemId,
    Guid SourceCompanyId,
    Guid TargetCompanyId,
    string ShareMode,
    Guid? CopiedItemId,
    string CorrelationId);

public sealed record FolderDocumentsModel(
    Guid CollectionInstanceId,
    string CollectionPath,
    IReadOnlyList<ControlledDocumentListItemModel> Documents,
    IReadOnlyList<TemplateListItemModel> Templates);

public sealed record FolderAccessPolicyModel(
    Guid CollectionInstanceId,
    Guid CompanyId,
    string TargetType,
    string TargetId,
    FolderPermissionsInput Permissions);

public sealed record FolderShareOutcomeModel(
    string ItemType,
    string ItemKey,
    string Status,
    string ReasonCode,
    string Message,
    bool Retryable);

// ── Explorer: active documentation structures + permission-filtered search ───

/// <summary>An active, company-instantiated Documentation Structure (an instantiation group), surfaced as a
/// selectable Explorer root. Raw published baselines are never returned here.</summary>
public sealed record DocumentationStructureModel(
    Guid ActiveStructureId,
    Guid RootCollectionInstanceId,
    string DisplayName,
    Guid CompanyId,
    Guid? BaselineReleaseId,
    string? InstanceToken,
    string Status,
    int FolderCount,
    DateTimeOffset? AdoptedAt);

public sealed record SearchResultPermissions(
    bool CanView,
    bool CanDownload,
    bool CanEditMetadata,
    bool CanUploadNewVersion,
    bool CanShare,
    bool CanManageAccess);

/// <summary>One mixed Explorer search result (folder / document / template), permission-filtered server-side.</summary>
public sealed record ExplorerSearchResultModel(
    string ResultType,           // FOLDER / DOCUMENT / TEMPLATE
    Guid Id,
    string Name,
    string FullPath,
    Guid CollectionInstanceId,
    Guid? DocumentId,
    Guid? TemplateId,
    string? DocumentType,
    int? CurrentVersion,
    string Status,
    DateTimeOffset? ModifiedAt,
    SearchResultPermissions Permissions);

public sealed record ExplorerSearchResultModelList(
    Guid CompanyId,
    Guid ActiveStructureId,
    string Scope,
    string? Query,
    IReadOnlyList<ExplorerSearchResultModel> Results);

public sealed record FolderShareResultModel(
    Guid OperationId,
    Guid SourceCompanyId,
    Guid TargetCompanyId,
    Guid SourceBranchCollectionInstanceId,
    bool IncludeTemplates,
    string ShareMode,
    string OperationType,
    string Status,
    int FoldersIncluded,
    int TemplatesIncluded,
    int TemplatesSkipped,
    int Failed,
    int Total,
    string CorrelationId,
    IReadOnlyList<FolderShareOutcomeModel> Outcomes);

// ── wire-enum mapping (UPPER_SNAKE on the wire) ──────────────────────────────

public static class ControlledDocumentWire
{
    public static string ToWire(this DocumentType v) => v switch
    {
        DocumentType.Sop => "SOP",
        DocumentType.WorkInstruction => "WORK_INSTRUCTION",
        DocumentType.Policy => "POLICY",
        DocumentType.Form => "FORM",
        DocumentType.Template => "TEMPLATE",
        _ => "OTHER"
    };

    public static DocumentType ParseDocumentType(string? value) => (value?.Trim().ToUpperInvariant()) switch
    {
        "SOP" => DocumentType.Sop,
        "WORK_INSTRUCTION" or "WORKINSTRUCTION" => DocumentType.WorkInstruction,
        "POLICY" => DocumentType.Policy,
        "FORM" => DocumentType.Form,
        "TEMPLATE" => DocumentType.Template,
        "OTHER" => DocumentType.Other,
        _ => (DocumentType)(-1)
    };

    public static string ToWire(this DocumentVersionStatus v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this ControlledItemStatus v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this AccessPolicySource v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this SharedItemKind v) =>
        v == SharedItemKind.ControlledDocument ? "CONTROLLED_DOCUMENT" : "TEMPLATE";
    public static string ToWire(this FolderShareOperationType v) => v == FolderShareOperationType.DryRun ? "DRY_RUN" : "EXECUTE";
    public static string ToWire(this FolderShareStatus v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this FolderShareItemType v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this FolderShareOutcomeStatus v) => v.ToString().ToUpperInvariant();

    public static string ToWire(this DocumentShareMode v) => v == DocumentShareMode.CopyOnAdopt ? "COPY_ON_ADOPT" : "REFERENCE";
    public static DocumentShareMode ParseShareMode(string? value) => (value?.Trim().ToUpperInvariant()) switch
    {
        "COPY_ON_ADOPT" or "COPY_ON_SHARE" => DocumentShareMode.CopyOnAdopt,
        _ => DocumentShareMode.Reference
    };

    public static string ToWire(this DocumentAccessAction v) => v.ToString().ToUpperInvariant();
    public static DocumentAccessAction? ParseAccessAction(string? value) => (value?.Trim().ToUpperInvariant()) switch
    {
        "VIEW" => DocumentAccessAction.View,
        "DOWNLOAD" => DocumentAccessAction.Download,
        "EDIT" => DocumentAccessAction.Edit,
        "VERSION" => DocumentAccessAction.Version,
        "SHARE" => DocumentAccessAction.Share,
        "MANAGE_ACCESS" or "MANAGEACCESS" => DocumentAccessAction.ManageAccess,
        _ => null
    };

    public static string ToWire(this AccessTargetType v) => v switch
    {
        AccessTargetType.User => "USER",
        AccessTargetType.Role => "ROLE",
        AccessTargetType.Company => "COMPANY",
        AccessTargetType.Plant => "PLANT",
        AccessTargetType.BusinessUnit => "BUSINESS_UNIT",
        _ => "USER"
    };

    public static AccessTargetType? ParseTargetType(string? value) => (value?.Trim().ToUpperInvariant()) switch
    {
        "USER" => AccessTargetType.User,
        "ROLE" => AccessTargetType.Role,
        "COMPANY" => AccessTargetType.Company,
        "PLANT" => AccessTargetType.Plant,
        "BUSINESS_UNIT" or "BUSINESSUNIT" or "BU" => AccessTargetType.BusinessUnit,
        _ => null
    };
}
