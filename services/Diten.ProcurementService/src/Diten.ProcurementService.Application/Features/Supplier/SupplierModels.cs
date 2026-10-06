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

/// <summary>validateSuppliers tek satır sonucu (bilinmeyen → known:false, status:null).</summary>
public sealed record SupplierValidationResultDto(
    string SupplierId,
    bool Known,
    SupplierStatus? Status);

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
