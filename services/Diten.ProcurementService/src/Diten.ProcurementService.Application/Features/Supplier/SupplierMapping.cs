using Diten.ProcurementService.Application.Features.Supplier.Store;

namespace Diten.ProcurementService.Application.Features.Supplier;

/// <summary>Snapshot → DTO eşlemeleri (command + query handler'ları paylaşır). Tek kaynak; şekil sapması önlenir.</summary>
internal static class SupplierMapping
{
    public static SupplierListItemDto ToListItem(SupplierSnapshot s) => new(
        s.MasterId,
        s.SupplierId,
        s.Name,
        s.Status,
        s.Country,
        s.TaxId,
        s.Contacts.Select(c => new SupplierContactDto(c.Type, c.Email, c.Phone, c.Name)).ToList(),
        s.OnboardingStatus,
        SupplierContract.Version);

    public static SupplierDetailDto ToDetail(SupplierSnapshot s) => new(
        s.MasterId,
        s.SupplierId,
        s.Name,
        s.Status,
        s.Country,
        s.TaxId,
        s.Contacts.Select(c => new SupplierContactDto(c.Type, c.Email, c.Phone, c.Name)).ToList(),
        s.OnboardingStatus,
        s.KycOutcome,
        s.SanctionsOutcome,
        s.SourceSystem,
        s.ExternalRef,
        s.Version,
        SupplierContract.Version);

    public static OnboardingCaseDto ToOnboardingCase(SupplierSnapshot s) => new(
        s.SupplierId,
        s.OnboardingStatus,
        s.KycOutcome,
        s.SanctionsOutcome,
        s.Documents.Select(d => new OnboardingDocumentDto(d.Type, d.EvidenceRef)).ToList(),
        s.Approval is null
            ? null
            : new OnboardingApprovalDto(s.Approval.WorkflowInstanceId, s.Approval.DecidedBy, s.Approval.DecidedAt),
        SupplierContract.Version);

    /// <summary>§3.1 for a supplier the caller's company IS related to (eligibility endpoint, /eligible).</summary>
    public static SupplierEligibilityDto ToEligibility(SupplierSnapshot s) => new(
        s.SupplierId,
        s.MasterId,
        true,
        s.Status,
        s.OnboardingStatus,
        s.PurchasingEnabled,
        SupplierEligibilityRule.IsEligible(s),
        SupplierContract.Version);

    /// <summary>§3.2: the validate row — the requested value as <c>supplierId</c>, the relationship fields or nulls.</summary>
    public static SupplierValidationResultDto ToValidationRow(string requested, SupplierSnapshot? s) => s is null
        ? new SupplierValidationResultDto(requested, false, null, null, false, null, null, false)
        : new SupplierValidationResultDto(requested, true, s.Status, s.MasterId, true, s.OnboardingStatus, s.PurchasingEnabled, SupplierEligibilityRule.IsEligible(s));

    public static SupplierRelationshipDto ToRelationship(SupplierSnapshot s) => new(
        s.SupplierId,
        s.MasterId,
        s.RelationshipId,
        s.Status,
        s.OnboardingStatus,
        s.PaymentTermsCode,
        s.PurchasingEnabled,
        s.PurchasingSince,
        s.SourceSystem,
        s.ExternalRef,
        s.LegacySupplierCodes,
        s.Version,
        SupplierContract.Version);
}
