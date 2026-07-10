using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.Organization;

public sealed class Position : TenantScopedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required Guid OrganizationUnitId { get; set; }
    public Guid? ReportsToPositionId { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // MOD-0288 v1 — enterprise fields (additive). JobTitle is the HR job label (seam to a future Job entity),
    // NOT the RBAC role. Occupancy/IsVacant is DERIVED from active assignments — never stored.
    public string? JobTitle { get; set; }
    public PositionType PositionType { get; set; } = PositionType.Permanent;
    public decimal? Fte { get; set; }
    public PositionStatus Status { get; set; } = PositionStatus.Draft;
    public DateTimeOffset? EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    // Deferred (field-only seam, no UI).
    public string? LocationCode { get; set; }
    public string? CostCenterCode { get; set; }
    public string? GradeCode { get; set; }
}
