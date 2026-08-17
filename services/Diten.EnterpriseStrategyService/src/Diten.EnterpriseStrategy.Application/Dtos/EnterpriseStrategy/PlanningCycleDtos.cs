namespace Diten.Application.Dtos.EnterpriseStrategy;

public sealed class PlanningCycleDto
{
    public string Id { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PlanningCycleType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string OwnerCompanyId { get; set; } = string.Empty;
    public string? OwnerPositionId { get; set; }
    public string? CurrentOwnerPersonId { get; set; }
    public string OwnerId
    {
        get => !string.IsNullOrWhiteSpace(CurrentOwnerPersonId)
            ? CurrentOwnerPersonId!
            : (OwnerPositionId ?? string.Empty);
        set => CurrentOwnerPersonId = value;
    }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedOn { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime? ArchivedAt { get; set; }
}

public sealed class StrategyPeriodDto
{
    public string Id { get; set; } = string.Empty;
    public string PlanningCycleId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerEmployeeId { get; set; } = string.Empty;
    public string OwnerCompanyId
    {
        get => CompanyId;
        set => CompanyId = value ?? string.Empty;
    }
    public string? OwnerPositionId { get; set; }
    public string CurrentOwnerPersonId
    {
        get => OwnerEmployeeId;
        set => OwnerEmployeeId = value ?? string.Empty;
    }
    public string CompanyId { get; set; } = string.Empty;
    public string? BusinessUnitId { get; set; }
    public string? RegionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string ReviewCadence { get; set; } = string.Empty;
    public string? ScenarioType { get; set; }
    public string? VersionLabel { get; set; }
    public string Status { get; set; } = "Draft";
    public bool IsDefaultForScope { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedOn { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime? ArchivedAt { get; set; }
}

public sealed class StrategyPeriodUsageSummaryDto
{
    public string StrategyPeriodId { get; set; } = string.Empty;
    public int GoalCount { get; set; }
    public int ObjectiveCount { get; set; }
    public bool IsInUse => GoalCount > 0 || ObjectiveCount > 0;
    public List<StrategyPeriodUsageGoalRef> Goals { get; set; } = new();
}

public sealed class StrategyPeriodUsageGoalRef
{
    public string GoalId { get; set; } = string.Empty;
    public string GoalTitle { get; set; } = string.Empty;
    public int ObjectiveCount { get; set; }
}
