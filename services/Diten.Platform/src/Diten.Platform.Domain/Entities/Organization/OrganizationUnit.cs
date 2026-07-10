using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Organization;

public sealed class OrganizationUnit : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required Guid LegalEntityId { get; set; }
    public Guid? ParentOrganizationUnitId { get; set; }
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
