using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateMasters;

public static class DocumentManagementTemplateMasterPermissions
{
    public const string View = "platform.document-management.template-masters.view";
    public const string Create = "platform.document-management.template-masters.create";
    public const string VersionPublish = "platform.document-management.template-masters.version.publish";
    public const string Deprecate = "platform.document-management.template-masters.deprecate";
    public const string ImpactView = "platform.document-management.template-masters.impact.view";
    public const string Delete = "platform.document-management.template-masters.delete";
    public const string Manage = "platform.document-management.template-masters.manage";
}

public static class TemplateMasterReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Conflict = "CONFLICT";
    public const string DuplicateMasterCode = "DUPLICATE_MASTER_CODE";
    public const string DuplicateVersion = "DUPLICATE_VERSION";
    public const string NoContentChange = "NO_CONTENT_CHANGE";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string InvalidLifecycleTransition = "INVALID_LIFECYCLE_TRANSITION";
    public const string StorageUnavailable = "STORAGE_UNAVAILABLE";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

public static class TemplateMasterClassifications
{
    public static readonly IReadOnlyList<string> Allowed =
    [
        "SOP",
        "WORK_INSTRUCTION",
        "POLICY",
        "FORM",
        "TEMPLATE",
        "OTHER"
    ];

    public static bool IsAllowed(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Allowed.Contains(Normalize(value), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

public sealed record TemplateMasterListFilter(
    string? Status,
    string? Classification,
    Guid? CollectionDefinitionId,
    string? CanonicalId,
    string? VariantPolicy);

public sealed record CreateTemplateMasterInput(
    string MasterCode,
    string TemplateName,
    string? Description,
    string Classification,
    Guid? CollectionDefinitionId,
    string? CanonicalId,
    string VariantPolicy,
    Guid? OwnerCompanyId,
    Guid? OwnerUserId,
    DateTimeOffset? EffectiveDate);

public sealed record PublishTemplateMasterVersionInput(
    FileUploadInput File,
    string? ChangeSummary,
    bool AllowUnchanged);

public sealed record DeprecateTemplateMasterInput(string? DeprecationReason);

public sealed record TemplateMasterListItemModel(
    Guid Id,
    string MasterCode,
    string TemplateName,
    int CurrentMasterVersion,
    Guid? CurrentVersionId,
    string Status,
    string Classification,
    Guid? CollectionDefinitionId,
    string? CanonicalId,
    string VariantPolicy,
    Guid? OwnerCompanyId,
    Guid? OwnerUserId,
    int UsageCount,
    int VariantCount,
    DateTimeOffset CreatedAt,
    bool CanPublish = true,
    bool CanDeprecate = true,
    bool CanDelete = true,
    bool CanImpact = true);

public sealed record TemplateMasterDetailModel(
    Guid Id,
    string MasterCode,
    string TemplateName,
    string? Description,
    string Classification,
    Guid? CollectionDefinitionId,
    string? CanonicalId,
    string VariantPolicy,
    Guid? OwnerCompanyId,
    Guid? OwnerUserId,
    string Status,
    Guid? CurrentVersionId,
    int CurrentMasterVersion,
    DateTimeOffset? EffectiveDate,
    DateTimeOffset? DeprecatedAt,
    string? DeprecatedBy,
    int UsageCount,
    int VariantCount,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record TemplateMasterVersionModel(
    Guid Id,
    Guid TemplateMasterId,
    int VersionNumber,
    string Status,
    DateTimeOffset? PublishedAt,
    string? PublishedBy,
    string? ChangeSummary,
    ContentRefModel File);

public sealed record TemplateMasterOptionModel(
    Guid Id,
    string MasterCode,
    string TemplateName,
    int CurrentMasterVersion,
    Guid? CurrentVersionId,
    string Classification,
    string VariantPolicy);

public sealed record TemplateMasterAdoptionImpactModel(
    Guid TemplateMasterId,
    int UsageCount,
    int VariantCount,
    int FolderAttachedTemplateCount,
    string Summary);

public static class TemplateMasterWire
{
    public static string ToWire(this TemplateMasterStatus v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this TemplateVariantPolicy v) => v.ToString().ToUpperInvariant();
    public static string ToWire(this TemplateMasterVersionStatus v) => v.ToString().ToUpperInvariant();

    public static TemplateVariantPolicy? ParseVariantPolicy(string? value) => (value?.Trim().ToUpperInvariant()) switch
    {
        "ALLOWED" => TemplateVariantPolicy.Allowed,
        "LOCKED" => TemplateVariantPolicy.Locked,
        _ => null
    };

    public static TemplateMasterListItemModel ToListItem(
        TemplateMaster master,
        int usageCount = 0,
        int variantCount = 0,
        bool canPublish = true,
        bool canDeprecate = true,
        bool canDelete = true,
        bool canImpact = true) => new(
        master.Id,
        master.MasterCode,
        master.TemplateName,
        master.CurrentMasterVersion,
        master.CurrentVersionId,
        master.Status.ToWire(),
        master.Classification,
        master.CollectionDefinitionId,
        master.CanonicalId,
        master.VariantPolicy.ToWire(),
        master.OwnerCompanyId,
        master.OwnerUserId,
        master.UsageCount ?? usageCount,
        master.VariantCount ?? variantCount,
        master.CreatedAt,
        canPublish,
        canDeprecate,
        canDelete,
        canImpact);

    public static TemplateMasterDetailModel ToDetail(TemplateMaster master, int usageCount = 0, int variantCount = 0) => new(
        master.Id,
        master.MasterCode,
        master.TemplateName,
        master.Description,
        master.Classification,
        master.CollectionDefinitionId,
        master.CanonicalId,
        master.VariantPolicy.ToWire(),
        master.OwnerCompanyId,
        master.OwnerUserId,
        master.Status.ToWire(),
        master.CurrentVersionId,
        master.CurrentMasterVersion,
        master.EffectiveDate,
        master.DeprecatedAt,
        master.DeprecatedBy,
        master.UsageCount ?? usageCount,
        master.VariantCount ?? variantCount,
        master.CreatedAt,
        master.CreatedBy);

    public static TemplateMasterVersionModel ToVersionModel(TemplateMasterVersion version) => new(
        version.Id,
        version.TemplateMasterId,
        version.VersionNumber,
        version.Status.ToWire(),
        version.PublishedAt,
        version.PublishedBy,
        version.ChangeSummary,
        new ContentRefModel(
            version.FileRef.ContentId,
            version.FileRef.FileName,
            version.FileRef.MediaType,
            version.FileRef.ByteSize,
            version.FileRef.Checksum));
}
