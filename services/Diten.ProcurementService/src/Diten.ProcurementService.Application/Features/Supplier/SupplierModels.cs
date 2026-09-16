using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Supplier;

/// <summary>Embedded contact DTO (read surface).</summary>
public sealed record SupplierContactDto(
    string Type,
    string? Email,
    string? Phone,
    string? Name);

/// <summary>List item — SUPPLIER contract consumer slice (listSuppliers) şekliyle uyumlu minimal görünüm.</summary>
public sealed record SupplierListItemDto(
    Guid Id,
    string SupplierId,
    string Name,
    SupplierStatus Status,
    string? Country,
    string? TaxId,
    OnboardingStatus OnboardingStatus);

/// <summary>Detail — getSupplier consumer slice ile uyumlu.</summary>
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
    string? ExternalRef);
