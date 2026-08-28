using Diten.Platform.Application.Features.DocumentManagementControlledDocuments;
using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Application.Features.DocumentManagementControlledDocumentRegistration;

public static class ControlledDocumentRegistrationPermissions
{
    public const string View = "platform.document-management.master-register.registration.view";
    public const string Create = "platform.document-management.master-register.registration.create";
    public const string Reconcile = "platform.document-management.master-register.registration.reconcile";
}

public static class ControlledDocumentRegistrationReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string PermissionDenied = "PERMISSION_DENIED";
    public const string AlreadyLinked = "ALREADY_LINKED";
    public const string TemplateFlowRequired = "TEMPLATE_FLOW_REQUIRED";
    public const string StorageFailed = "STORAGE_FAILED";
    public const string DocumentCreateFailed = "DOCUMENT_CREATE_FAILED";
    public const string RegisterCreateFailed = "REGISTER_CREATE_FAILED";
    public const string LinkFailed = "LINK_FAILED";
    public const string DuplicateDocumentTitle = "DUPLICATE_DOCUMENT_TITLE";
    public const string DuplicateRecordCode = "DUPLICATE_RECORD_CODE";
    public const string VariantParentNotFound = "VARIANT_PARENT_NOT_FOUND";
    public const string VariantContentUnchanged = "VARIANT_CONTENT_UNCHANGED";
    public const string RegistrationFailed = "REGISTRATION_FAILED";
}

public sealed record CreateControlledDocumentRegistrationInput(
    string IdempotencyKey,
    string DocumentTitle,
    string DocumentClass,
    string Criticality,
    string DocumentType,
    string? Description,
    IReadOnlyList<string>? Tags,
    string GoverningLanguage,
    string? OwnerFunction,
    Guid OwnerCompanyId,
    string? ProcessOwnerRole,
    Guid? ProcessOwnerUserId,
    int? ReviewCycleMonths,
    string? RetentionClass,
    Guid CompanyId,
    Guid CollectionInstanceId,
    FileUploadInput InitialFile)
{
    public DocumentScope DocumentScope { get; init; } = DocumentScope.Company;
    public Guid CorporateOwnerId { get; init; }
    public Guid FolderId { get; init; }
    public Guid? AuthorUserId { get; init; }
    public string? GoverningLanguageId { get; init; }
    public string? RetentionClassId { get; init; }
    // Default keeps existing callers producing controlled documents (backward compatible).
    public RegistrationKind Kind { get; init; } = RegistrationKind.ControlledDocument;
    // Optional manual code for RECORDS only (records are not eligible for the FU07 governed allocation engine).
    // Distinct from the governed DocumentCode; ignored for controlled documents, whose code the engine allocates.
    public string? RecordCode { get; init; }
    // VARIANT-only (Faz 2a). Parent controlled document this variant derives from + locale metadata. Ignored otherwise.
    public Guid? ParentRegisterEntryId { get; init; }
    public DocumentVariantType VariantType { get; init; } = DocumentVariantType.Translation;
    public string? LanguageCode { get; init; }
    public string? CountryCode { get; init; }
    public string? SiteCode { get; init; }
}

public sealed record ControlledDocumentRegistrationOperationModel(
    Guid OperationId,
    string Status,
    Guid? ControlledDocumentId,
    Guid? ControlledDocumentVersionId,
    Guid? MasterRegisterEntryId,
    string? ContentSha256,
    string? FailureReasonCode,
    string? FailureDetail,
    DateTimeOffset? LastAttemptAt,
    int AttemptCount,
    string CorrelationId,
    string DocumentScope,
    Guid ScopeOwnerId,
    Guid? CompanyId,
    Guid? OwnerCompanyId,
    Guid? CorporateOwnerId,
    Guid CollectionInstanceId,
    Guid FolderId,
    string? StoragePartition,
    Guid? BaselineReleaseId,
    string? GoverningLanguageId,
    string? RetentionClassId);

public sealed record ControlledDocumentRegistrationResultModel(
    Guid OperationId,
    Guid ControlledDocumentId,
    Guid ControlledDocumentVersionId,
    Guid MasterRegisterEntryId,
    string Status,
    string CorrelationId);

public sealed record RetryControlledDocumentRegistrationResultModel(
    ControlledDocumentRegistrationOperationModel Operation,
    bool Resumed,
    bool Completed);

public sealed record MasterRegisterByControlledDocumentModel(
    Guid ControlledDocumentId,
    Guid MasterRegisterEntryId,
    string DocumentTitle,
    string RegisterStatus,
    string LifecycleStatus,
    string DocumentScope,
    Guid ScopeOwnerId,
    Guid? OwnerCompanyId,
    Guid CorporateOwnerId,
    string DocumentClass,
    string DocumentType,
    string LinkCompatibilityStatus,
    DateTimeOffset? LinkedAt,
    string? LinkedBy,
    string? LinkReason);

public static class ControlledDocumentRegistrationMapping
{
    public static ControlledDocumentRegistrationOperationModel ToModel(ControlledDocumentRegistrationOperation operation) => new(
        operation.Id,
        operation.Status.ToString(),
        operation.ControlledDocumentId,
        operation.ControlledDocumentVersionId,
        operation.MasterRegisterEntryId,
        operation.ContentSha256,
        operation.FailureReasonCode,
        operation.FailureDetail,
        operation.LastAttemptAt,
        operation.AttemptCount,
        operation.CorrelationId,
        operation.DocumentScope.ToString(),
        operation.ScopeOwnerId,
        operation.DocumentScope == DocumentScope.Company ? operation.CompanyId : null,
        operation.DocumentScope == DocumentScope.Company ? operation.OwnerCompanyId : null,
        operation.DocumentScope == DocumentScope.Corporate ? operation.CorporateOwnerId : null,
        operation.CollectionInstanceId,
        operation.FolderId,
        operation.StoragePartition,
        operation.BaselineReleaseId,
        operation.GoverningLanguageId,
        operation.RetentionClassId);
}
