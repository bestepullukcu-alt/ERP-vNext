using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Diten.Application.Dtos.EnterpriseStrategy;

public sealed class CompanyReferenceDto
{
    public string CompanyId { get; set; } = string.Empty;
    public string CompanyCode { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? Region { get; set; }
    public string? BusinessUnit { get; set; }
    public string? Notes { get; set; }
}

public class StrategicGoalMetricYearlyTargetDto
{
    public string GoalMetricId { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? ThresholdMin { get; set; }
    public decimal? ThresholdMax { get; set; }
    public string? Commentary { get; set; }

    // Legacy compatibility fields (deprecated).
    [Obsolete("Deprecated legacy field. Use targetValue in yearlyValues rows.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public decimal? BaselineValue { get; set; }
    [Obsolete("Deprecated legacy field. Use goal KPI actual tracking APIs instead of goal yearly target payload.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public decimal? ActualValue { get; set; }
    [Obsolete("Deprecated legacy field. Use goal KPI forecast tracking APIs instead of goal yearly target payload.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public decimal? ForecastValue { get; set; }
    [Obsolete("Deprecated legacy field. Use commentary.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? ThresholdCommentary { get => Commentary; set => Commentary = value; }
}

public sealed class GoalMetricYearValueDto : StrategicGoalMetricYearlyTargetDto
{
}

public class StrategicGoalMetricDto
{
    public string Id { get; set; } = string.Empty;
    public string GoalId { get; set; } = string.Empty;
    public string MetricDefinitionId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public string DirectionPolarity { get; set; } = string.Empty;
    public string ThresholdModel { get; set; } = string.Empty;
    public string ReportingFrequency { get; set; } = string.Empty;
    public decimal BaselineValue { get; set; }
    public decimal TargetValue { get; set; }
    public bool CascadeMetric { get; set; } = true;
    public string MetricOrigin { get; set; } = "Local";
    public string MetricRole { get; set; } = "Strategic";
    public string RestrictionMode { get; set; } = "GoalGovernedStructure";
    public bool RollupEligible { get; set; } = true;
    public int SortOrder { get; set; }
    public string MetricBindingStatus { get; set; } = "Unbound";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Canonical API shape
    [JsonPropertyName("yearlyValues")]
    public List<GoalMetricYearValueDto> YearlyValues { get; set; } = new();

    [JsonPropertyName("yearlyTargets")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<GoalMetricYearValueDto>? LegacyYearlyTargets { get; set; }

    // Legacy aliases
    [Obsolete("Deprecated legacy field. Use id.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string MetricAssignmentId { get; set; } = string.Empty;
    [Obsolete("Deprecated legacy field. Use metricDefinitionId.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string MetricDefId { get => MetricDefinitionId; set => MetricDefinitionId = value; }
    [Obsolete("Deprecated legacy field. Use directionPolarity.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string PolarityCode { get => DirectionPolarity; set => DirectionPolarity = value; }
    [Obsolete("Deprecated legacy field. Use thresholdModel.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string ThresholdModelCode { get => ThresholdModel; set => ThresholdModel = value; }
    [Obsolete("Deprecated legacy field. Use reportingFrequency.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string ReportingFrequencyCode { get => ReportingFrequency; set => ReportingFrequency = value; }
    [JsonIgnore]
    public List<GoalMetricYearValueDto> YearlyTargets { get => YearlyValues; set => YearlyValues = value ?? new(); }
}

public sealed class GoalMetricDto : StrategicGoalMetricDto
{
}

public class StrategicGoalBudgetEnvelopeDto
{
    public string GoalId { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal? RevenueTarget { get; set; }
    public decimal? EbitdaTarget { get; set; }
    public decimal? CapexEnvelope { get; set; }
    public decimal? OpexEnvelope { get; set; }
    public decimal? SavingsTarget { get; set; }
    public decimal? FundingPool { get; set; }
    public string? Commentary { get; set; }

    // Legacy alias (deprecated).
    [Obsolete("Deprecated legacy field. Use fundingPool.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public decimal? FundingPoolEnvelope { get => FundingPool; set => FundingPool = value; }
}

public sealed class GoalYearlyBudgetEnvelopeDto : StrategicGoalBudgetEnvelopeDto
{
}

/// <summary>Goal create/update and read model. <see cref="Version"/> is server-assigned on save.</summary>
public class StrategicGoalDto
{
    public string GoalId { get; set; } = string.Empty;
    public string GoalTitle { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string StrategicThemeId { get; set; } = string.Empty;
    public string GoalStatement { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string Priority { get; set; } = "Medium";
    public string? StrategyPeriodId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string OwnerRole { get; set; } = string.Empty;
    public string OwnerCompanyId { get; set; } = string.Empty;
    public string? OwnerPersonId { get; set; }
    public string RelatedEntityScope { get; set; } = string.Empty;
    public string ApplicabilityMode { get; set; } = "Enterprise";
    public bool AppliesToAllCompanies { get; set; }
    public List<string> ApplicableCompanyIds { get; set; } = new();
    public string? ChangeLogRef { get; set; }
    public string? DecisionReference { get; set; }
    public string? EvidenceLink { get; set; }
    public int Version { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? SourceTemplateType { get; set; }
    public string? SourceTemplateId { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
    public string? InstantiationBatchId { get; set; }
    public bool CreatedFromLibrary { get; set; }
    public List<GoalMetricDto> Metrics { get; set; } = new();
    public List<GoalYearlyBudgetEnvelopeDto> BudgetEnvelopes { get; set; } = new();

    // Legacy compatibility aliases (deprecated).
    [JsonPropertyName("goal_id")]
    [Obsolete("Deprecated legacy field. Use goalId.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Id { get => GoalId; set => GoalId = value; }
    [JsonPropertyName("goal_name")]
    [Obsolete("Deprecated legacy field. Use goalTitle.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Name { get => GoalTitle; set => GoalTitle = value; }
    public string GoalTypeId { get => Category; set => Category = value; }
    public string StrategicTheme { get => StrategicThemeId; set => StrategicThemeId = value; }
    [Obsolete("Deprecated legacy field. Use goalTitle.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string GoalName { get => GoalTitle; set => GoalTitle = value; }
    [Obsolete("Deprecated legacy field. Use goalStatement.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Statement { get => GoalStatement; set => GoalStatement = value; }
    [Obsolete("Deprecated legacy field. Use ownerRole.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string OwnerId { get => OwnerRole; set => OwnerRole = value; }
    [Obsolete("Deprecated legacy field. Use ownerRole.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string Owner { get => OwnerRole; set => OwnerRole = value; }
    public string OwnerPositionId { get => OwnerRole; set => OwnerRole = value; }
    public string? OwnerOrgId { get => OwnerCompanyId; set => OwnerCompanyId = value ?? string.Empty; }
    public string? CurrentOwnerPersonId { get => OwnerPersonId; set => OwnerPersonId = value; }
    [Obsolete("Deprecated legacy field. Use ownerPersonId.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? OwnerDisplayName { get => OwnerPersonId; set => OwnerPersonId = value; }
    [Obsolete("Deprecated legacy field. Use startDate.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public DateTime? PlanningHorizonStart { get => StartDate; set => StartDate = value; }
    [Obsolete("Deprecated legacy field. Use endDate.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public DateTime? PlanningHorizonEnd { get => EndDate; set => EndDate = value; }
    [Obsolete("Deprecated legacy field. Use startDate.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int? StartYear
    {
        get => StartDate?.Year;
        set
        {
            if (value.HasValue)
                StartDate = new DateTime(value.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
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
                EndDate = new DateTime(value.Value, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        }
    }
    [Obsolete("Deprecated legacy field. Use relatedEntityScope.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string EntityScope { get => RelatedEntityScope; set => RelatedEntityScope = value; }
    [Obsolete("Deprecated legacy field. Use evidenceLink.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? EvidenceReference { get => EvidenceLink; set => EvidenceLink = value; }
    [Obsolete("Deprecated legacy field. Use applicabilityMode.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string ScopeMode { get => ApplicabilityMode; set => ApplicabilityMode = value; }
    [JsonPropertyName("enterprise_scope_mode")]
    [Obsolete("Deprecated legacy field. Use applicabilityMode.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string EnterpriseScopeMode { get => ApplicabilityMode; set => ApplicabilityMode = value; }
    [Obsolete("Deprecated legacy field. Use ownerCompanyId.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? PrimaryCompanyId { get => OwnerCompanyId; set => OwnerCompanyId = value ?? string.Empty; }
    [Obsolete("Deprecated legacy field. Use applicabilityMode + applicableCompanyIds.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool AppliesToSelectedCompaniesFlag
    {
        get => !AppliesToAllCompanies && ApplicableCompanyIds.Count > 0;
        set
        {
            if (!value) ApplicableCompanyIds.Clear();
        }
    }
    [Obsolete("Deprecated legacy field. Use appliesToAllCompanies.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool AppliesToAllCompaniesFlag { get => AppliesToAllCompanies; set => AppliesToAllCompanies = value; }
    [Obsolete("Derived compatibility field. Use relatedEntityScope and company applicability fields.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? RelatedEntityScopeSummary
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RelatedEntityScope))
                return RelatedEntityScope;
            if (AppliesToAllCompanies || string.Equals(ApplicabilityMode, "Enterprise", StringComparison.OrdinalIgnoreCase))
                return $"{RelatedEntityScope} | All Companies";
            if ((ApplicableCompanyIds?.Count ?? 0) > 0)
                return $"{RelatedEntityScope} | {ApplicableCompanyIds.Count} companies";
            return RelatedEntityScope;
        }
        set { }
    }
    [Obsolete("Deprecated legacy field. Use budgetEnvelopes.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public List<GoalYearlyBudgetEnvelopeDto> YearlyBudgets { get => BudgetEnvelopes; set => BudgetEnvelopes = value ?? new(); }
    /// <summary>Transient: only used on create; not persisted on the aggregate.</summary>
    public bool SaveAsTemplate { get; set; }
    /// <summary>Transient: only used on create.</summary>
    public GoalTemplateSaveMetadataDto? TemplateSave { get; set; }
    /// <summary>Populated on API response when Save-as-template just wrote a new Goal library template snapshot.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? SavedTemplateId { get; set; }
}

public sealed class GoalDto : StrategicGoalDto
{
}

public sealed class ObjectiveMetricDto
{
    public string Id { get; set; } = string.Empty;
    public string ObjectiveId { get; set; } = string.Empty;
    public string? ParentMetricAssignmentId { get; set; }
    public string MetricDefId { get; set; } = string.Empty;
    public string MetricClass { get; set; } = "Local";
    public string MetricRole { get; set; } = "Contribution";
    public string MetricName { get; set; } = string.Empty;
    public decimal BaselineValue { get; set; }
    public DateTime? BaselineDate { get; set; }
    public decimal TargetValue { get; set; }
    public string TargetPeriod { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public string ThresholdTolerance { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string PolarityCode { get; set; } = string.Empty;
    public string MetricBindingStatus { get; set; } = "Unbound";
    public bool RollupEligibleFlag { get; set; }
    public string ThresholdModelCode { get; set; } = string.Empty;
    public string ReportingFrequencyCode { get; set; } = string.Empty;
    public decimal? ContributionWeight { get; set; }
    public List<ObjectiveMetricYearValueDto> YearlyValues { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? TargetDate { get; set; }
    public string? FiscalPeriodId { get; set; }
    public decimal? ThresholdValue { get; set; }
    public string? Tolerance { get => ThresholdTolerance; set => ThresholdTolerance = value ?? string.Empty; }
    public string Notes { get; set; } = string.Empty;
    public string MetricId { get => MetricName; set => MetricName = value; }
    public string UnitOfMeasureId { get => UnitOfMeasure; set => UnitOfMeasure = value; }
    public string AggregationMethodId { get => AggregationMethod; set => AggregationMethod = value; }
    public string MetricOriginCode { get => MetricClass; set => MetricClass = value; }
    public string MetricRoleCode { get => MetricRole; set => MetricRole = value; }
}

public sealed class ObjectiveMetricYearValueDto
{
    public int Year { get; set; }
    public string PeriodKey { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime? PeriodStart { get; set; }
    public DateTime? PeriodEnd { get; set; }
    public string PeriodGranularity { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? ForecastValue { get; set; }
    public decimal? ThresholdMin { get; set; }
    public decimal? ThresholdMax { get; set; }
    public string? Commentary { get; set; }
}

public sealed class ObjectiveYearlyBudgetDto
{
    public int Year { get; set; }
    public decimal? RequestedBudget { get; set; }
    public decimal? ApprovedBudget { get; set; }
    public decimal? ForecastBudget { get; set; }
    public decimal? ActualBudget { get; set; }
    public decimal? VarianceAmount { get; set; }
    public string? Commentary { get; set; }
}

public sealed class ObjectiveDependencyLinkDto
{
    public string Id { get; set; } = string.Empty;
    public string DependencyTypeId { get; set; } = string.Empty;
    public string DependencyObjectType { get; set; } = string.Empty;
    public string? DependencyReferenceId { get; set; }
    public string? DependencyReferenceText { get; set; }
    public string? DependencyCriticality { get; set; }
}

public sealed class ObjectiveDto
{
    public string Id { get; set; } = string.Empty;
    public string ParentGoalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string StrategicTheme { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string Type { get; set; } = string.Empty;
    public DateTime? TimeHorizonStart { get; set; }
    public DateTime? TimeHorizonEnd { get; set; }
    public string PlanningCycle { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public string ContributionType { get; set; } = "Supports";
    public decimal ContributionWeight { get; set; }
    public string DependencyType { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string? BusinessUnit { get; set; }
    public string? Region { get; set; }
    public string DependencyNotes { get; set; } = string.Empty;
    public string OwnerCompanyId { get; set; } = string.Empty;
    public string? OwnerPositionId { get; set; }
    public string? CurrentOwnerPersonId { get; set; }
    public string Owner { get; set; } = string.Empty;
    public string ExecutiveSponsor { get; set; } = string.Empty;
    public List<string> CoOwnerIds { get; set; } = new();
    public string? ApprovalGroup { get; set; }
    public string? ReviewOwner { get; set; }
    public string ApprovalRouteType { get; set; } = "IndividualApprover";
    public string ApprovalStatus { get; set; } = "Draft";
    public string PrimaryKpiMetric { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string DirectionOfPerformance { get; set; } = string.Empty;
    public string TargetPlanGranularity { get; set; } = "Yearly";
    public string ReportingFrequency { get; set; } = string.Empty;
    public string? ThresholdModel { get; set; }
    public string? EvidenceReference { get; set; }
    public bool InheritCompanyScope { get; set; } = true;
    public string? PrimaryCompanyId { get; set; }
    public List<string> ApplicableCompanyIds { get; set; } = new();
    public List<string> LinkedInitiativeIds { get; set; } = new();
    public List<string> LinkedProjectIds { get; set; } = new();
    public List<string> LinkedRiskIssueIds { get; set; } = new();
    public List<string> LinkedDependencyIds { get; set; } = new();
    public List<ObjectiveDependencyLinkDto> DependencyLinks { get; set; } = new();
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedOn { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? ReviewCadence { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public string? ChangeReason { get; set; }
    public int Version { get; set; }
    public string? DecisionReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? SourceTemplateType { get; set; }
    public string? SourceTemplateId { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
    public string? InstantiationBatchId { get; set; }
    public bool CreatedFromLibrary { get; set; }
    public List<ObjectiveMetricDto> Metrics { get; set; } = new();
    public List<ObjectiveYearlyBudgetDto> YearlyBudgets { get; set; } = new();
    public bool AllowMultipleTargetMetrics { get; set; }
    public bool AllowRowThresholdOverrides { get; set; }

    // Canonical create contract aliases (ID-based payload names)
    public string ObjectiveId { get => Id; set => Id = value; }
    public string ObjectiveName { get => Name; set => Name = value; }
    public string ObjectiveStatement { get => Statement; set => Statement = value; }
    public string StrategicThemeId { get => StrategicTheme; set => StrategicTheme = value; }
    public string ObjectiveTypeId { get => Type; set => Type = value; }
    public string LifecycleState { get => Status; set => Status = value; }
    public string PlanningCycleId { get => PlanningCycle; set => PlanningCycle = value; }
    public string StrategyPeriodId { get => PlanningCycle; set => PlanningCycle = value; }
    public string OwnerId
    {
        get => string.IsNullOrWhiteSpace(CurrentOwnerPersonId) ? Owner : CurrentOwnerPersonId!;
        set
        {
            Owner = value;
            CurrentOwnerPersonId = value;
        }
    }
    public string ExecutiveSponsorId { get => ExecutiveSponsor; set => ExecutiveSponsor = value; }
    public string? ApproverId { get; set; }
    public string? ApprovalGroupId { get => ApprovalGroup; set => ApprovalGroup = value; }
    public string? ReviewOwnerId { get => ReviewOwner; set => ReviewOwner = value; }
    public string ContributionTypeId { get => ContributionType; set => ContributionType = value; }
    public decimal ContributionWeightPct { get => ContributionWeight; set => ContributionWeight = value; }
    public DateTime? StartDate { get => TimeHorizonStart; set => TimeHorizonStart = value; }
    public DateTime? EndDate { get => TimeHorizonEnd; set => TimeHorizonEnd = value; }
    public string DependencyTypeId { get => DependencyType; set => DependencyType = value; }
    public bool InheritScopeFromParentGoal { get => InheritCompanyScope; set => InheritCompanyScope = value; }
    public string? BusinessUnitId { get => BusinessUnit; set => BusinessUnit = value; }
    public string? RegionId { get => Region; set => Region = value; }
    public string PrimaryMetricId { get => PrimaryKpiMetric; set => PrimaryKpiMetric = value; }
    public string UnitOfMeasureId { get => UnitOfMeasure; set => UnitOfMeasure = value; }
    public string PerformanceDirection { get => DirectionOfPerformance; set => DirectionOfPerformance = value; }
    public string ReportingFrequencyId { get => ReportingFrequency; set => ReportingFrequency = value; }
    public string? ThresholdModelId { get => ThresholdModel; set => ThresholdModel = value; }
    public List<string> LinkedRiskIds { get => LinkedRiskIssueIds; set => LinkedRiskIssueIds = value ?? new(); }
    public string? ReviewCadenceId { get => ReviewCadence; set => ReviewCadence = value; }
    public string? ApprovedById { get => ApprovedBy; set => ApprovedBy = value; }
    public List<ObjectiveMetricDto> Targets { get => Metrics; set => Metrics = value ?? new(); }
    public List<ObjectiveMetricDto> MetricAssignments { get => Metrics; set => Metrics = value ?? new(); }
    public List<ObjectiveYearlyBudgetDto> BudgetYearlyValues { get => YearlyBudgets; set => YearlyBudgets = value ?? new(); }
    public string GoalId { get => ParentGoalId; set => ParentGoalId = value; }
}

public sealed class StrategyConnectionDto
{
    public string Id { get; set; } = string.Empty;
    public string FromType { get; set; } = string.Empty;
    public string FromId { get; set; } = string.Empty;
    public string ToType { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = "Supports";
    public string ContributionType { get; set; } = "Supports";
    public decimal ContributionWeight { get; set; }
    public string MetricBindingsJson { get; set; } = "[]";
    public string DecisionReferencesJson { get; set; } = "[]";
    public string EvidenceReferencesJson { get; set; } = "[]";
    public string CompanyScopeMode { get; set; } = "Derived";
    public string? CompanyId { get; set; }
    public string Status { get; set; } = "Draft";
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? RetiredAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class InitiativeContributionPlanValueDto
{
    public string PeriodKey { get; set; } = string.Empty;
    public string PeriodLabel { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal? PlannedValue { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? ForecastValue { get; set; }
    public string Commentary { get; set; } = string.Empty;
}

public sealed class InitiativeReadinessDto
{
    public bool DraftReady { get; set; }
    public bool PlanningReady { get; set; }
    public bool PublishReady { get; set; }
    public string ReadinessStatus { get; set; } = "Blocked";
    public int ContributionPlanRowsCount { get; set; }
    public int MissingContributionValuesCount { get; set; }
    public IReadOnlyList<string> Missing { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Blockers { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class InitiativeStrategyLinkViewDto
{
    public string LinkId { get; set; } = string.Empty;
    public string InitiativeId { get; set; } = string.Empty;
    public string InitiativeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string DeliveryOwnerCompanyId { get; set; } = string.Empty;
    public string DeliveryOwnerPositionId { get; set; } = string.Empty;
    public string DeliveryOwnerPersonId { get; set; } = string.Empty;
    public string ExecutiveSponsor { get; set; } = string.Empty;
    public string AccountableSponsorRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string NormalizedType { get; set; } = string.Empty;
    public string WaveOrPhase { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
    public string Maturity { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string ReportingFrequency { get; set; } = string.Empty;
    public string PrimaryKpi { get; set; } = string.Empty;
    public string ContributionMetricName { get; set; } = string.Empty;
    public string ContributionUnitOfMeasure { get; set; } = string.Empty;
    public string ContributionPlanGranularity { get; set; } = "InheritFromObjective";
    public string ContributionMethod { get; set; } = string.Empty;
    public string ObjectiveTargetGranularity { get; set; } = string.Empty;
    public string ContributionTiming { get; set; } = string.Empty;
    public string BenefitHypothesis { get; set; } = string.Empty;
    public DateTime? BenefitRealizationStart { get; set; }
    public DateTime? BenefitRealizationEnd { get; set; }
    public IReadOnlyList<InitiativeContributionPlanValueDto> ContributionPlanValues { get; set; } = Array.Empty<InitiativeContributionPlanValueDto>();
    public string SourceSystem { get; set; } = "ppm";
    public string SourceRecordId { get; set; } = string.Empty;
    public string ParentObjectiveId { get; set; } = string.Empty;
    public string ParentObjectiveName { get; set; } = string.Empty;
    public string ParentGoalId { get; set; } = string.Empty;
    public string ParentGoalName { get; set; } = string.Empty;
    public string StrategyLinkStatus { get; set; } = "Unlinked";
    public string ContributionType { get; set; } = "Supports";
    public decimal ContributionWeight { get; set; }
    public string MetricBindingsJson { get; set; } = "[]";
    public string? DecisionReference { get; set; }
    public string? EvidenceReference { get; set; }
    public string SponsoringCompanyId { get; set; } = string.Empty;
    public List<string> ParticipatingCompanyIds { get; set; } = new();
    public string EntityScope { get; set; } = string.Empty;
    public string InitiativeClass { get; set; } = string.Empty;
    public string BudgetEnvelope { get; set; } = string.Empty;
    public decimal? BudgetAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string FundingSource { get; set; } = string.Empty;
    public string StrategyAlignmentNote { get; set; } = string.Empty;
    public string GovernanceStage { get; set; } = string.Empty;
    public string GovernanceNotes { get; set; } = string.Empty;
    public bool DependencyFlag { get; set; }
    public InitiativeReadinessDto? Readiness { get; set; }
    public string ReadinessStatus { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime? SyncedAt { get; set; }
    public string SyncFreshness { get; set; } = "Fresh";
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public string? SourceTemplateType { get; set; }
    public string? SourceTemplateId { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
    public string? InstantiationBatchId { get; set; }
    public bool CreatedFromLibrary { get; set; }
}

public sealed class ProjectStrategyLinkViewDto
{
    public string LinkId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerPm { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public string BusinessOwner { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string DeliveryType { get; set; } = string.Empty;
    public string DeliveryMethodology { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string ComplexitySize { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? GoLiveDate { get; set; }
    public string ReportingCadence { get; set; } = string.Empty;
    public string SuccessMetric { get; set; } = string.Empty;
    public string MetricBaseline { get; set; } = string.Empty;
    public string MetricTarget { get; set; } = string.Empty;
    public string RiskRating { get; set; } = string.Empty;
    public string ReadinessStatus { get; set; } = string.Empty;
    public string OverallHealth { get; set; } = string.Empty;
    public string ComplianceRegulatoryImpact { get; set; } = string.Empty;
    public bool DependencyFlag { get; set; }
    public bool EvidenceRequiredFlag { get; set; }
    public string SourceSystem { get; set; } = "ppm";
    public string SourceRecordId { get; set; } = string.Empty;
    public string ParentInitiativeId { get; set; } = string.Empty;
    public string ParentInitiativeName { get; set; } = string.Empty;
    public string ParentObjectiveId { get; set; } = string.Empty;
    public string ParentObjectiveName { get; set; } = string.Empty;
    public string ParentGoalId { get; set; } = string.Empty;
    public string ParentGoalName { get; set; } = string.Empty;
    public string ParentType { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string CreationMode { get; set; } = "Blank";
    public string? TemplateApplicationMode { get; set; }
    public string StrategyLinkStatus { get; set; } = "Unlinked";
    public string ContributionNote { get; set; } = string.Empty;
    public string MetricBindingsJson { get; set; } = "[]";
    public string? DecisionReference { get; set; }
    public string? EvidenceReference { get; set; }
    public string DeliveryCompanyId { get; set; } = string.Empty;
    public string? FundingCompanyId { get; set; }
    public string OwningFunctionDepartment { get; set; } = string.Empty;
    public string DeliveryPartnerVendor { get; set; } = string.Empty;
    public string ScopeSummary { get; set; } = string.Empty;
    public string OutOfScopeNote { get; set; } = string.Empty;
    public bool? BudgetRequired { get; set; }
    public decimal? BudgetAmount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string BudgetType { get; set; } = string.Empty;
    public string BudgetBasis { get; set; } = string.Empty;
    public string FundingSource { get; set; } = string.Empty;
    public string CostCenter { get; set; } = string.Empty;
    public string BudgetOwner { get; set; } = string.Empty;
    public string ApprovalRoute { get; set; } = string.Empty;
    public string FinancialNotes { get; set; } = string.Empty;
    public string NoBudgetReason { get; set; } = string.Empty;
    public string BudgetSummary { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime? SyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string SyncFreshness { get; set; } = "Fresh";
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    public string? SourceTemplateType { get; set; }
    public string? SourceTemplateId { get; set; }
    public string? SourceTemplateName { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
    public string? InstantiationBatchId { get; set; }
    public bool CreatedFromLibrary { get; set; }
}

public sealed class PpmInitiativeReadModelDto
{
    public string InitiativeId { get; set; } = string.Empty;
    public string InitiativeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string WaveOrPhase { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
    public string PrimaryKpi { get; set; } = string.Empty;
    public string BudgetEnvelope { get; set; } = string.Empty;
    public string Maturity { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = "ppm";
    public DateTime? SourceUpdatedAt { get; set; }
    public DateTime? CachedAt { get; set; }
    public bool DegradedMode { get; set; }
}

public sealed class PpmProjectReadModelDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerPm { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string DeliveryType { get; set; } = string.Empty;
    public string SuccessMetric { get; set; } = string.Empty;
    public string RiskRating { get; set; } = string.Empty;
    public string ReadinessStatus { get; set; } = string.Empty;
    public string BudgetSummary { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = "ppm";
    public DateTime? SourceUpdatedAt { get; set; }
    public DateTime? CachedAt { get; set; }
    public bool DegradedMode { get; set; }
}

public sealed class InitiativeDetailDto
{
    public PpmInitiativeReadModelDto Initiative { get; set; } = new();
    public InitiativeStrategyLinkViewDto? StrategyLink { get; set; }
    public ObjectiveDto? ParentObjective { get; set; }
    public GoalDto? ParentGoal { get; set; }
    public InitiativeReadinessDto Readiness { get; set; } = new();
    public IReadOnlyList<ProjectStrategyLinkViewDto> Projects { get; set; } = Array.Empty<ProjectStrategyLinkViewDto>();
    public string TraceabilitySummary { get; set; } = string.Empty;
}

public sealed class ProjectDetailDto
{
    public ProjectStrategyLinkViewDto Project { get; set; } = new();
    public ProjectStrategyLinkViewDto? StrategyLink { get; set; }
    public string TraceabilitySummary { get; set; } = string.Empty;
    public string UpstreamLineage { get; set; } = string.Empty;
    public IReadOnlyList<EnterpriseStrategyAuditEventDto> AuditTrail { get; set; } = Array.Empty<EnterpriseStrategyAuditEventDto>();
}

public sealed class EnterpriseStrategyAuditEventDto
{
    public string Id { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string SourceModule { get; set; } = string.Empty;
    public string BeforeSummary { get; set; } = string.Empty;
    public string AfterSummary { get; set; } = string.Empty;
}

public sealed class ProjectCreationTemplateDto
{
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ParentType { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public int Version { get; set; }
    public string OwnerPm { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string DeliveryType { get; set; } = string.Empty;
    public string DeliveryMethodology { get; set; } = string.Empty;
    public string ComplexitySize { get; set; } = string.Empty;
    public string ReportingCadence { get; set; } = string.Empty;
    public string RiskRating { get; set; } = string.Empty;
    public string ReadinessStatus { get; set; } = string.Empty;
    public string ScopeSummaryTemplate { get; set; } = string.Empty;
    public string ApprovalRoute { get; set; } = string.Empty;
    public string BudgetType { get; set; } = string.Empty;
    public string BudgetBasis { get; set; } = string.Empty;
    public string FundingSource { get; set; } = string.Empty;
    public string CostCenter { get; set; } = string.Empty;
    public string SuccessMetric { get; set; } = string.Empty;
    public string MetricBaseline { get; set; } = string.Empty;
    public string MetricTarget { get; set; } = string.Empty;
}

public sealed class SyncResultDto
{
    public string CorrelationId { get; set; } = string.Empty;
    public int ImportedCount { get; set; }
    public bool DegradedMode { get; set; }
    public string EventName { get; set; } = string.Empty;
}

public sealed class EnterpriseStrategyOverviewDto
{
    public int GoalsCount { get; set; }
    public int ObjectivesCount { get; set; }
    public int ActiveGoalsCount { get; set; }
    public int ActiveObjectivesCount { get; set; }
    public int ConnectionGapsCount { get; set; }
}

public sealed class GoalObjectivesSummaryDto
{
    public int TotalObjectives { get; set; }
    public int ActiveObjectives { get; set; }
    public int ArchivedObjectives { get; set; }
}

public sealed class GoalSummaryDto
{
    public string GoalId { get; set; } = string.Empty;
    public int MetricsCount { get; set; }
    public GoalObjectivesSummaryDto ChildObjectivesSummary { get; set; } = new();
    public int LinkedInitiativesCount { get; set; }
    public int LinkedProjectsCount { get; set; }
    public string? DecisionReference { get; set; }
    public string? EvidenceReference { get; set; }
    public int Version { get; set; }
    public string AuditSummary { get; set; } = string.Empty;
}

public sealed class GoalDetailDto
{
    public GoalDto Goal { get; set; } = new();
    public GoalObjectivesSummaryDto ChildObjectivesSummary { get; set; } = new();
    public int LinkedInitiativesCount { get; set; }
    public int LinkedProjectsCount { get; set; }
    public string AuditSummary { get; set; } = string.Empty;
}

public sealed class GoalPlanningContextDto
{
    public string StrategyPeriodId { get; set; } = string.Empty;
    public string StrategyPeriodStatus { get; set; } = string.Empty;
    public string PlanningCycleId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string CompanyId { get; set; } = string.Empty;
    public string? BusinessUnitId { get; set; }
    public string? RegionId { get; set; }
    public string ReviewCadence { get; set; } = string.Empty;
}

public sealed class ObjectiveAlignmentSummaryDto
{
    public string ObjectiveId { get; set; } = string.Empty;
    public int LinkedInitiativesCount { get; set; }
    public int LinkedProjectsCount { get; set; }
    public bool HasCoverageGap { get; set; }
    public string AuditSummary { get; set; } = string.Empty;
}

public sealed class ObjectiveDetailDto
{
    public ObjectiveDto Objective { get; set; } = new();
    public GoalDto? ParentGoal { get; set; }
    public IReadOnlyList<InitiativeStrategyLinkViewDto> LinkedInitiatives { get; set; } = Array.Empty<InitiativeStrategyLinkViewDto>();
    public IReadOnlyList<ProjectStrategyLinkViewDto> LinkedProjects { get; set; } = Array.Empty<ProjectStrategyLinkViewDto>();
    public ObjectiveAlignmentSummaryDto AlignmentSummary { get; set; } = new();
    public string AuditSummary { get; set; } = string.Empty;
}

public sealed class ConnectionTreeNodeDto
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<ConnectionTreeNodeDto> Children { get; set; } = new();
}

public sealed class ConnectionGraphViewDto
{
    public IReadOnlyList<ConnectionNodeDto> Nodes { get; set; } = Array.Empty<ConnectionNodeDto>();
    public IReadOnlyList<ConnectionEdgeDto> Edges { get; set; } = Array.Empty<ConnectionEdgeDto>();
}

public sealed class ConnectionNodeDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class ConnectionEdgeDto
{
    public string Id { get; set; } = string.Empty;
    public string FromId { get; set; } = string.Empty;
    public string ToId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class ConnectionMatrixCellDto
{
    public string RowId { get; set; } = string.Empty;
    public string ColumnId { get; set; } = string.Empty;
    public string State { get; set; } = "not linked";
}

public sealed class CoverageGapDto
{
    public string GapType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class PagedRequestDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public string? SortBy { get; set; }
    public string? SortDirection { get; set; } = "desc";
    public Dictionary<string, string> Filters { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PagedResponseDto<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
}

public sealed class MutationMetadataDto
{
    public string CorrelationId { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
}

public sealed class StatusChangeRequestDto
{
    public string Status { get; set; } = string.Empty;
    public int ExpectedVersion { get; set; }
}