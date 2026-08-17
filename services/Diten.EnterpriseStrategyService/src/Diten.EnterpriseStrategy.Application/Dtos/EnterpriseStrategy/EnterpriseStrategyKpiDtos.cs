namespace Diten.Application.Dtos.EnterpriseStrategy;

public sealed class KpiDefinitionDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string? BackupOwner { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public string ThresholdModel { get; set; } = string.Empty;
    public string ReportingFrequency { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string ScopeMode { get; set; } = "Enterprise";
    public string? CompanyId { get; set; }
    public string SourceType { get; set; } = "Derived";
    public decimal? BaselineValue { get; set; }
    public decimal? TargetValue { get; set; }
    public string? DecisionReference { get; set; }
    public string? EvidenceReference { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string? SourceKpiTemplateId { get; set; }
    public string? SourceKpiTemplateCode { get; set; }
    public string? SourceKpiTemplateVersion { get; set; }
    public bool CreatedFromLibrary { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class KpiUsageDto
{
    public string KpiId { get; set; } = string.Empty;
    public IReadOnlyList<string> GoalIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ObjectiveIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> InitiativeIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ProjectIds { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> ScorecardIds { get; set; } = Array.Empty<string>();
}

public sealed class KpiOwnershipRowDto
{
    public string KpiId { get; set; } = string.Empty;
    public string KpiName { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string? BackupOwner { get; set; }
    public string ReportingFrequency { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public string CompanyScope { get; set; } = string.Empty;
    public int UsedByCount { get; set; }
    public string Status { get; set; } = "Active";
}

public sealed class ScorecardKpiRowDto
{
    public string KpiId { get; set; } = string.Empty;
    public string KpiName { get; set; } = string.Empty;
    public string GoalId { get; set; } = string.Empty;
    public string ObjectiveId { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;
    public string TimePeriod { get; set; } = string.Empty;
    public decimal? CurrentValue { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? Variance { get; set; }
    public string Trend { get; set; } = "Stable";
    public string Status { get; set; } = "On Track";
    public string? SourceKpiTemplateCode { get; set; }
    public string? SourceKpiTemplateVersion { get; set; }
    public bool CreatedFromLibrary { get; set; }
}

public sealed class ScorecardSnapshotDto
{
    public int TotalKpis { get; set; }
    public int OnTrackCount { get; set; }
    public int AtRiskCount { get; set; }
    public int OffTrackCount { get; set; }
    public IReadOnlyList<ScorecardKpiRowDto> Rows { get; set; } = Array.Empty<ScorecardKpiRowDto>();
}
