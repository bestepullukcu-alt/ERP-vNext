using Diten.Platform.Domain.Entities.DocumentManagement;
using Diten.Platform.Domain.Enums.DocumentManagement;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.DocumentManagementIdentifiers;

// MOD-0029-FU07 — Document identifier (Permanent UID / Document Code) allocation contracts, permission constants,
// reason codes, configurable coding options and the type-code resolver, kept in one file (Golden Reference Compact).

/// <summary>
/// MOD-0029-FU07 — RECOMMENDED Layer 1 RBAC keys. NOT seeded in this FU (no AuthService change): the controller
/// reuses the already-seeded controlled-documents create/view keys. FU06A/FU07 hardening should seed these.
/// </summary>
public static class DocumentIdentifierPermissions
{
    public const string Allocate = "platform.document-management.identifiers.allocate";
    public const string Reserve = "platform.document-management.identifiers.reserve";
    public const string Cancel = "platform.document-management.identifiers.cancel";
    public const string View = "platform.document-management.identifiers.view";
}

public static class IdentifierAllocationReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string ManualIdentifierExists = "MANUAL_IDENTIFIER_EXISTS";
    public const string DuplicateIdentifier = "DUPLICATE_IDENTIFIER";
    public const string TypeMappingMissing = "TYPE_MAPPING_MISSING";
    public const string RecordNotEligible = "RECORD_NOT_ELIGIBLE";
    public const string ExternalNotEligible = "EXTERNAL_NOT_ELIGIBLE";
    public const string VariantInheritsParent = "VARIANT_INHERITS_PARENT";
    public const string EntryNotAllocatable = "ENTRY_NOT_ALLOCATABLE";
    public const string PermissionDenied = "PERMISSION_DENIED";
}

/// <summary>
/// MOD-0029-FU07 — minimal configurable coding rules (GMG-QMS-SOP-0001 §6.3; the full Coding Register / LOG-0006 is a
/// future FU). Bound from config section <c>DocumentManagement:Coding</c>; defaults match the SOP examples
/// (<c>GMG-QMS-SOP-0001</c>, Permanent UID <c>UID-0000001</c>).
/// </summary>
public sealed class DocumentCodingOptions
{
    public const string SectionName = "DocumentManagement:Coding";

    public string OrgPrefix { get; set; } = "GMG";
    public string DomainCode { get; set; } = "QMS";
    public string UidPrefix { get; set; } = "UID";
    public int UidPadding { get; set; } = 7;
    public int CodePadding { get; set; } = 4;
}

/// <summary>
/// MOD-0029-FU07 — resolves a document TYPE CODE (SOP §6.1 classes) for the Document Code. Class is primary; for the
/// bundled Form/Template/Register/Matrix/Plan/Checklist class and <c>Other</c>, it falls back to DocumentType.
/// Returns null when no deterministic mapping exists → allocation is blocked (TYPE_MAPPING_MISSING).
/// </summary>
public static class DocumentTypeCodeResolver
{
    public static string? Resolve(ControlledDocumentClass documentClass, DocumentType documentType) => documentClass switch
    {
        ControlledDocumentClass.PolicyGovernance => "POL",
        ControlledDocumentClass.ManualSystemDescription => "MAN",
        ControlledDocumentClass.Sop => "SOP",
        ControlledDocumentClass.WorkInstruction => "WI",
        ControlledDocumentClass.QualityTechnicalAgreementSdea => "AGR",
        ControlledDocumentClass.UrgentTemporaryInstruction => "TMP",
        // Bundled class / Other: the class alone is not deterministic — fall back to the document type.
        ControlledDocumentClass.FormTemplateRegisterMatrixPlanChecklist or ControlledDocumentClass.Other => FromType(documentType),
        _ => null
    };

    private static string? FromType(DocumentType documentType) => documentType switch
    {
        DocumentType.Form => "FRM",
        DocumentType.Template => "TPL",
        DocumentType.Sop => "SOP",
        DocumentType.WorkInstruction => "WI",
        DocumentType.Policy => "POL",
        _ => null
    };
}

// ── inputs ───────────────────────────────────────────────────────────────────

public sealed record AllocateIdentifierInput(string? AllocationReason);

/// <summary>MOD-0029-FU07 — manual/migration reservation of an externally-owned UID or code.</summary>
public sealed record ReserveIdentifierInput(
    string IdentifierType,
    string IdentifierValue,
    Guid? RegisterEntryId,
    string? AllocationReason,
    string? LegacyCode,
    string? SourceSystem,
    string? SourceLegacyId);

public sealed record CancelIdentifierInput(string? CancellationReason);

// ── output models ────────────────────────────────────────────────────────────

public sealed record IdentifierAllocationModel(
    Guid Id,
    string IdentifierType,
    string IdentifierValue,
    long? SequenceNumber,
    string? Prefix,
    string? DomainCode,
    string? TypeCode,
    Guid? RegisterEntryId,
    Guid? ControlledDocumentId,
    string AllocationStatus,
    string AllocationReason,
    bool IsSystemAllocated,
    string? LegacyCode,
    string? SourceSystem,
    string? SourceLegacyId,
    DateTimeOffset AllocatedAt,
    string? AllocatedBy);

/// <summary>Returned by allocate-uid / allocate-code / allocate-identifiers: the register entry's resulting identity.</summary>
public sealed record IdentifierAllocationResultModel(
    Guid RegisterEntryId,
    string? PermanentUid,
    string? DocumentCode,
    bool IsSystemAllocated,
    IdentifierAllocationModel? UidAllocation,
    IdentifierAllocationModel? CodeAllocation);

public static class IdentifierWire
{
    public static DocumentIdentifierType? ParseType(string? value) =>
        Enum.TryParse<DocumentIdentifierType>(value, true, out var v) ? v : null;

    public static DocumentIdentifierAllocationStatus? ParseStatus(string? value) =>
        Enum.TryParse<DocumentIdentifierAllocationStatus>(value, true, out var v) ? v : null;

    public static DocumentIdentifierAllocationReason ParseReason(string? value) =>
        Enum.TryParse<DocumentIdentifierAllocationReason>(value, true, out var v) ? v : DocumentIdentifierAllocationReason.NewDocument;

    public static IdentifierAllocationListFilter ToFilter(string? type, string? status, Guid? registerEntryId) =>
        new(ParseType(type), ParseStatus(status), registerEntryId == Guid.Empty ? null : registerEntryId);

    public static IdentifierAllocationModel ToModel(DocumentIdentifierAllocation a) => new(
        a.Id, a.IdentifierType.ToString(), a.IdentifierValue, a.SequenceNumber, a.Prefix, a.DomainCode, a.TypeCode,
        a.RegisterEntryId, a.ControlledDocumentId, a.AllocationStatus.ToString(), a.AllocationReason.ToString(),
        a.IsSystemAllocated, a.LegacyCode, a.SourceSystem, a.SourceLegacyId, a.AllocatedAt, a.AllocatedBy);
}
