namespace Diten.Domain.Aggregates.EnterpriseStrategy;

public class StrategicGoalMetricYearlyTarget
{
    public string GoalMetricId { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? ThresholdMin { get; set; }
    public decimal? ThresholdMax { get; set; }
    public string? Commentary { get; set; }

    // Legacy read/write aliases kept for backward compatibility.
    public decimal? BaselineValue { get; set; }
    public decimal? ActualValue { get; set; }
    public decimal? ForecastValue { get; set; }
    public string? ThresholdCommentary { get => Commentary; set => Commentary = value; }
}

public class StrategicGoalMetric
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
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
    public List<GoalMetricYearValue> YearlyTargets { get; set; } = new();
    public int SortOrder { get; set; }
    public string MetricBindingStatus { get; set; } = "Unbound";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Legacy aliases
    public string MetricAssignmentId { get; set; } = string.Empty;
    public string MetricDefId { get => MetricDefinitionId; set => MetricDefinitionId = value; }
    public string PolarityCode { get => DirectionPolarity; set => DirectionPolarity = value; }
    public string ThresholdModelCode { get => ThresholdModel; set => ThresholdModel = value; }
    public string ReportingFrequencyCode { get => ReportingFrequency; set => ReportingFrequency = value; }
}

public class StrategicGoalBudgetEnvelope
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

    // Legacy alias
    public decimal? FundingPoolEnvelope { get => FundingPool; set => FundingPool = value; }
}

public class StrategicGoal
{
    public string GoalId { get; set; } = Guid.NewGuid().ToString("N");
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
    public int Version { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? SourceTemplateType { get; set; }
    public string? SourceTemplateId { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
    public string? InstantiationBatchId { get; set; }
    public bool CreatedFromLibrary { get; set; }
    public List<GoalMetric> Metrics { get; set; } = new();
    public List<GoalYearlyBudgetEnvelope> YearlyBudgets { get; set; } = new();

    // Legacy read/write aliases for existing API/UI and persistence.
    public string Id { get => GoalId; set => GoalId = value; }
    public string Name { get => GoalTitle; set => GoalTitle = value; }
    public string GoalTypeId { get => Category; set => Category = value; }
    public string StrategicTheme { get => StrategicThemeId; set => StrategicThemeId = value; }
    public string Statement { get => GoalStatement; set => GoalStatement = value; }
    public string OwnerId { get => OwnerRole; set => OwnerRole = value; }
    public string Owner { get => OwnerRole; set => OwnerRole = value; }
    public string OwnerPositionId { get => OwnerRole; set => OwnerRole = value; }
    public string? OwnerOrgId { get => OwnerCompanyId; set => OwnerCompanyId = value ?? string.Empty; }
    public string? CurrentOwnerPersonId { get => OwnerPersonId; set => OwnerPersonId = value; }
    public string? OwnerDisplayName { get => OwnerPersonId; set => OwnerPersonId = value; }
    public DateTime? PlanningHorizonStart { get => StartDate; set => StartDate = value; }
    public DateTime? PlanningHorizonEnd { get => EndDate; set => EndDate = value; }
    public string EntityScope { get => RelatedEntityScope; set => RelatedEntityScope = value; }
    public string? EvidenceReference { get => EvidenceLink; set => EvidenceLink = value; }
    public string ScopeMode { get => ApplicabilityMode; set => ApplicabilityMode = value; }
    public bool AppliesToAllCompaniesFlag { get => AppliesToAllCompanies; set => AppliesToAllCompanies = value; }
    public bool AppliesToSelectedCompaniesFlag
    {
        get => !AppliesToAllCompanies && ApplicableCompanyIds.Count > 0;
        set
        {
            if (!value) ApplicableCompanyIds.Clear();
        }
    }
    public string? PrimaryCompanyId { get => OwnerCompanyId; set => OwnerCompanyId = value ?? string.Empty; }
    public string? RelatedEntityScopeSummary
    {
        get => BuildRelatedEntityScopeSummary(RelatedEntityScope, ApplicabilityMode, AppliesToAllCompanies, ApplicableCompanyIds);
        set { }
    }

    private static string BuildRelatedEntityScopeSummary(string scope, string mode, bool appliesToAll, IReadOnlyCollection<string> companies)
    {
        var trimmedScope = scope?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedScope))
            return trimmedScope;

        if (appliesToAll || string.Equals(mode, "Enterprise", StringComparison.OrdinalIgnoreCase))
            return $"{trimmedScope} | All Companies";
        if (companies.Count > 0)
            return $"{trimmedScope} | {companies.Count} companies";
        return trimmedScope;
    }
}

