using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Supplier;

// ── Contract version sabiti (SUPPLIER contract consumer slice; MVP-6 uyumu) ──
public static class SupplierContract
{
    public const string Version = "v1";
}

// ══ READ DTOs ════════════════════════════════════════════════════════════════

/// <summary>Embedded contact DTO (read surface — contract Contact).</summary>
public sealed record SupplierContactDto(
    string Type,
    string? Email,
    string? Phone,
    string? Name);

/// <summary>List item — SUPPLIER contract consumer slice (listSuppliers) şekliyle uyumlu görünüm.</summary>
public sealed record SupplierListItemDto(
    Guid Id,
    string SupplierId,
    string Name,
    SupplierStatus Status,
    string? Country,
    string? TaxId,
    IReadOnlyList<SupplierContactDto> Contacts,
    OnboardingStatus OnboardingStatus,
    string ContractVersion);

/// <summary>Sayfalı liste sonucu — contract listSuppliers {items, nextCursor, contractVersion}.</summary>
public sealed record SupplierListResultDto(
    IReadOnlyList<SupplierListItemDto> Items,
    string? NextCursor,
    string ContractVersion);

/// <summary>Detail — getSupplier consumer slice ile uyumlu (+ optimistic concurrency için Version).</summary>
public sealed record SupplierDetailDto(
    Guid Id,
    string SupplierId,
    string Name,
    SupplierStatus Status,
    string? Country,
    string? TaxId,
    IReadOnlyList<SupplierContactDto> Contacts,
    OnboardingStatus OnboardingStatus,
    KycOutcome KycOutcome,
    SanctionsOutcome SanctionsOutcome,
    string? SourceSystem,
    string? ExternalRef,
    int Version,
    string ContractVersion);

/// <summary>
/// validateSuppliers tek satır sonucu (bilinmeyen → known:false, status:null). MOD-0140 DL-3 (1.3.0, §3.2): the 1.2.0 fields
/// stay as they were (<c>supplierId</c> = the requested value) and five properties are added, written on EVERY row (null
/// when there is no relationship): <c>supplierMasterId</c>, <c>relationshipExists</c>, <c>onboardingStatus</c>,
/// <c>purchasingEnabled</c>, <c>eligibleForTransactions</c>. Invariant: <c>known == relationshipExists</c>.
/// </summary>
public sealed record SupplierValidationResultDto(
    string SupplierId,
    bool Known,
    SupplierStatus? Status,
    Guid? SupplierMasterId = null,
    bool RelationshipExists = false,
    OnboardingStatus? OnboardingStatus = null,
    bool? PurchasingEnabled = null,
    bool EligibleForTransactions = false);

/// <summary>validateSuppliers yanıtı (contract {results, contractVersion}).</summary>
public sealed record SupplierValidateResponseDto(
    IReadOnlyList<SupplierValidationResultDto> Results,
    string ContractVersion);

/// <summary>Onboarding doküman DTO (type + evidenceRef; binary yok).</summary>
public sealed record OnboardingDocumentDto(
    string Type,
    string EvidenceRef);

/// <summary>Onboarding onay bağlantısı DTO (MOD-0023 workflow instance + karar).</summary>
public sealed record OnboardingApprovalDto(
    string? WorkflowInstanceId,
    string? DecidedBy,
    DateTimeOffset? DecidedAt);

/// <summary>Onboarding case DTO — contract OnboardingCase.</summary>
public sealed record OnboardingCaseDto(
    string SupplierId,
    OnboardingStatus OnboardingStatus,
    KycOutcome KycOutcome,
    SanctionsOutcome SanctionsOutcome,
    IReadOnlyList<OnboardingDocumentDto> Documents,
    OnboardingApprovalDto? Approval,
    string ContractVersion);

// ══ REQUEST / INPUT records (controller body binding — contract SupplierUpsert / OnboardingSubmit) ══

public sealed record SupplierContactInput(
    string Type,
    string? Email,
    string? Phone,
    string? Name = null);

/// <summary>contract SupplierUpsert request body.</summary>
public sealed record SupplierUpsertRequest(
    string Name,
    string? Country,
    string? TaxId,
    List<SupplierContactInput>? Contacts,
    string? SourceSystem,
    string? ExternalRef);

public sealed record KycInput(
    string? LegalName,
    string? RegistrationNo,
    List<string>? BeneficialOwners);

public sealed record OnboardingDocumentInput(
    string Type,
    string EvidenceRef);

/// <summary>contract OnboardingSubmit request body.</summary>
public sealed record OnboardingSubmitRequest(
    KycInput? Kyc,
    List<OnboardingDocumentInput>? Documents,
    bool? SubmitForApproval);

/// <summary>validateSuppliers request body.</summary>
public sealed record ValidateSuppliersRequest(
    List<string> SupplierIds);

// ══ MOD-0140 DL-3 — eligibility (1.3.0, §3.1) and relationship (1.4.0, K-1) surfaces ══

/// <summary>
/// <c>SupplierEligibility</c> (§3.1): every value comes from the CALLER company's relationship only. Null-able fields are
/// always written (value null); <c>supplierId</c> is always the canonical code.
/// </summary>
public sealed record SupplierEligibilityDto(
    string SupplierId,
    Guid? SupplierMasterId,
    bool RelationshipExists,
    SupplierStatus? Status,
    OnboardingStatus? OnboardingStatus,
    bool? PurchasingEnabled,
    bool EligibleForTransactions,
    string ContractVersion);

/// <summary><c>listEligibleSuppliers</c> envelope — the <c>listSuppliers</c> fields.</summary>
public sealed record EligibleSupplierListDto(
    IReadOnlyList<SupplierEligibilityDto> Items,
    string? NextCursor,
    string ContractVersion);

/// <summary>1.4.0 <c>SupplierRelationship</c> — the caller company's relationship (only its own).</summary>
public sealed record SupplierRelationshipDto(
    string SupplierId,
    Guid SupplierMasterId,
    Guid RelationshipId,
    SupplierStatus Status,
    OnboardingStatus OnboardingStatus,
    string? PaymentTermsCode,
    bool PurchasingEnabled,
    DateTimeOffset? PurchasingSince,
    string? SourceSystem,
    string? ExternalRef,
    IReadOnlyList<string> LegacySupplierCodes,
    int Version,
    string ContractVersion);

/// <summary>1.4.0 <c>RelationshipStatusChange</c> request body.</summary>
public sealed record RelationshipStatusChangeRequest(SupplierStatus Status);

/// <summary>1.4.0 <c>RelationshipPaymentTerms</c> request body (a code — no bank data, K-8).</summary>
public sealed record RelationshipPaymentTermsRequest(string? PaymentTermsCode);

/// <summary>The 409 <c>DUPLICATE_SUPPLIER</c> details: the existing supplier's code ("link it to this company", K-6).</summary>
public sealed record SupplierDuplicateDetails(string SupplierId);
