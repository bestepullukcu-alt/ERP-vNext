using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Organization;

public sealed class OrganizationUnit : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required Guid LegalEntityId { get; set; }
    public Guid? ParentOrganizationUnitId { get; set; }

    /*
     * MOD-0288-FU02 — the SECOND reporting line. `ParentOrganizationUnitId` above is the FUNCTIONAL line
     * (work allocation); this one is the ADMINISTRATIVE line.
     *
     * ⚠ NULLABLE, AND NULL MEANS UNDEFINED — NOT "the functional one". There is deliberately no fallback
     * (§8 decision 2). A fallback collapses two lines into one and destroys the distinction the feature
     * exists for, and it would make "genuinely two lines" indistinguishable from "defaulted". A surface with
     * no administrative parent shows nothing, never a substitute.
     *
     * ⚠ IT MAY EQUAL `ParentOrganizationUnitId` WHEN AN ADMINISTRATOR TYPES IT (§12). One unit genuinely
     * holding both responsibilities for another is a real structure. What is forbidden is the SYSTEM writing
     * that value: no default, no migration, no import copies the functional parent into this slot.
     *
     * ⚠ IT DRIVES NO APPROVAL. Task scope walks Position.ReportsToPositionId and MOD-0023 resolves the
     * approver; neither reads this field, and FU02 changes that not at all (§8 decision 1).
     */
    public Guid? AdministrativeParentOrganizationUnitId { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // MOD-0288 v1 — enterprise fields (additive; defaults keep existing rows valid).
    public OrgUnitType OrgUnitType { get; set; } = OrgUnitType.Department;
    public Guid? ManagerPositionId { get; set; }   // a Position, NOT a free user
    public string? Description { get; set; }
    public OrgUnitStatus Status { get; set; } = OrgUnitStatus.Active;
    public DateTimeOffset? EffectiveFrom { get; set; }  // simple lifecycle dates, NOT temporal versioning
    public DateTimeOffset? EffectiveTo { get; set; }

    // Deferred (field-only seam, no UI): location / cost-center integration.
    public string? LocationCode { get; set; }
    public string? CostCenterCode { get; set; }
}
