namespace Diten.Application.Dtos.EnterpriseStrategy;

public sealed class PlanningCycleListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlanningCycleType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OwnerCompanyId { get; set; } = string.Empty;
    public string? OwnerPositionId { get; set; }
    public string? CurrentOwnerPersonId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
}

public sealed class PlanningCycleDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlanningCycleType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OwnerCompanyId { get; set; } = string.Empty;
    public string? OwnerPositionId { get; set; }
    public string? CurrentOwnerPersonId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedOn { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime? ArchivedAt { get; set; }
}

public sealed class CreatePlanningCycleRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlanningCycleType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? OwnerCompanyId { get; set; }
    public string? OwnerPositionId { get; set; }
    public string? CurrentOwnerPersonId { get; set; }
    public string? OwnerId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
}

public sealed class UpdatePlanningCycleRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlanningCycleType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? OwnerCompanyId { get; set; }
    public string? OwnerPositionId { get; set; }
    public string? CurrentOwnerPersonId { get; set; }
    public string? OwnerId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
}

public sealed class StrategyPeriodListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string PlanningCycleId { get; set; } = string.Empty;
    public string PlanningCycleCode { get; set; } = string.Empty;
    public string PlanningCycleName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerCompanyId { get; set; } = string.Empty;
    public string OwnerEmployeeId { get; set; } = string.Empty;
    public string? OwnerPositionId { get; set; }
    public string CurrentOwnerPersonId { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string? BusinessUnitId { get; set; }
    public string? RegionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ReviewCadence { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsDefaultForScope { get; set; }
}

public sealed class StrategyPeriodDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string PlanningCycleId { get; set; } = string.Empty;
    public string PlanningCycleCode { get; set; } = string.Empty;
    public string PlanningCycleName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerCompanyId { get; set; } = string.Empty;
    public string OwnerEmployeeId { get; set; } = string.Empty;
    public string? OwnerPositionId { get; set; }
    public string CurrentOwnerPersonId { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string? BusinessUnitId { get; set; }
    public string? RegionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ReviewCadence { get; set; } = string.Empty;
    public string? ScenarioType { get; set; }
    public string? VersionLabel { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsDefaultForScope { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedOn { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime? ArchivedAt { get; set; }
}

public sealed class CreateStrategyPeriodRequest
{
    public string PlanningCycleId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerCompanyId { get; set; } = string.Empty;
    public string OwnerEmployeeId { get; set; } = string.Empty;
    public string? OwnerPositionId { get; set; }
    public string CurrentOwnerPersonId { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string? BusinessUnitId { get; set; }
    public string? RegionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ReviewCadence { get; set; } = string.Empty;
    public string? ScenarioType { get; set; }
    public string? VersionLabel { get; set; }
    public string? Status { get; set; }
    public bool IsDefaultForScope { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateStrategyPeriodRequest
{
    public string PlanningCycleId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerCompanyId { get; set; } = string.Empty;
    public string OwnerEmployeeId { get; set; } = string.Empty;
    public string? OwnerPositionId { get; set; }
    public string CurrentOwnerPersonId { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string? BusinessUnitId { get; set; }
    public string? RegionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ReviewCadence { get; set; } = string.Empty;
    public string? ScenarioType { get; set; }
    public string? VersionLabel { get; set; }
    public string? Status { get; set; }
    public bool IsDefaultForScope { get; set; }
    public string? Notes { get; set; }
}
