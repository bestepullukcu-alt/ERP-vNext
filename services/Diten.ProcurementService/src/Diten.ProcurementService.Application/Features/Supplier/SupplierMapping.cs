using SupplierEntity = Diten.ProcurementService.Domain.Entities.Supplier;

namespace Diten.ProcurementService.Application.Features.Supplier;

/// <summary>Entity → DTO eşlemeleri (command + query handler'ları paylaşır). Tek kaynak; şekil sapması önlenir.</summary>
internal static class SupplierMapping
{
    public static SupplierListItemDto ToListItem(SupplierEntity e) => new(
        e.Id,
        e.SupplierId,
        e.Name,
        e.Status,
        e.Country,
        e.TaxId,
        e.Contacts.Select(c => new SupplierContactDto(c.Type, c.Email, c.Phone, c.Name)).ToList(),
        e.OnboardingStatus,
        SupplierContract.Version);

    public static SupplierDetailDto ToDetail(SupplierEntity e) => new(
        e.Id,
        e.SupplierId,
        e.Name,
        e.Status,
        e.Country,
        e.TaxId,
        e.Contacts.Select(c => new SupplierContactDto(c.Type, c.Email, c.Phone, c.Name)).ToList(),
        e.OnboardingStatus,
        e.KycOutcome,
        e.SanctionsOutcome,
        e.SourceSystem,
        e.ExternalRef,
        e.Version,
        SupplierContract.Version);

    public static OnboardingCaseDto ToOnboardingCase(SupplierEntity e) => new(
        e.SupplierId,
        e.OnboardingStatus,
        e.KycOutcome,
        e.SanctionsOutcome,
        e.Documents.Select(d => new OnboardingDocumentDto(d.Type, d.EvidenceRef)).ToList(),
        e.Approval is null
            ? null
            : new OnboardingApprovalDto(e.Approval.WorkflowInstanceId, e.Approval.DecidedBy, e.Approval.DecidedAt),
        SupplierContract.Version);
}
