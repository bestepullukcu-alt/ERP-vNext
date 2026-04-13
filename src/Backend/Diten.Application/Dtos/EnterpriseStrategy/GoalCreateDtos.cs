using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Diten.Application.Dtos.EnterpriseStrategy;

public sealed class CreateGoalRequestDto
{
    public string GoalTitle { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use goalTitle.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Goal { get => GoalTitle; set => GoalTitle = value; }
    public string Category { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use category.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string CategoryCode { get => Category; set => Category = value; }
    public string GoalTypeId { get => Category; set => Category = value; }
    public string StrategicThemeId { get; set; } = string.Empty;
    public string OwnerRole { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use ownerRole.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string OwnerId { get => OwnerRole; set => OwnerRole = value; }
    public string OwnerPositionId { get => OwnerRole; set => OwnerRole = value; }
    public string? OwnerCompanyId { get; set; }
    public string? OwnerOrgId { get => OwnerCompanyId; set => OwnerCompanyId = value; }
    public string? OwnerPersonId { get; set; }
    public string? CurrentOwnerPersonId { get => OwnerPersonId; set => OwnerPersonId = value; }
    public string Status { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use status.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string StatusCode { get => Status; set => Status = value; }
    public string Priority { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use priority.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string PriorityCode { get => Priority; set => Priority = value; }
    public string GoalStatement { get; set; } = string.Empty;
    public CreateGoalPlanningDto Planning { get; set; } = new();
    public CreateGoalCompanyScopeDto CompanyScope { get; set; } = new();
    public List<CreateGoalMetricDto> Metrics { get; set; } = new();
    public List<CreateGoalYearlyBudgetDto> BudgetEnvelopes { get; set; } = new();
    [Obsolete("Deprecated legacy field. Use budgetEnvelopes.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public List<CreateGoalYearlyBudgetDto> YearlyBudgets { get => BudgetEnvelopes; set => BudgetEnvelopes = value ?? new(); }
    public CreateGoalGovernanceDto Governance { get; set; } = new();
    /// <summary>Blank | Template | BlueprintPack — informational; also set source ids when applicable.</summary>
    public string? CreationModeCode { get; set; }
    public string? SourceTemplateId { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
    public bool SaveAsTemplate { get; set; }
    public GoalTemplateSaveMetadataDto? TemplateSave { get; set; }
}

public sealed class GoalTemplateSaveMetadataDto
{
    public string TemplateName { get; set; } = string.Empty;
    public string? TemplateDescription { get; set; }
    public string? TemplateCategoryOrTags { get; set; }
    public bool PublishReady { get; set; }
}

public sealed class CreateGoalYearlyBudgetDto
{
    public int Year { get; set; }
    public decimal? RevenueTarget { get; set; }
    public decimal? EbitdaTarget { get; set; }
    public decimal? CapexEnvelope { get; set; }
    public decimal? OpexEnvelope { get; set; }
    public decimal? SavingsTarget { get; set; }
    public decimal? FundingPool { get; set; }
    [Obsolete("Deprecated legacy field. Use fundingPool.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public decimal? FundingPoolEnvelope { get => FundingPool; set => FundingPool = value; }
    public string? Commentary { get; set; }
}

public sealed class CreateGoalPlanningDto
{
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    [Obsolete("Deprecated legacy field. Use startDate.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int? StartYear
    {
        get => StartDate?.Year;
        set
        {
            if (value.HasValue)
                StartDate = DateTime.SpecifyKind(new DateTime(value.Value, 1, 1), DateTimeKind.Utc);
        }
    }
    [Obsolete("Deprecated legacy field. Use endDate.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int? EndYear
    {
        get => EndDate?.Year;
        set
        {
            if (value.HasValue)
                EndDate = DateTime.SpecifyKind(new DateTime(value.Value, 12, 31), DateTimeKind.Utc);
        }
    }
    public string? StrategyPeriodId { get; set; }
    public string? RelatedEntityScope { get; set; }
    public string? ChangeLogRef { get; set; }
}

public sealed class CreateGoalCompanyScopeDto
{
    public string ApplicabilityMode { get; set; } = string.Empty;
    public bool AppliesToAllCompanies { get; set; }
    public List<string> ApplicableCompanyIds { get; set; } = new();
    [Obsolete("Derived read-model field only. Do not submit from clients.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? RelatedEntityScopeSummary { get; set; }
    [Obsolete("Deprecated legacy field. Use applicabilityMode.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string ScopeModeCode { get => ApplicabilityMode; set => ApplicabilityMode = value; }
    [Obsolete("Deprecated legacy field. Use appliesToAllCompanies.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool AppliesToAllCompaniesFlag { get => AppliesToAllCompanies; set => AppliesToAllCompanies = value; }
    [Obsolete("Deprecated legacy field. Use applicabilityMode + applicableCompanyIds.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool AppliesToSelectedCompaniesFlag
    {
        get => !AppliesToAllCompanies && (ApplicableCompanyIds?.Count ?? 0) > 0;
        set
        {
            if (!value)
                ApplicableCompanyIds.Clear();
        }
    }
    public string? PrimaryCompanyId { get; set; }
}

public sealed class CreateGoalMetricDto
{
    public string? MetricAssignmentId { get; set; }
    public string? GoalId { get; set; }
    public string MetricDefinitionId { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use metricDefinitionId.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? MetricDefId { get => MetricDefinitionId; set => MetricDefinitionId = value ?? string.Empty; }
    public string MetricName { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use metricType.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string MetricTypeCode { get => MetricType; set => MetricType = value; }
    public decimal? BaselineValue { get; set; }
    public decimal? TargetValue { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use unitOfMeasure.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string UnitOfMeasureCode { get => UnitOfMeasure; set => UnitOfMeasure = value; }
    public string AggregationMethod { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use aggregationMethod.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string AggregationMethodCode { get => AggregationMethod; set => AggregationMethod = value; }
    public string DirectionPolarity { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use directionPolarity.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string PolarityCode { get => DirectionPolarity; set => DirectionPolarity = value; }
    public string ThresholdModel { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use thresholdModel.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string ThresholdModelCode { get => ThresholdModel; set => ThresholdModel = value; }
    public string ReportingFrequency { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use reportingFrequency.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string ReportingFrequencyCode { get => ReportingFrequency; set => ReportingFrequency = value; }
    public bool CascadeMetric { get; set; } = true;
    public string MetricOrigin { get; set; } = "Local";
    public string MetricRole { get; set; } = "Strategic";
    public string RestrictionMode { get; set; } = "GoalGovernedStructure";
    public bool RollupEligible { get; set; } = true;
    [JsonPropertyName("yearlyValues")]
    public List<CreateGoalMetricYearDto> YearlyValues { get; set; } = new();
    [JsonPropertyName("yearlyTargets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<CreateGoalMetricYearDto>? LegacyYearlyTargets { get; set; }
    public int SortOrder { get; set; }
}

public sealed class CreateGoalGovernanceDto
{
    public string? DecisionReference { get; set; }
    public string? EvidenceLink { get; set; }
}

public sealed class CreateGoalResponseDto
{
    public string GoalId { get; set; } = string.Empty;
    public string GoalTitle { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use goalTitle.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Goal { get => GoalTitle; set => GoalTitle = value; }
    public string Category { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use category.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string CategoryCode { get => Category; set => Category = value; }
    public string GoalTypeId { get => Category; set => Category = value; }
    public string StrategicThemeId { get; set; } = string.Empty;
    public string OwnerRole { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use ownerRole.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string OwnerId { get => OwnerRole; set => OwnerRole = value; }
    public string OwnerPositionId { get => OwnerRole; set => OwnerRole = value; }
    public string OwnerCompanyId { get; set; } = string.Empty;
    public string OwnerOrgId { get => OwnerCompanyId; set => OwnerCompanyId = value; }
    public string? OwnerPersonId { get; set; }
    public string? CurrentOwnerPersonId { get => OwnerPersonId; set => OwnerPersonId = value; }
    public string OwnerDisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use status.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string StatusCode { get => Status; set => Status = value; }
    public string Priority { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use priority.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string PriorityCode { get => Priority; set => Priority = value; }
    public string GoalStatement { get; set; } = string.Empty;
    public CreateGoalPlanningDto Planning { get; set; } = new();
    public CreateGoalCompanyScopeDto CompanyScope { get; set; } = new();
    public List<CreateGoalResponseMetricDto> Metrics { get; set; } = new();
    public List<CreateGoalYearlyBudgetDto> BudgetEnvelopes { get; set; } = new();
    [Obsolete("Deprecated legacy field. Use budgetEnvelopes.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public List<CreateGoalYearlyBudgetDto> YearlyBudgets { get => BudgetEnvelopes; set => BudgetEnvelopes = value ?? new(); }
    public CreateGoalGovernanceDto Governance { get; set; } = new();
    public int Version { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    /// <summary>When <see cref="CreateGoalRequestDto.SaveAsTemplate"/> succeeded, id of the new Goal template in the Strategy Library.</summary>
    public string? SavedTemplateId { get; set; }
    /// <summary>Echo of creation mode / source references stored on the goal.</summary>
    public string? CreationModeCode { get; set; }
    public string? SourceTemplateId { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
}

public sealed class CreateGoalResponseMetricDto
{
    public string Id { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use id.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string MetricId { get => Id; set => Id = value; }
    public string MetricAssignmentId { get; set; } = string.Empty;
    public string GoalId { get; set; } = string.Empty;
    public string MetricDefinitionId { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use metricDefinitionId.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string MetricDefId { get => MetricDefinitionId; set => MetricDefinitionId = value; }
    public string MetricName { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use metricType.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string MetricTypeCode { get => MetricType; set => MetricType = value; }
    public decimal? BaselineValue { get; set; }
    public decimal? TargetValue { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use unitOfMeasure.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string UnitOfMeasureCode { get => UnitOfMeasure; set => UnitOfMeasure = value; }
    public string AggregationMethod { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use aggregationMethod.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string AggregationMethodCode { get => AggregationMethod; set => AggregationMethod = value; }
    public string DirectionPolarity { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use directionPolarity.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string PolarityCode { get => DirectionPolarity; set => DirectionPolarity = value; }
    public string ThresholdModel { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use thresholdModel.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string ThresholdModelCode { get => ThresholdModel; set => ThresholdModel = value; }
    public string ReportingFrequency { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use reportingFrequency.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string ReportingFrequencyCode { get => ReportingFrequency; set => ReportingFrequency = value; }
    public bool CascadeMetric { get; set; }
    public string MetricOrigin { get; set; } = string.Empty;
    public string MetricRole { get; set; } = string.Empty;
    public string RestrictionMode { get; set; } = string.Empty;
    public bool RollupEligible { get; set; }
    [JsonPropertyName("yearlyValues")]
    public List<CreateGoalMetricYearDto> YearlyValues { get; set; } = new();
    public int SortOrder { get; set; }
}

public sealed class CreateGoalMetricYearDto
{
    public int Year { get; set; }
    [Obsolete("Deprecated legacy field. Baseline belongs to KPI definition/governance, not yearly target rows.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public decimal? BaselineValue { get; set; }
    public decimal? TargetValue { get; set; }
    [Obsolete("Deprecated legacy field. Actual values belong to tracking APIs, not goal contract.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public decimal? ActualValue { get; set; }
    [Obsolete("Deprecated legacy field. Forecast values belong to tracking APIs, not goal contract.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public decimal? ForecastValue { get; set; }
    public decimal? ThresholdMin { get; set; }
    public decimal? ThresholdMax { get; set; }
    public string? Commentary { get; set; }
    [Obsolete("Deprecated legacy field. Use commentary.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? ThresholdCommentary { get; set; }
}