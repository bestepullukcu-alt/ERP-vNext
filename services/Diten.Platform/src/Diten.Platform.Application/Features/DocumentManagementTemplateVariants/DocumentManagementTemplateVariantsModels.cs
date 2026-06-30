using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants;

public static class DocumentManagementTemplateVariantPermissions
{
    public const string View = "platform.document-management.template-variants.view";
    public const string Create = "platform.document-management.template-variants.create";
    public const string Compare = "platform.document-management.template-variants.compare";
    public const string Rebase = "platform.document-management.template-variants.rebase";
    public const string Manage = "platform.document-management.template-variants.manage";
}

public static class TemplateVariantReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Conflict = "CONFLICT";
    public const string DuplicateVariantCode = "DUPLICATE_VARIANT_CODE";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string MasterInactive = "MASTER_INACTIVE";
    public const string InvalidMasterVersion = "INVALID_MASTER_VERSION";
    public const string InvalidMasterContent = "INVALID_MASTER_CONTENT";
    public const string InvalidTargetFolder = "INVALID_TARGET_FOLDER";
    public const string InvalidContentSource = "INVALID_CONTENT_SOURCE";
    public const string LocalFileRequired = "LOCAL_FILE_REQUIRED";
    public const string LocalFileNotAllowed = "LOCAL_FILE_NOT_ALLOWED";
    public const string StorageUnavailable = "STORAGE_UNAVAILABLE";
    public const string InvalidScope = "INVALID_SCOPE";
    public const string LinkedTemplateCreateFailed = "LINKED_TEMPLATE_CREATE_FAILED";
    public const string RebaseBlocked = "REBASE_BLOCKED";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

public sealed record TemplateVariantListFilter(
    Guid? TemplateMasterId,
    string? ScopeType,
    Guid? ScopeId,
    string? Status,
    string? ApprovalStatus);

public sealed record CreateTemplateVariantInput(
    Guid TemplateMasterId,
    Guid TemplateMasterVersionId,
    string VariantCode,
    string VariantName,
    string? Description,
    string ScopeType,
    Guid ScopeId,
    Guid TargetCollectionInstanceId,
    string ContentSource,
    FileUploadInput? LocalFile,
    Guid? OwnerCompanyId,
    Guid? OwnerUserId,
    string? Status);

public sealed record RebaseTemplateVariantInput(Guid? TargetMasterVersionId);

public sealed record TemplateVariantListItemModel(
    Guid Id,
    string VariantCode,
    string VariantName,
    Guid TemplateMasterId,
    string? MasterCode,
    string? MasterTemplateName,
    int MasterCurrentVersion,
    Guid? LastRebasedMasterVersionId,
    int? LastRebasedMasterVersionNumber,
    DateTimeOffset? LastRebasedAt,
    string DriftStatus,
    string ScopeType,
    Guid ScopeId,
    string Status,
    Guid? OwnerCompanyId,
    Guid? OwnerUserId,
    bool HasLocalChanges,
    string ApprovalStatus,
    DateTimeOffset CreatedAt,
    bool CanCompare = true,
    bool CanRebase = true);

public sealed record TemplateVariantDetailModel(
    Guid Id,
    string VariantCode,
    string VariantName,
    string? Description,
    Guid TemplateMasterId,
    string? MasterCode,
    string? MasterTemplateName,
    Guid TemplateMasterVersionId,
    int MasterCurrentVersion,
    string ScopeType,
    Guid ScopeId,
    Guid? OwnerCompanyId,
    Guid? OwnerUserId,
    string Status,
    Guid? LastRebasedMasterVersionId,
    int? LastRebasedMasterVersionNumber,
    DateTimeOffset? LastRebasedAt,
    Guid? CurrentVariantVersionId,
    Guid? LinkedTemplateDocumentId,
    string ContentSource,
    bool UsesMasterContent,
    string? LinkedTemplateDocumentTitle,
    Guid? CollectionInstanceId,
    string? CollectionPath,
    int? TemplateDocumentCurrentVersion,
    bool ContentLinked,
    bool HasLocalChanges,
    string DriftStatus,
    string ApprovalStatus,
    Guid? ApprovalRequestId,
    string? BlockedReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy);

public sealed record TemplateVariantCompareModel(
    Guid VariantId,
    string VariantCode,
    string VariantName,
    string VariantStatus,
    Guid TemplateMasterId,
    string? MasterCode,
    string? MasterTemplateName,
    string MasterStatus,
    int MasterCurrentVersion,
    int? VariantLastRebasedVersionNumber,
    bool HasLocalChanges,
    string ApprovalStatus,
    string DriftStatus,
    string ContentSource,
    bool UsesMasterContent,
    bool? ChecksumEqual,
    Guid? LinkedTemplateDocumentId,
    string? LinkedTemplateDocumentTitle,
    Guid? CollectionInstanceId,
    string? CollectionPath,
    int? TemplateDocumentCurrentVersion,
    bool ContentLinked,
    string Summary);

