using System.Text.Json.Serialization;
using Diten.Application.Common.Models;

namespace Diten.Application.Dtos.EnterpriseStrategy;

public sealed class StrategyLibraryCatalogItemDto
{
    public string ItemType { get; set; } = string.Empty; // Template | BlueprintPack
    public string TemplateType { get; set; } = string.Empty; // Goal | Objective | Initiative | Project | BlueprintPack
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public int Version { get; set; }
    public string CategoryOrType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string ParentGoalTemplateId { get; set; } = string.Empty;
    public string ParentObjectiveTemplateId { get; set; } = string.Empty;
    public string ParentObjectiveName { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? TimeHorizonStart { get; set; }
    public DateTime? TimeHorizonEnd { get; set; }
    public bool PublishedOnly { get; set; }
    public int UsageCount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class StrategyTemplateVersionDto
{
    public string Id { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "Draft";
    public string ChangeSummary { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public sealed class StrategyBlueprintHierarchyRowDto
{
    public string GoalTemplateId { get; set; } = string.Empty;
    public string ObjectiveTemplateId { get; set; } = string.Empty;
    public string InitiativeTemplateId { get; set; } = string.Empty;
    public string ProjectTemplateId { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public int? PlanningYearStart { get; set; }
    public int? PlanningYearEnd { get; set; }
}

public sealed class StrategyBlueprintDetailDto
{
    public string BlueprintPackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public int Version { get; set; }
    public IReadOnlyList<StrategyBlueprintHierarchyRowDto> HierarchyRows { get; set; } = Array.Empty<StrategyBlueprintHierarchyRowDto>();
    public IReadOnlyList<string> DecisionReferences { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> EvidenceReferences { get; set; } = Array.Empty<string>();
}

public sealed class StrategyTemplateDetailDto
{
    public string TemplateType { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public int Version { get; set; }
    public string EntityScope { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Dictionary<string, string> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<Dictionary<string, string>> Metrics { get; set; } = Array.Empty<Dictionary<string, string>>();
    /// <summary>Populated for Goal templates: structured metrics including yearly targets.</summary>
    public IReadOnlyList<GoalTemplateMetricSnapshotDto> GoalMetrics { get; set; } = Array.Empty<GoalTemplateMetricSnapshotDto>();
    /// <summary>Populated for Goal templates: strategic yearly budget envelope.</summary>
    public IReadOnlyList<GoalYearlyBudgetEnvelopeDto> GoalYearlyBudgets { get; set; } = Array.Empty<GoalYearlyBudgetEnvelopeDto>();
    /// <summary>Populated for Goal templates: structured prefill alongside <see cref="GoalMetrics"/> / <see cref="GoalYearlyBudgets"/>.</summary>
    public GoalTemplatePrefillDto? GoalPrefill { get; set; }
    /// <summary>Populated for Objective templates: typed create-from-template prefill and advisory metadata.</summary>
    public ObjectiveTemplatePrefillDto? ObjectivePrefill { get; set; }
    /// <summary>Populated for Initiative templates: typed create-from-template prefill and advisory metadata.</summary>
    public InitiativeTemplatePrefillDto? InitiativePrefill { get; set; }
}

public sealed class GoalTemplateMetricSnapshotDto
{
    /// <summary>Stable id of the metric row within the Strategy Library (template metric document id).</summary>
    public string TemplateMetricId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public decimal BaselineValue { get; set; }
    public decimal TargetValue { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public bool CascadeMetric { get; set; } = true;
    public string MetricOrigin { get; set; } = "Local";
    public string MetricRole { get; set; } = "Strategic";
    public string RestrictionMode { get; set; } = "GoalGovernedStructure";
    public bool RollupEligible { get; set; } = true;
    [JsonPropertyName("yearlyValues")]
    public IReadOnlyList<GoalMetricYearValueDto> YearlyValues { get; set; } = Array.Empty<GoalMetricYearValueDto>();
}

public sealed class StrategyLibraryImportPayloadDto
{
    public string BatchName { get; set; } = string.Empty;
    public Dictionary<string, List<Dictionary<string, string>>> Sheets { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class StrategyLibraryImportIssueDto
{
    public string Severity { get; set; } = "Info"; // Fatal | Warning | Info
    public string SheetName { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class StrategyLibraryImportBatchDto
{
    public string BatchId { get; set; } = string.Empty;
    public string BatchName { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public string ImportedBy { get; set; } = string.Empty;
    public int TotalRowsRead { get; set; }
    public int UniqueTemplatesCreated { get; set; }
    public int DuplicateRowsCollapsed { get; set; }
    public int InvalidParentReferences { get; set; }
    public int MissingIds { get; set; }
    public int RepeatedMetricsDetected { get; set; }
    public int OrphanRows { get; set; }
    public int VersionConflicts { get; set; }
    public IReadOnlyList<StrategyLibraryImportIssueDto> Issues { get; set; } = Array.Empty<StrategyLibraryImportIssueDto>();
}

public sealed class StrategyTemplateInstantiateRequestDto
{
    public string TemplateType { get; set; } = string.Empty; // Goal | Objective | Initiative | Project
    public string TemplateId { get; set; } = string.Empty;
    public string? BlueprintPackId { get; set; }
    /// <summary>When true, instantiate child templates in the library (e.g. goal → objectives → initiatives, objective → initiatives, initiative → projects).</summary>
    public bool FullChain { get; set; } = true;
    public bool AllowDuplicates { get; set; }
    public Dictionary<string, string> DefaultOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class StrategyBlueprintInstantiateRequestDto
{
    public string BlueprintPackId { get; set; } = string.Empty;
    public bool FullChain { get; set; } = true;
    public IReadOnlyList<string> SelectedPackItemIds { get; set; } = Array.Empty<string>();
    public bool AllowDuplicates { get; set; }
    public Dictionary<string, string> DefaultOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class InstantiatedLiveRecordDto
{
    public string RuntimeObjectType { get; set; } = string.Empty;
    public string RuntimeObjectId { get; set; } = string.Empty;
    public string SourceTemplateType { get; set; } = string.Empty;
    public string SourceTemplateId { get; set; } = string.Empty;
    public int SourceTemplateVersion { get; set; }
}

public sealed class StrategyInstantiationResultDto
{
    public string InstantiationBatchId { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public int CreatedCount { get; set; }
    public int DuplicateWarnings { get; set; }
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public IReadOnlyList<InstantiatedLiveRecordDto> CreatedRecords { get; set; } = Array.Empty<InstantiatedLiveRecordDto>();
}

public sealed class StrategyLibraryUsageSummaryDto
{
    public int TotalTemplates { get; set; }
    public int PublishedTemplates { get; set; }
    public int RetiredTemplates { get; set; }
    public int TotalBlueprintPacks { get; set; }
    public int PublishedBlueprintPacks { get; set; }
    public int TotalInstantiations { get; set; }
    public string LastInstantiatedBy { get; set; } = string.Empty;
    public DateTime? LastInstantiatedAt { get; set; }
}

public sealed class StrategyLibraryUsageItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public string LastInstantiatedBy { get; set; } = string.Empty;
    public DateTime? LastInstantiatedAt { get; set; }
}

public sealed class StrategyLibraryCatalogRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; } = "desc";
    public Dictionary<string, string> Filters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string TemplateType { get; set; } = string.Empty;
    public string CategoryOrType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ParentGoalTemplateId { get; set; } = string.Empty;
    public bool PublishedOnly { get; set; }
}

/// <summary>Projects Library datatable row (library templates, not runtime projects).</summary>
public sealed class ProjectLibraryRowDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OwnerPm { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string DeliveryType { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string RiskRating { get; set; } = string.Empty;
    public string ReadinessStatus { get; set; } = string.Empty;
    public int Version { get; set; }
    public int MetricCount { get; set; }
    public string LifecycleStatus { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ProjectLibraryCatalogRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; } = "desc";
    public string? ProjectStatus { get; set; }
    public string? Phase { get; set; }
    public string? OwnerPm { get; set; }
    public string? Sponsor { get; set; }
    public string? DeliveryType { get; set; }
    public string? EntityScope { get; set; }
    public string? RiskRating { get; set; }
    public string? ReadinessStatus { get; set; }
    public int? Version { get; set; }
}

public sealed class ProjectTemplateMetricDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectTemplateId { get; set; } = string.Empty;
    public string SuccessMetric { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public decimal BaselineValue { get; set; }
    public decimal TargetValue { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