// Legacy type aliases used broadly across the existing codebase.
public sealed class GoalMetricYearValue : StrategicGoalMetricYearlyTarget
{
}
public sealed class GoalMetric : StrategicGoalMetric
{
}
public sealed class GoalYearlyBudgetEnvelope : StrategicGoalBudgetEnvelope
{
}
public sealed class GoalAggregate : StrategicGoal
{
}

public sealed class PlanningCycleAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
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
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime? ArchivedAt { get; set; }
}

public sealed class StrategyPeriodAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
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
    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
    public DateTime? ArchivedAt { get; set; }
}

public sealed class ObjectiveMetric
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
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
    public List<ObjectiveMetricYearValue> YearlyValues { get; set; } = new();
    public DateTime? TargetDate { get; set; }
    public string? FiscalPeriodId { get; set; }
    public decimal? ThresholdValue { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ObjectiveMetricYearValue
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

public sealed class ObjectiveYearlyBudget
{
    public int Year { get; set; }
    public decimal? RequestedBudget { get; set; }
    public decimal? ApprovedBudget { get; set; }
    public decimal? ForecastBudget { get; set; }
    public decimal? ActualBudget { get; set; }
    public decimal? VarianceAmount { get; set; }
    public string? Commentary { get; set; }
}

public sealed class ObjectiveDependencyLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string DependencyTypeId { get; set; } = string.Empty;
    public string DependencyObjectType { get; set; } = string.Empty;
    public string? DependencyReferenceId { get; set; }
    public string? DependencyReferenceText { get; set; }
    public string? DependencyCriticality { get; set; }
}

public sealed class ObjectiveAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
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
    public string? ApproverId { get; set; }
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
    public List<ObjectiveDependencyLink> DependencyLinks { get; set; } = new();
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedOn { get; set; }
    public DateTime? EffectiveDate { get; set; }
    public string? ReviewCadence { get; set; }
    public DateTime? NextReviewDate { get; set; }
    public string? ChangeReason { get; set; }
    public int Version { get; set; } = 1;
    public string? DecisionReference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArchivedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? SourceTemplateType { get; set; }
    public string? SourceTemplateId { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
    public string? InstantiationBatchId { get; set; }
    public bool CreatedFromLibrary { get; set; }
    public List<ObjectiveMetric> Metrics { get; set; } = new();
    public List<ObjectiveYearlyBudget> YearlyBudgets { get; set; } = new();
    public bool AllowMultipleTargetMetrics { get; set; }
    public bool AllowRowThresholdOverrides { get; set; }
}

public sealed class StrategyConnectionAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
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
    public int Version { get; set; } = 1;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? RetiredAt { get; set; }
}

public sealed class InitiativeStrategyLinkAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string InitiativeId { get; set; } = string.Empty;
    public string InitiativeName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string DeliveryOwnerCompanyId { get; set; } = string.Empty;
    public string DeliveryOwnerPositionId { get; set; } = string.Empty;
    public string DeliveryOwnerPersonId { get; set; } = string.Empty;
    public string ExecutiveSponsor { get; set; } = string.Empty;
    public string AccountableSponsorRole { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string Type { get; set; } = string.Empty;
    public string NormalizedType { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string WaveOrPhase { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
    public string Maturity { get; set; } = string.Empty;
    public string PrimaryKpi { get; set; } = string.Empty;
    public string ReportingFrequency { get; set; } = string.Empty;
    public string ContributionMetricName { get; set; } = string.Empty;
    public string ContributionUnitOfMeasure { get; set; } = string.Empty;
    public string ContributionPlanGranularity { get; set; } = "InheritFromObjective";
    public string ContributionMethod { get; set; } = string.Empty;
    public string ContributionTiming { get; set; } = string.Empty;
    public string BenefitHypothesis { get; set; } = string.Empty;
    public DateTime? BenefitRealizationStart { get; set; }
    public DateTime? BenefitRealizationEnd { get; set; }
    public string SourceSystem { get; set; } = "ppm";
    public string SourceRecordId { get; set; } = string.Empty;
    public string ParentObjectiveId { get; set; } = string.Empty;
    public string ParentGoalId { get; set; } = string.Empty;
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
    public List<InitiativeContributionPlanValue> ContributionPlanValues { get; set; } = new();
    public string Notes { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public DateTime? SyncedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? SourceTemplateType { get; set; }
    public string? SourceTemplateId { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
    public string? InstantiationBatchId { get; set; }
    public bool CreatedFromLibrary { get; set; }
}

public sealed class InitiativeContributionPlanValue
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

public sealed class ProjectStrategyLinkAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProjectId { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerPm { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public string BusinessOwner { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
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
    public int Version { get; set; } = 1;
    public DateTime? SyncedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public string? SourceTemplateType { get; set; }
    public string? SourceTemplateId { get; set; }
    public string? SourceTemplateName { get; set; }
    public int? SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
    public string? InstantiationBatchId { get; set; }
    public bool CreatedFromLibrary { get; set; }
}

public sealed class PpmInitiativeReadModelAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
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
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PpmProjectReadModelAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
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
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public sealed class StrategyBlueprintPack
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public int Version { get; set; } = 1;
    public string EntityScope { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class StrategyBlueprintPackItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string BlueprintPackId { get; set; } = string.Empty;
    public string GoalTemplateId { get; set; } = string.Empty;
    public string ObjectiveTemplateId { get; set; } = string.Empty;
    public string InitiativeTemplateId { get; set; } = string.Empty;
    public string ProjectTemplateId { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public int? PlanningYearStart { get; set; }
    public int? PlanningYearEnd { get; set; }
}

public sealed class GoalTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Category { get => Type; set => Type = value; }
    public string Statement { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime? PlanningHorizonStart { get; set; }
    public DateTime? PlanningHorizonEnd { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string DecisionReference { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string ChangeLogRef { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string LifecycleStatus { get; set; } = "Draft";
    public string? Tags { get; set; }
    public List<GoalYearlyBudgetEnvelope> YearlyBudgets { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class GoalTemplateMetric
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string GoalTemplateId { get; set; } = string.Empty;
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
    public List<GoalMetricYearValue> YearlyTargets { get; set; } = new();
}

public sealed class ObjectiveTemplate
{
    public string Id { get; set; } = string.Empty;
    public string ParentGoalTemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Statement { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string Type { get; set; } = string.Empty;
    public DateTime? TimeHorizonStart { get; set; }
    public DateTime? TimeHorizonEnd { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string ContributionType { get; set; } = string.Empty;
    public decimal ContributionWeight { get; set; }
    public string EntityScope { get; set; } = string.Empty;
    public string DependencyNotes { get; set; } = string.Empty;
    public string DecisionReference { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string LifecycleStatus { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class ObjectiveTemplateMetric
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ObjectiveTemplateId { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public decimal BaselineValue { get; set; }
    public decimal TargetValue { get; set; }
    public string AggregationMethod { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
}

public sealed class InitiativeTemplate
{
    public string Id { get; set; } = string.Empty;
    public string ParentObjectiveTemplateId { get; set; } = string.Empty;
    public string ParentGoalTemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string Type { get; set; } = string.Empty;
    public string NormalizedType { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string WaveOrPhase { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Complexity { get; set; } = string.Empty;
    public string DependencyIds { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string BudgetEnvelope { get; set; } = string.Empty;
    public string MaturityReadiness { get; set; } = string.Empty;
    public string DecisionReference { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string InitiativeClass { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string LifecycleStatus { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class InitiativeTemplateMetric
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string InitiativeTemplateId { get; set; } = string.Empty;
    public string SuccessMeasure { get; set; } = string.Empty;
    public decimal BaselineValue { get; set; }
    public decimal TargetValue { get; set; }
}

public sealed class ProjectTemplate
{
    public string Id { get; set; } = string.Empty;
    public string ParentInitiativeTemplateId { get; set; } = string.Empty;
    public string ParentObjectiveTemplateId { get; set; } = string.Empty;
    public string ParentGoalTemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OwnerPm { get; set; } = string.Empty;
    public string Sponsor { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string Phase { get; set; } = string.Empty;
    public string DeliveryMethodology { get; set; } = string.Empty;
    public string ComplexitySize { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string MilestoneFlag { get; set; } = string.Empty;
    public string DependencyIds { get; set; } = string.Empty;
    public string DeliveryType { get; set; } = string.Empty;
    public string EntityScope { get; set; } = string.Empty;
    public string NormalizedParentType { get; set; } = string.Empty;
    public string ReportingCadence { get; set; } = string.Empty;
    public string BudgetSummary { get; set; } = string.Empty;
    public string RiskRating { get; set; } = string.Empty;
    public string ReadinessStatus { get; set; } = string.Empty;
    public string ScopeSummaryTemplate { get; set; } = string.Empty;
    public string ApprovalRoute { get; set; } = string.Empty;
    public string BudgetType { get; set; } = string.Empty;
    public string BudgetBasis { get; set; } = string.Empty;
    public string FundingSource { get; set; } = string.Empty;
    public string CostCenter { get; set; } = string.Empty;
    public string DecisionReference { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public string LifecycleStatus { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class ProjectTemplateMetric
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProjectTemplateId { get; set; } = string.Empty;
    public string SuccessMetric { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty;
    public decimal BaselineValue { get; set; }
    public decimal TargetValue { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}

public sealed class TemplateImportBatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
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
}

public sealed class TemplateImportIssue
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string BatchId { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string SheetName { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class TemplateVersion
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TemplateType { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string Status { get; set; } = "Draft";
    public string ChangeSummary { get; set; } = string.Empty;
    public string ChangedBy { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

public sealed class TemplatePublishHistory
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TemplateType { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public sealed class TemplateUsageStat
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ItemType { get; set; } = string.Empty; // Template | BlueprintPack
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public string LastInstantiatedBy { get; set; } = string.Empty;
    public DateTime? LastInstantiatedAt { get; set; }
}

public sealed class TemplateOverrideLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string InstantiationBatchId { get; set; } = string.Empty;
    public string RuntimeObjectType { get; set; } = string.Empty;
    public string RuntimeObjectId { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string BeforeValue { get; set; } = string.Empty;
    public string AfterValue { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public sealed class InstantiationBatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string SourceType { get; set; } = string.Empty; // Template | BlueprintPack
    public string SourceId { get; set; } = string.Empty;
    public bool FullChain { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}

public sealed class InstantiationRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string InstantiationBatchId { get; set; } = string.Empty;
    public string RuntimeObjectType { get; set; } = string.Empty;
    public string RuntimeObjectId { get; set; } = string.Empty;
    public string SourceTemplateType { get; set; } = string.Empty;
    public string SourceTemplateId { get; set; } = string.Empty;
    public int SourceTemplateVersion { get; set; }
    public string? SourceBlueprintPackId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class KpiTemplateAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string StrategicPerspective { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string ObjectLevel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string BusinessQuestion { get; set; } = string.Empty;
    public string Polarity { get; set; } = string.Empty;
    public string UnitOfMeasure { get; set; } = string.Empty;
    public string AggregationMethod { get; set; } = string.Empty;
    public string ReportingFrequency { get; set; } = string.Empty;
    public string FormulaType { get; set; } = string.Empty;
    public string NumeratorDefinition { get; set; } = string.Empty;
    public string DenominatorDefinition { get; set; } = string.Empty;
    public string FormulaExpression { get; set; } = string.Empty;
    public string BaselineLogic { get; set; } = string.Empty;
    public string TargetLogic { get; set; } = string.Empty;
    public string ThresholdModelCode { get; set; } = string.Empty;
    public string DefaultOwnerRole { get; set; } = string.Empty;
    public string ReviewRole { get; set; } = string.Empty;
    public string DataSourcePattern { get; set; } = string.Empty;
    public string EvidenceRequirement { get; set; } = string.Empty;
    public string DecisionReferenceRequirement { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string VersionLabel { get; set; } = "v1.0";
    public DateTime? PublishDate { get; set; }
    public string Tags { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public string? LastUsedBy { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class KpiThresholdModelAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ModelCode { get; set; } = string.Empty;
    public string MetricUnit { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string Polarity { get; set; } = string.Empty;
    public decimal? RedFloor { get; set; }
    public decimal? AmberFloor { get; set; }
    public decimal? GreenTarget { get; set; }
    public decimal? GreenStretch { get; set; }
    public decimal? UpperControlLimit { get; set; }
    public string Interpretation { get; set; } = string.Empty;
    public string Status { get; set; } = "Published";
    public string VersionLabel { get; set; } = "v1.0";
    public DateTime? PublishDate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class KpiScorecardPackAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PackCode { get; set; } = string.Empty;
    public string PackName { get; set; } = string.Empty;
    public string PackLevel { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public string VersionLabel { get; set; } = "v1.0";
    public DateTime? PublishDate { get; set; }
    public string DefaultOwnerRole { get; set; } = string.Empty;
    public int UsageCount { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class KpiScorecardPackItemAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string PackId { get; set; } = string.Empty;
    public string PackCode { get; set; } = string.Empty;
    public string KpiTemplateId { get; set; } = string.Empty;
    public string KpiTemplateCode { get; set; } = string.Empty;
    public string KpiTemplateName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string PriorityClass { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
}

public sealed class KpiCatalogItemAggregate
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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class KpiGovernanceActionAggregate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string BeforeStatus { get; set; } = string.Empty;
    public string AfterStatus { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public DateTime At { get; set; } = DateTime.UtcNow;
}
