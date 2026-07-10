using Diten.MdmService.Domain.Enums;

namespace Diten.MdmService.Domain.Entities;

// MOD-0220 FULL — rich Legal Entity (8 sections). EXTENDS the original slim slice (Code/LegalName/
// DisplayName kept; mdm_legal_entities collection unchanged). OrgUnit.LegalEntityId FK keeps working via
// IsReferenceable + the /lookup-validation endpoint. Evidence/workflow gating is DEFERRED (Faz 1 manual).
public sealed class LegalEntity : EntityBase
{
    // 1 — Identity
    public string Code { get; set; } = string.Empty;
    public string LegalName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string LegalFormCode { get; set; } = string.Empty;
    public string OrganizationRoleCode { get; set; } = string.Empty;

    // 2 — Statutory & Tax
    public string? RegistrationNumber { get; set; }
    public string? TaxId { get; set; }
    // MOD-0220 finish — additive statutory fields. VatNumber is distinct from TaxId/RegistrationNumber
    // (multi-jurisdiction, e.g. EU VAT ID); PlaceOfIncorporation may differ from CountryCode. Free strings /
    // nullable dates — no VIES/format validation (seam only), no new lifecycle logic.
    public string? VatNumber { get; set; }
    public string? PlaceOfIncorporation { get; set; }
    public DateTimeOffset? IncorporationDate { get; set; }
    public DateTimeOffset? DissolutionDate { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public LegalEntityStatutoryStatus StatutoryStatus { get; set; } = LegalEntityStatutoryStatus.Registered;

    // 3 — Structure
    public Guid? ParentLegalEntityId { get; set; }
    public decimal? OwnershipPercent { get; set; }
    public string? ControlTypeCode { get; set; }

    // 4 — Finance
    public string? FiscalYearVariant { get; set; }
    public string? AccountingStandardCode { get; set; }
    public string? TaxRegimeCode { get; set; }
    public string BaseCurrencyCode { get; set; } = string.Empty;

    // 5 — Addresses & Contacts
    public string RegisteredAddressJson { get; set; } = string.Empty;
    public string? CorrespondenceAddressJson { get; set; }
    public string? OfficialEmail { get; set; }
    public string? OfficialPhone { get; set; }
    public string? Website { get; set; }

    // 6 — Governance
    public LegalEntityOperationalStatus OperationalStatus { get; set; } = LegalEntityOperationalStatus.Draft;
    public LegalEntityApprovalStatus ApprovalStatus { get; set; } = LegalEntityApprovalStatus.Draft;
    public DateTimeOffset? ReviewDueUtc { get; set; }
    public string? SourceSystem { get; set; }
    public string? LegacyCode { get; set; }

    // 7 — Evidence
    public LegalEntityEvidenceStatus EvidenceStatus { get; set; } = LegalEntityEvidenceStatus.NotStarted;
    public int? CompletenessScore { get; set; }

    // Referenceability (OrgUnit FK contract): ACTIVE + not deleted. Was LifecycleStatus==Active.
    public bool IsReferenceable => OperationalStatus == LegalEntityOperationalStatus.Active && !IsDeleted;
}