public sealed record TemplateVariantOptionModel(
    Guid TemplateMasterId,
    string MasterCode,
    string TemplateName,
    int CurrentMasterVersion,
    Guid? CurrentVersionId,
    string Status,
    string Classification);

public static class TemplateVariantWire
{
    public static string ToWire(this TemplateVariantScopeType v) => v.ToString();
    public static string ToWire(this TemplateVariantStatus v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this TemplateVariantDriftStatus v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this TemplateVariantApprovalStatus v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this TemplateVariantContentSource v) => v switch
    {
        TemplateVariantContentSource.LocalUpload => "LOCAL_UPLOAD",
        _ => "MASTER_VERSION"
    };

    public static TemplateVariantScopeType? ParseScopeType(string? value) => (value?.Trim().ToUpperInvariant()) switch
    {
        "COMPANY" => TemplateVariantScopeType.Company,
        "BUSINESSUNIT" or "BUSINESS_UNIT" => TemplateVariantScopeType.BusinessUnit,
        "SITE" => TemplateVariantScopeType.Site,
        _ => null
    };

    public static TemplateVariantStatus? ParseStatus(string? value) => (value?.Trim().ToUpperInvariant()) switch
    {
        "DRAFT" => TemplateVariantStatus.Draft,
        "ACTIVE" => TemplateVariantStatus.Active,
        "DEPRECATED" => TemplateVariantStatus.Deprecated,
        "ARCHIVED" => TemplateVariantStatus.Archived,
        _ => null
    };

    public static TemplateVariantContentSource? ParseContentSource(string? value) => (value?.Trim().ToUpperInvariant()) switch
    {
        "MASTER_VERSION" or "MASTERVERSION" => TemplateVariantContentSource.MasterVersion,
        "LOCAL_UPLOAD" or "LOCALUPLOAD" => TemplateVariantContentSource.LocalUpload,
        _ => null
    };

    public static TemplateVariantListItemModel ToListItem(
        TemplateVariant variant,
        TemplateMaster? master,
        TemplateVariantDriftStatus drift,
        bool canCompare = true,
        bool canRebase = true) => new(
        variant.Id,
        variant.VariantCode,
        variant.VariantName,
        variant.TemplateMasterId,
        master?.MasterCode,
        master?.TemplateName,
        master?.CurrentMasterVersion ?? 0,
        variant.LastRebasedMasterVersionId,
        variant.LastRebasedMasterVersionNumber,
        variant.LastRebasedAt,
        drift.ToWire(),
        variant.ScopeType.ToWire(),
        variant.ScopeId,
        variant.Status.ToWire(),
        variant.OwnerCompanyId,
        variant.OwnerUserId,
        variant.HasLocalChanges,
        variant.ApprovalStatus.ToWire(),
        variant.CreatedAt,
        canCompare,
        canRebase);

    public static TemplateVariantDetailModel ToDetail(
        TemplateVariant variant,
        TemplateMaster? master,
        TemplateVariantDriftStatus drift,
        TemplateDocument? linkedTemplate = null) => new(
        variant.Id,
        variant.VariantCode,
        variant.VariantName,
        variant.Description,
        variant.TemplateMasterId,
        master?.MasterCode,
        master?.TemplateName,
        variant.TemplateMasterVersionId,
        master?.CurrentMasterVersion ?? 0,
        variant.ScopeType.ToWire(),
        variant.ScopeId,
        variant.OwnerCompanyId,
        variant.OwnerUserId,
        variant.Status.ToWire(),
        variant.LastRebasedMasterVersionId,
        variant.LastRebasedMasterVersionNumber,
        variant.LastRebasedAt,
        variant.CurrentVariantVersionId,
        variant.LinkedTemplateDocumentId,
        variant.ContentSource.ToWire(),
        variant.ContentSource == TemplateVariantContentSource.MasterVersion,
        linkedTemplate?.Title,
        linkedTemplate?.CollectionInstanceId,
        linkedTemplate?.CollectionPath,
        linkedTemplate?.CurrentVersionNumber,
        variant.LinkedTemplateDocumentId.HasValue && linkedTemplate?.CurrentVersionId.HasValue == true,
        variant.HasLocalChanges,
        drift.ToWire(),
        variant.ApprovalStatus.ToWire(),
        variant.ApprovalRequestId,
        variant.BlockedReason,
        variant.CreatedAt,
        variant.CreatedBy,
        variant.UpdatedAt,
        variant.UpdatedBy);
}
