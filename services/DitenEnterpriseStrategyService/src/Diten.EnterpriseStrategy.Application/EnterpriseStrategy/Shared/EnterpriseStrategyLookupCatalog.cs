using Diten.Application.Dtos.EnterpriseStrategy;
using System.Linq;

namespace Diten.Application.EnterpriseStrategy.Shared;

/// <summary>
/// Canonical lookup lists for enterprise strategy UI until replaced by dedicated reference tables.
/// </summary>
public static class EnterpriseStrategyLookupCatalog
{
    private static readonly string[] AggregationTypes =
    {
        "Sum", "Average", "Weighted Average", "Minimum", "Maximum", "Latest Value"
    };

    private static readonly string[] ThresholdModelValues =
    {
        "Green / Amber / Red", "Target Range", "Minimum Threshold", "Maximum Threshold", "Tolerance Band"
    };

    public static IReadOnlyList<OwnerReferenceDto> OwnerReferences { get; } = new List<OwnerReferenceDto>
    {
        new() { OwnerId = "emp-001", DisplayName = "Beste Pullukcu — Strategy Director — Alpha Holdings" },
        new() { OwnerId = "emp-002", DisplayName = "Emre Karaca — Finance Transformation Lead — Beta Manufacturing" },
        new() { OwnerId = "emp-003", DisplayName = "Deniz Yilmaz — Portfolio Governance Manager — Gamma Digital" },
        new() { OwnerId = "emp-004", DisplayName = "Ipek Candan — Regional Strategy Partner — Delta Services" }
    };

    private static readonly string[] OwnerDisplayNames = OwnerReferences
        .Select(x => x.DisplayName)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToArray();

    public static IReadOnlyList<CompanyReferenceDto> Companies { get; } = new List<CompanyReferenceDto>
    {
        new() { CompanyId = "cmp-001", CompanyCode = "ALPHA", CompanyName = "Alpha Holdings", Status = "Active", Region = "Global", BusinessUnit = "Corporate" },
        new() { CompanyId = "cmp-002", CompanyCode = "BETA", CompanyName = "Beta Manufacturing", Status = "Active", Region = "EMEA", BusinessUnit = "Operations" },
        new() { CompanyId = "cmp-003", CompanyCode = "GAMMA", CompanyName = "Gamma Digital", Status = "Active", Region = "APAC", BusinessUnit = "Technology" },
        new() { CompanyId = "cmp-004", CompanyCode = "DELTA", CompanyName = "Delta Services", Status = "Active", Region = "North America", BusinessUnit = "Shared Services" },
        new() { CompanyId = "cmp-005", CompanyCode = "EPS", CompanyName = "Epsilon Markets", Status = "Active", Region = "South America", BusinessUnit = "Commercial" }
    };

    public static EnterpriseStrategyWorkbookLookupsDto BuildWorkbookLookups() => new()
    {
        Owners = OwnerDisplayNames,
        OwnerReferences = OwnerReferences,
        Priorities = new[] { "Critical", "High", "Medium", "Low" },
        ComplexityRiskScale = new[] { "Very High", "High", "Medium", "Low", "Critical", "Moderate" },
        LifecycleStatus = new[] { "Proposed", "Planned", "Approved", "In Progress", "Completed", "Cancelled" },
        ApprovalStatus = new[] { "Draft", "Pending Approval", "Approved", "Rejected", "Rework Required" },
        GoalObjectiveTypes = GoalTemplateTypeCatalog.AllowedTypes,
        InitiativeTypes = new[]
        {
            "Transformation Initiative", "Improvement Initiative", "Compliance Initiative",
            "Innovation Initiative", "Capability Initiative", "Cost Optimization Initiative"
        },
        StrategicThemes = new[]
        {
            "Digital Transformation", "Operational Excellence", "Customer Growth", "Compliance Excellence",
            "Cost Leadership", "Data-Driven Decision Making", "Talent & Capability", "Sustainability"
        },
        ContributionTypes = new[] { "Direct", "Supports", "Enabling", "Dependent" },
        DependencyTypes = new[] { "None", "Predecessor", "Successor", "Mutual", "External" },
        DirectionOfPerformance = new[] { "Increase", "Decrease", "Maintain", "Within Range" },
        ReportingFrequencies = new[] { "Real Time", "Daily", "Weekly", "Monthly", "Quarterly", "Annually" },
        ThresholdModels = ThresholdModelValues,
        ReviewCadences = EnterpriseStrategyPlanningLookupCatalog.ReviewCadenceValues.ToArray(),
        BusinessUnits = new[] { "IT", "Operations", "Finance", "HR", "Legal", "Quality", "PMO" },
        Regions = new[] { "Global", "EMEA", "APAC", "North America", "South America", "Germany", "UK", "US", "HQ" },
        ApprovalGroups = new[] { "esbp-gov-board", "esbp-investment-committee", "esbp-exec-council" },
        ApprovalRouteTypes = new[] { "IndividualApprover", "ApprovalGroup" },
        PlanningCycles = new[] { "cycle-fy2026", "cycle-fy2027", "cycle-fy2028", "cycle-fy2029", "cycle-fy2030" },
        PlanningCycleTypes = EnterpriseStrategyPlanningLookupCatalog.PlanningCycleTypeValues.ToArray(),
        PlanningLifecycleStatuses = EnterpriseStrategyPlanningLookupCatalog.LifecycleStatusValues.ToArray(),
        StrategyPeriodLifecycleStatuses = EnterpriseStrategyPlanningLookupCatalog.LifecycleStatusValues.ToArray(),
        StrategyPeriodScenarioTypes = EnterpriseStrategyPlanningLookupCatalog.ScenarioTypeValues.ToArray(),
        RiskIds = new[] { "risk-001", "risk-002", "risk-003", "risk-004" },
        FiscalPeriods = new[] { "FY2026", "FY2027", "Q1", "Q2", "Q3", "Q4", "Monthly Cycle" },
        DependencyObjectTypes = new[] { "Objective", "Initiative", "Project", "External", "Milestone", "Other" },
        DependencyCriticalities = new[] { "Critical", "High", "Medium", "Low" },
        EntityScopes = new[]
        {
            "Enterprise / BU / Market", "Enterprise / BU / Product", "Market / Segment / Account",
            "Plant / Function / Process", "Innovation Portfolio / Venture / Product", "Enterprise / Function",
            "Enterprise / Control Environment", "Enterprise / Program", "Enterprise / Supply Chain",
            "Enterprise / Function / Workforce", "Customer Journey / Channel / Region", "Enterprise / Portfolio / Entity"
        },
        UnitOfMeasure = new[]
        {
            "Percentage", "Currency", "Count", "Days", "Hours", "Minutes", "Ratio",
            "Index", "Score", "FTE", "Kg", "Liters", "Units", "Batches"
        },
        GoalMetricType = new[] { "%", "Sum", "Index", "Count", "Score", "Ratio", "%/Score" },
        ObjectiveMetricType = new[] { "%", "Sum", "Count", "Ratio", "Days", "Index", "Rank", "Score", "Hours/Days", "Count/Ratio", "Rate", "Hours", "%/Score", "%/Rate" },
        InitiativeMetricType = new[] { "%", "Sum", "Ratio", "Count", "Days" },
        ProjectMetricType = new[] { "%", "Sum", "Score", "Count", "Days", "Ratio" },
        GoalAggregation = AggregationTypes,
        ConnectionAggregation = AggregationTypes,
        ObjectiveTargetAggregation = AggregationTypes,
        WaveValues = new[] { "Wave 1" },
        MaturityValues = new[] { "Emerging", "Defined", "Ready", "In Flight", "Scaled", "Stabilized" },
        ProjectOwnerValues = OwnerDisplayNames,
        ProjectSponsorValues = OwnerDisplayNames,
        ProjectStageValues = new[] { "Discovery", "Design", "Build", "Test", "Deploy", "Stabilize", "Close" },
        ProjectDeliveryValues = new[] { "Implementation" },
        ReadinessValues = new[] { "Not Started", "Ready", "In Progress", "Blocked", "At Risk", "Complete", "Planned" },
        ScopeModeValues = new[] { "Enterprise", "SingleCompany", "MultiCompany", "AppliesToSelectedCompanies" },
        CurrencyCodes = new[] { "USD", "EUR", "GBP", "AED", "SAR", "JPY", "INR", "CNY" },
        BudgetTypeValues = new[] { "CapEx", "OpEx", "Mixed" },
        BudgetBasisValues = new[] { "Top-down", "Bottom-up", "Hybrid" },
        ProjectNumberingScheme = "PROJ-YYYY-NNNN",
        Positions = new[] { "CEO", "CTO", "CFO", "Director", "Manager", "Analyst" },
        Companies = Companies
    };
}
