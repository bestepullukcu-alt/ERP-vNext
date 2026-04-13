using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Domain.Aggregates.EnterpriseStrategy;

namespace Diten.Application.EnterpriseStrategy.Validators;

public static class EnterpriseStrategyValidators
{
    public static Dictionary<string, List<string>> ValidateGoal(GoalDto goal)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var ownerRole = string.IsNullOrWhiteSpace(goal.OwnerRole)
            ? (string.IsNullOrWhiteSpace(goal.OwnerId) ? goal.Owner : goal.OwnerId)
            : goal.OwnerRole;
        var ownerCompanyId = string.IsNullOrWhiteSpace(goal.OwnerCompanyId)
            ? (goal.PrimaryCompanyId ?? string.Empty)
            : goal.OwnerCompanyId;
        var goalTitle = string.IsNullOrWhiteSpace(goal.GoalTitle) ? goal.Name : goal.GoalTitle;
        var goalStatement = string.IsNullOrWhiteSpace(goal.GoalStatement) ? goal.Statement : goal.GoalStatement;
        var applicabilityMode = string.IsNullOrWhiteSpace(goal.ApplicabilityMode) ? goal.ScopeMode : goal.ApplicabilityMode;
        var isPublish = !string.Equals(goal.Status, "Draft", StringComparison.OrdinalIgnoreCase);
        var categories = new HashSet<string>(
            EnterpriseStrategyLookupCatalog.BuildWorkbookLookups().GoalObjectiveTypes,
            StringComparer.OrdinalIgnoreCase);
        var statuses = new HashSet<string>(
            EnterpriseStrategyLookupCatalog.BuildWorkbookLookups().LifecycleStatus
                .Concat(new[] { "Draft", "Active", "On Hold", "Archived" }),
            StringComparer.OrdinalIgnoreCase);
        var priorities = new HashSet<string>(
            EnterpriseStrategyLookupCatalog.BuildWorkbookLookups().Priorities,
            StringComparer.OrdinalIgnoreCase);
        var metricTypes = new HashSet<string>(
            EnterpriseStrategyLookupCatalog.BuildWorkbookLookups().GoalMetricType,
            StringComparer.OrdinalIgnoreCase);
        var uom = new HashSet<string>(
            EnterpriseStrategyLookupCatalog.BuildWorkbookLookups().UnitOfMeasure,
            StringComparer.OrdinalIgnoreCase);
        var aggregation = new HashSet<string>(
            EnterpriseStrategyLookupCatalog.BuildWorkbookLookups().GoalAggregation,
            StringComparer.OrdinalIgnoreCase);

        // Draft validation (minimum required)
        AddIf(errors, string.IsNullOrWhiteSpace(goalTitle), "name", "Goal Title is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(goalTitle), "goalTitle", "Goal Title is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(goal.Category), "category", "Category is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(goal.StrategicThemeId), "strategicThemeId", "Strategic theme is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(goal.Status), "status", "Status is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(goal.Priority), "priority", "Priority is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(goalStatement), "statement", "Goal statement is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(goalStatement), "goalStatement", "Goal statement is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(ownerRole), "ownerRole", "Owner Role is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(ownerRole), "ownerId", "Owner Role is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(ownerCompanyId), "ownerCompanyId", "Owner Company is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(goal.StrategyPeriodId), "strategyPeriodId", "Strategy Period is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(applicabilityMode), "applicabilityMode", "Applicability Mode is required.");
        if (goal.StartDate.HasValue && goal.EndDate.HasValue)
            AddIf(errors, goal.EndDate.Value.Date < goal.StartDate.Value.Date, "planning.endDate", "End Date must be on or after Start Date.");

        if (!string.IsNullOrWhiteSpace(goal.Category))
            AddIf(errors, !categories.Contains(goal.Category), "categoryCode", "Category code is invalid.");
        if (!string.IsNullOrWhiteSpace(goal.Status))
            AddIf(errors, !statuses.Contains(goal.Status), "statusCode", "Status code is invalid.");
        if (!string.IsNullOrWhiteSpace(goal.Priority))
            AddIf(errors, !priorities.Contains(goal.Priority), "priorityCode", "Priority code is invalid.");

        if (!string.IsNullOrWhiteSpace(goal.EvidenceLink))
            AddIf(errors, !Uri.TryCreate(goal.EvidenceLink, UriKind.Absolute, out _), "governance.evidenceLink", "Evidence Link must be a valid URL.");

        var mode = (applicabilityMode ?? string.Empty).Trim();
        var primaryCompanyId = string.IsNullOrWhiteSpace(ownerCompanyId) ? null : ownerCompanyId.Trim();
        var applicableCompanyIds = (goal.ApplicableCompanyIds ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        goal.OwnerRole = ownerRole;
        goal.OwnerCompanyId = primaryCompanyId ?? string.Empty;
        goal.PrimaryCompanyId = primaryCompanyId;
        goal.GoalTitle = goalTitle;
        goal.Name = goalTitle;
        goal.GoalStatement = goalStatement;
        goal.Statement = goalStatement;
        goal.ApplicabilityMode = mode;
        goal.ScopeMode = mode;
        goal.ApplicableCompanyIds = applicableCompanyIds;
        AddIf(errors, string.IsNullOrWhiteSpace(mode), "companyScope.scopeModeCode", "Scope Mode is required.");
        if (!string.IsNullOrWhiteSpace(mode))
        {
            if (!mode.Equals("Enterprise", StringComparison.OrdinalIgnoreCase) &&
                !mode.Equals("SingleCompany", StringComparison.OrdinalIgnoreCase) &&
                !mode.Equals("MultiCompany", StringComparison.OrdinalIgnoreCase) &&
                !mode.Equals("AppliesToSelectedCompanies", StringComparison.OrdinalIgnoreCase))
            {
                AddIf(errors, true, "companyScope.scopeModeCode", "Scope mode must be Enterprise or AppliesToSelectedCompanies.");
            }

            if (mode.Equals("Enterprise", StringComparison.OrdinalIgnoreCase))
            {
                AddIf(errors, applicableCompanyIds.Count > 0, "companyScope.applicableCompanyIds", "Applicable companies must be empty for Enterprise scope metadata.");
            }
            else if (mode.Equals("SingleCompany", StringComparison.OrdinalIgnoreCase))
            {
                AddIf(errors, string.IsNullOrWhiteSpace(primaryCompanyId), "companyScope.primaryCompanyId", "Primary Company is required for SingleCompany scope metadata.");
                if (!string.IsNullOrWhiteSpace(primaryCompanyId) && applicableCompanyIds.Count > 0)
                {
                    AddIf(
                        errors,
                        !applicableCompanyIds.Contains(primaryCompanyId, StringComparer.OrdinalIgnoreCase),
                        "companyScope.applicableCompanyIds",
                        "Applicable companies must be empty or include Primary Company for SingleCompany scope metadata.");
                }
            }
            else if ((mode.Equals("MultiCompany", StringComparison.OrdinalIgnoreCase) || mode.Equals("AppliesToSelectedCompanies", StringComparison.OrdinalIgnoreCase)) &&
                     applicableCompanyIds.Count == 0)
            {
                AddIf(errors, true, "companyScope.applicableCompanyIds", "At least one applicable company is required for selected-company applicability metadata.");
            }
            else if ((mode.Equals("MultiCompany", StringComparison.OrdinalIgnoreCase) || mode.Equals("AppliesToSelectedCompanies", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(primaryCompanyId))
            {
                AddIf(errors, !applicableCompanyIds.Contains(primaryCompanyId, StringComparer.OrdinalIgnoreCase), "companyScope.applicableCompanyIds", "Primary Company must be included in Applicable Companies for MultiCompany scope metadata.");
            }
        }

        var planningStartYear = goal.StartDate?.Year;
        var planningEndYear = goal.EndDate?.Year;
        var budgets = goal.BudgetEnvelopes ?? new List<GoalYearlyBudgetEnvelopeDto>();
        var budgetYears = budgets.Select(x => x.Year).ToList();
        var bdups = budgetYears.GroupBy(y => y).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (bdups.Count > 0)
            AddIf(errors, true, "yearlyBudgets", $"Duplicate yearly budget rows: {string.Join(", ", bdups)}.");
        // Budget section is optional; only validate row bounds when rows exist.
        if (budgets.Count > 0 && planningStartYear.HasValue && planningEndYear.HasValue)
        {
            foreach (var row in budgets)
            {
                AddIf(
                    errors,
                    row.Year < planningStartYear.Value || row.Year > planningEndYear.Value,
                    "yearlyBudgets",
                    $"Budget year {row.Year} is outside planning horizon {planningStartYear.Value}-{planningEndYear.Value}.");
            }
        }

        var metrics = goal.Metrics ?? new List<GoalMetricDto>();
        if (isPublish)
        {
            var activeMetrics = metrics.Where(IsActiveGoalMetric).ToList();
            AddIf(errors, activeMetrics.Count == 0, "metrics", "At least 1 active KPI is required for publish.");
        }

        for (var i = 0; i < metrics.Count; i++)
        {
            var m = metrics[i];
            var p = $"metrics[{i}]";
            var directionPolarity = string.IsNullOrWhiteSpace(m.DirectionPolarity) ? m.PolarityCode : m.DirectionPolarity;
            var thresholdModel = string.IsNullOrWhiteSpace(m.ThresholdModel) ? m.ThresholdModelCode : m.ThresholdModel;
            var reportingFrequency = string.IsNullOrWhiteSpace(m.ReportingFrequency) ? m.ReportingFrequencyCode : m.ReportingFrequency;
            if (isPublish && IsActiveGoalMetric(m))
            {
                AddIf(errors, string.IsNullOrWhiteSpace(m.MetricName), $"{p}.metricName", "Goal Metric is required.");
                AddIf(errors, string.IsNullOrWhiteSpace(m.MetricType), $"{p}.metricTypeCode", "Goal Metric Type is required.");
                AddIf(errors, string.IsNullOrWhiteSpace(m.UnitOfMeasure), $"{p}.unitOfMeasureCode", "Unit of Measure is required.");
                AddIf(errors, string.IsNullOrWhiteSpace(m.AggregationMethod), $"{p}.aggregationMethodCode", "Aggregation Method is required.");
                AddIf(errors, string.IsNullOrWhiteSpace(directionPolarity), $"{p}.directionPolarity", "Direction / polarity is required.");
                AddIf(errors, string.IsNullOrWhiteSpace(thresholdModel), $"{p}.thresholdModel", "Threshold model is required.");
                AddIf(errors, string.IsNullOrWhiteSpace(reportingFrequency), $"{p}.reportingFrequency", "Reporting frequency is required.");
            }

            var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Local", "Inherited" };
            AddIf(errors, !origins.Contains((m.MetricOrigin ?? string.Empty).Trim()), $"{p}.metricOrigin", "Metric Origin must be Local or Inherited.");
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Strategic" };
            AddIf(errors, string.IsNullOrWhiteSpace(m.MetricRole) || !roles.Contains(m.MetricRole.Trim()), $"{p}.metricRole", "Goal metric role must be Strategic.");
            var restrictions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GoalGovernedStructure", "LocalEditable", "ParentGovernedStructure" };
            AddIf(errors, !restrictions.Contains((m.RestrictionMode ?? string.Empty).Trim()), $"{p}.restrictionMode", "Restriction Mode is invalid.");
            if (string.Equals((m.MetricOrigin ?? string.Empty).Trim(), "Inherited", StringComparison.OrdinalIgnoreCase))
            {
                // Structural fields still required; downstream layers may lock edits in UI.
            }
            if (!string.IsNullOrWhiteSpace(m.MetricType))
                AddIf(errors, !metricTypes.Contains(m.MetricType), $"{p}.metricTypeCode", "Metric type code is invalid.");
            if (!string.IsNullOrWhiteSpace(m.UnitOfMeasure))
                AddIf(errors, !uom.Contains(m.UnitOfMeasure), $"{p}.unitOfMeasureCode", "Unit of measure code is invalid.");
            if (!string.IsNullOrWhiteSpace(m.AggregationMethod))
                AddIf(errors, !aggregation.Contains(m.AggregationMethod), $"{p}.aggregationMethodCode", "Aggregation method code is invalid.");

            var yearly = (m.YearlyValues ?? new()).OrderBy(x => x.Year).ToList();
            var duplicates = yearly.GroupBy(x => x.Year).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            if (duplicates.Count > 0)
                AddIf(errors, true, $"{p}.yearlyValues", $"Duplicate yearly entries found: {string.Join(", ", duplicates)}.");

            if (isPublish && IsActiveGoalMetric(m))
            {
                AddIf(errors, yearly.Count == 0, $"{p}.yearlyValues", "Yearly targets are required for active KPI rows.");
                foreach (var yearVal in yearly.Where(x => x.Year > 0))
                {
                    AddIf(
                        errors,
                        yearVal.TargetValue is null,
                        $"{p}.yearlyValues",
                        $"Target Value is required for year {yearVal.Year}.");
                }

                if (planningStartYear.HasValue && planningEndYear.HasValue)
                {
                    foreach (var yearVal in yearly)
                    {
                        AddIf(
                            errors,
                            yearVal.Year < planningStartYear.Value || yearVal.Year > planningEndYear.Value,
                            $"{p}.yearlyValues",
                            $"Year {yearVal.Year} is outside planning horizon {planningStartYear.Value}-{planningEndYear.Value}.");
                    }
                }
            }
        }

        var creationMode = (goal.SourceTemplateType ?? string.Empty).Trim();
        if (creationMode.Equals("Template", StringComparison.OrdinalIgnoreCase))
            AddIf(errors, string.IsNullOrWhiteSpace(goal.SourceTemplateId), "sourceTemplateId", "SourceTemplateId is required when CreationModeCode is Template.");
        if (creationMode.Equals("BlueprintPack", StringComparison.OrdinalIgnoreCase) ||
            creationMode.Equals("Pack", StringComparison.OrdinalIgnoreCase))
            AddIf(errors, string.IsNullOrWhiteSpace(goal.SourceBlueprintPackId), "sourceBlueprintPackId", "SourceBlueprintPackId is required when CreationModeCode is BlueprintPack.");

        if (isPublish)
        {
            AddIf(errors, goal.Version <= 0, "version", "Version is required for publish.");
            AddIf(errors, string.IsNullOrWhiteSpace(goal.ChangeLogRef), "changeLogRef", "ChangeLogRef is required for publish.");
            AddIf(errors, string.IsNullOrWhiteSpace(goal.DecisionReference), "decisionReference", "DecisionReference is required for publish.");
        }

        if (goal.SaveAsTemplate)
        {
            AddIf(errors, goal.TemplateSave is null, "templateSave", "TemplateSave metadata is required when SaveAsTemplate is true.");
            if (goal.TemplateSave is not null)
                AddIf(errors, string.IsNullOrWhiteSpace(goal.TemplateSave.TemplateName), "templateSave.templateName", "Template name is required when SaveAsTemplate is true.");
        }

        return errors;
    }

    private static bool IsActiveGoalMetric(GoalMetricDto metric)
    {
        if (metric is null) return false;
        var status = (metric.MetricBindingStatus ?? string.Empty).Trim();
        if (status.Equals("Inactive", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Archived", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Removed", StringComparison.OrdinalIgnoreCase))
            return false;
        return !string.IsNullOrWhiteSpace(metric.MetricName);
    }

    public static Dictionary<string, List<string>> ValidateObjective(ObjectiveDto objective)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var lifecycleState = (objective.Status ?? string.Empty).Trim();
        var isDraft = string.IsNullOrWhiteSpace(lifecycleState) || lifecycleState.Equals("Draft", StringComparison.OrdinalIgnoreCase);
        var ownerCompanyId = (objective.OwnerCompanyId ?? string.Empty).Trim();
        var ownerPositionId = (objective.OwnerPositionId ?? string.Empty).Trim();
        var currentOwnerPersonId = string.IsNullOrWhiteSpace(objective.CurrentOwnerPersonId)
            ? (string.IsNullOrWhiteSpace(objective.OwnerId) ? objective.Owner : objective.OwnerId)
            : objective.CurrentOwnerPersonId;
        var ownerId = string.IsNullOrWhiteSpace(currentOwnerPersonId) ? ownerPositionId : currentOwnerPersonId;
        var strategicThemeId = string.IsNullOrWhiteSpace(objective.StrategicThemeId) ? objective.StrategicTheme : objective.StrategicThemeId;
        var objectiveTypeId = string.IsNullOrWhiteSpace(objective.ObjectiveTypeId) ? objective.Type : objective.ObjectiveTypeId;
        var inheritedStrategyPeriodId = string.IsNullOrWhiteSpace(objective.StrategyPeriodId) ? objective.PlanningCycleId : objective.StrategyPeriodId;
        var primaryMetricId = string.IsNullOrWhiteSpace(objective.PrimaryMetricId) ? objective.PrimaryKpiMetric : objective.PrimaryMetricId;
        var direction = string.IsNullOrWhiteSpace(objective.PerformanceDirection) ? objective.DirectionOfPerformance : objective.PerformanceDirection;
        var unitOfMeasureId = string.IsNullOrWhiteSpace(objective.UnitOfMeasureId) ? objective.UnitOfMeasure : objective.UnitOfMeasureId;
        var reportingFrequencyId = string.IsNullOrWhiteSpace(objective.ReportingFrequencyId) ? objective.ReportingFrequency : objective.ReportingFrequencyId;
        var targetPlanGranularity = ObjectiveTargetPlanPeriodHelper.NormalizeGranularity(objective.TargetPlanGranularity);

        objective.OwnerCompanyId = ownerCompanyId;
        objective.OwnerPositionId = ownerPositionId;
        objective.CurrentOwnerPersonId = currentOwnerPersonId;
        objective.Owner = ownerId;
        objective.OwnerId = ownerId;
        objective.StrategicTheme = strategicThemeId;
        objective.StrategicThemeId = strategicThemeId;
        objective.Type = objectiveTypeId;
        objective.ObjectiveTypeId = objectiveTypeId;
        objective.PrimaryKpiMetric = primaryMetricId;
        objective.PrimaryMetricId = primaryMetricId;
        objective.PerformanceDirection = direction;
        objective.DirectionOfPerformance = direction;
        objective.UnitOfMeasureId = unitOfMeasureId;
        objective.UnitOfMeasure = unitOfMeasureId;
        objective.ReportingFrequencyId = reportingFrequencyId;
        objective.ReportingFrequency = reportingFrequencyId;
        objective.TargetPlanGranularity = targetPlanGranularity;

        AddIf(errors, string.IsNullOrWhiteSpace(objective.ParentGoalId), "parentGoalId", "Parent goal is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(objective.ParentGoalId), "goal_id", "Parent goal is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(objective.Name), "name", "Objective name is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(objective.Statement), "statement", "Objective statement is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(strategicThemeId), "strategicTheme", "Strategic theme is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(strategicThemeId), "strategicThemeId", "StrategicThemeId is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(ownerCompanyId), "ownerCompanyId", "Owner Company / Org is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(ownerPositionId), "ownerPositionId", "Owner Position is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(objectiveTypeId), "type", "Objective type is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(objectiveTypeId), "objectiveTypeId", "ObjectiveTypeId is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(objective.Status), "status", "Lifecycle state is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(objective.Priority), "priority", "Priority is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(inheritedStrategyPeriodId), "planningCycleId", "StrategyPeriodId (inherited from Parent Goal) is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(inheritedStrategyPeriodId), "strategyPeriodId", "StrategyPeriodId (inherited from Parent Goal) is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(primaryMetricId), "primaryKpiMetric", "Primary KPI / Metric is required.");
        AddIf(errors, string.IsNullOrWhiteSpace(primaryMetricId), "primaryMetricId", "PrimaryMetricId is required.");

        AddIf(errors, !objective.TimeHorizonStart.HasValue, "timeHorizonStart", "Start Date is required.");
        AddIf(errors, !objective.TimeHorizonStart.HasValue, "start_year", "Start Date is required.");
        AddIf(errors, !objective.TimeHorizonEnd.HasValue, "timeHorizonEnd", "End Date is required.");
        AddIf(errors, !objective.TimeHorizonEnd.HasValue, "end_year", "End Date is required.");
        if (objective.TimeHorizonStart.HasValue && objective.TimeHorizonEnd.HasValue)
        {
            AddIf(errors, objective.TimeHorizonEnd.Value.Date < objective.TimeHorizonStart.Value.Date, "timeHorizonEnd", "End Date must be greater than or equal to Start Date.");
            AddIf(errors, objective.TimeHorizonEnd.Value.Date < objective.TimeHorizonStart.Value.Date, "end_year", "End Date must be greater than or equal to Start Date.");
        }

        AddIf(errors, objective.ContributionWeight < 0 || objective.ContributionWeight > 100, "contributionWeight", "Contribution weight must be between 0 and 100.");
        if (!objective.InheritCompanyScope)
        {
            AddIf(
                errors,
                string.IsNullOrWhiteSpace(objective.PrimaryCompanyId) && (objective.ApplicableCompanyIds?.Count ?? 0) == 0,
                "companyScope",
                "Objective must include PrimaryCompanyId or ApplicableCompanyIds when inherited scope is unlocked.");
            AddIf(
                errors,
                string.IsNullOrWhiteSpace(objective.PrimaryCompanyId) && (objective.ApplicableCompanyIds?.Count ?? 0) == 0,
                "company_id",
                "Objective must include PrimaryCompanyId or ApplicableCompanyIds when inherited scope is unlocked.");
        }

        if (!isDraft)
        {
            AddIf(errors, string.IsNullOrWhiteSpace(objective.ExecutiveSponsor), "executivesponsor", "Executive sponsor is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(objective.ExecutiveSponsorId), "executiveSponsorId", "ExecutiveSponsorId is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(objective.ApprovalStatus), "approvalstatus", "Approval status is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(objective.ContributionTypeId), "contributionTypeId", "ContributionTypeId is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(objective.DependencyTypeId), "dependencyTypeId", "DependencyTypeId is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(unitOfMeasureId), "unitOfMeasureId", "UnitOfMeasureId is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(unitOfMeasureId), "unitOfMeasure", "Unit of measure is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(direction), "performanceDirection", "PerformanceDirection is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(direction), "directionOfPerformance", "Direction is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(reportingFrequencyId), "reportingFrequencyId", "ReportingFrequencyId is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(reportingFrequencyId), "reportingFrequency", "Reporting frequency is required.");

            AddIf(errors, string.IsNullOrWhiteSpace(objective.ApprovalRouteType), "approvalRouteType", "ApprovalRouteType is required.");
            var isIndividualApproval = string.Equals(objective.ApprovalRouteType, "IndividualApprover", StringComparison.OrdinalIgnoreCase);
            var isGroupApproval = string.Equals(objective.ApprovalRouteType, "ApprovalGroup", StringComparison.OrdinalIgnoreCase);
            AddIf(errors, !isIndividualApproval && !isGroupApproval, "approvalRouteType", "ApprovalRouteType must be IndividualApprover or ApprovalGroup.");
            if (isIndividualApproval)
                AddIf(errors, string.IsNullOrWhiteSpace(objective.ApproverId), "approverId", "ApproverId is required for Individual Approver route.");
            if (isGroupApproval)
                AddIf(errors, string.IsNullOrWhiteSpace(objective.ApprovalGroupId), "approvalGroupId", "ApprovalGroupId is required for Approval Group route.");

            var contributionRequiresWeight = string.Equals(objective.ContributionTypeId, "Weighted", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(objective.ContributionTypeId, "WeightedContribution", StringComparison.OrdinalIgnoreCase);
            if (contributionRequiresWeight)
                AddIf(errors, objective.ContributionWeight <= 0, "contributionWeight", "Contribution weight is required for weighted contribution types.");

            if (!string.IsNullOrWhiteSpace(objective.ReviewCadenceId))
            {
                AddIf(errors, string.IsNullOrWhiteSpace(objective.ReviewOwnerId), "reviewOwnerId", "ReviewOwnerId is required when ReviewCadenceId is set.");
                AddIf(errors, objective.NextReviewDate is null, "nextReviewDate", "NextReviewDate is required when ReviewCadenceId is set.");
            }
            if (objective.NextReviewDate.HasValue)
            {
                var anchor = objective.EffectiveDate ?? objective.StartDate ?? objective.TimeHorizonStart;
                if (anchor.HasValue)
                    AddIf(errors, objective.NextReviewDate.Value.Date < anchor.Value.Date, "nextReviewDate", "NextReviewDate must be on/after the effective/start date.");
            }
        }

        var metricRows = objective.Metrics ?? new List<ObjectiveMetricDto>();
        var horizonStartYear = objective.TimeHorizonStart?.Year;
        var horizonEndYear = objective.TimeHorizonEnd?.Year;
        var expectedYearCount = horizonStartYear.HasValue && horizonEndYear.HasValue && horizonEndYear.Value >= horizonStartYear.Value
            ? (horizonEndYear.Value - horizonStartYear.Value + 1)
            : 0;
        var expectedPeriods = ObjectiveTargetPlanPeriodHelper.BuildPeriods(objective.TimeHorizonStart, objective.TimeHorizonEnd, targetPlanGranularity);
        var expectedPeriodLookup = expectedPeriods.ToDictionary(x => x.PeriodKey, x => x, StringComparer.OrdinalIgnoreCase);
        AddIf(errors, metricRows.Count == 0, "metrics", "At least one objective metric assignment is required.");
        if (!objective.AllowMultipleTargetMetrics && !string.IsNullOrWhiteSpace(primaryMetricId))
        {
            AddIf(errors, metricRows.Any(x => !string.IsNullOrWhiteSpace(x.MetricId) && !string.Equals(x.MetricId, primaryMetricId, StringComparison.OrdinalIgnoreCase)), "metrics.metricId", "MetricId must match PrimaryMetricId when multi-metric tracking is disabled.");
            AddIf(errors, metricRows.Any(x => !string.IsNullOrWhiteSpace(x.UnitOfMeasureId) && !string.Equals(x.UnitOfMeasureId, unitOfMeasureId, StringComparison.OrdinalIgnoreCase)), "metrics.unitOfMeasureId", "UnitOfMeasureId must match objective UnitOfMeasureId when multi-metric tracking is disabled.");
        }

        var thresholdModelRequiresValue = ThresholdModelRequiresValue(objective.ThresholdModelId);
        for (var i = 0; i < metricRows.Count; i++)
        {
            var row = metricRows[i];
            var p = $"metrics[{i}]";
            var rowMetricId = string.IsNullOrWhiteSpace(row.MetricId)
                ? (string.IsNullOrWhiteSpace(row.MetricDefId) ? row.MetricName : row.MetricDefId)
                : row.MetricId;
            AddIf(errors, string.IsNullOrWhiteSpace(rowMetricId), $"{p}.metricId", "MetricId is required.");

            if (!isDraft)
            {
                var rowDirection = string.IsNullOrWhiteSpace(row.Direction) ? direction : row.Direction;
                var rowAggregation = string.IsNullOrWhiteSpace(row.AggregationMethodId) ? row.AggregationMethod : row.AggregationMethodId;
                var rowUnitOfMeasureId = string.IsNullOrWhiteSpace(row.UnitOfMeasureId) ? unitOfMeasureId : row.UnitOfMeasureId;
                AddIf(errors, string.IsNullOrWhiteSpace(rowDirection), $"{p}.direction", "Direction is required.");
                AddIf(errors, string.IsNullOrWhiteSpace(rowAggregation), $"{p}.aggregationMethodId", "AggregationMethodId is required.");
                AddIf(errors, string.IsNullOrWhiteSpace(rowUnitOfMeasureId), $"{p}.unitOfMeasureId", "UnitOfMeasureId is required.");
                var rowThresholdRequired = thresholdModelRequiresValue && (objective.AllowRowThresholdOverrides || i == 0);
                AddIf(errors, rowThresholdRequired && string.IsNullOrWhiteSpace(row.ThresholdTolerance) && row.ThresholdValue is null, $"{p}.threshold", "Threshold/Tolerance is required for the selected threshold model.");
            }

            var metricClass = (row.MetricClass ?? string.Empty).Trim();
            var metricRole = (row.MetricRole ?? string.Empty).Trim();
            var yearlyValues = row.YearlyValues ?? new List<ObjectiveMetricYearValueDto>();
            var normalizedPeriodRows = yearlyValues.Select(yearRow =>
            {
                yearRow.PeriodGranularity = ObjectiveTargetPlanPeriodHelper.NormalizeGranularity(string.IsNullOrWhiteSpace(yearRow.PeriodGranularity) ? targetPlanGranularity : yearRow.PeriodGranularity);
                if (string.IsNullOrWhiteSpace(yearRow.PeriodKey) && yearRow.Year > 0)
                    yearRow.PeriodKey = yearRow.Year.ToString();
                if (string.IsNullOrWhiteSpace(yearRow.PeriodLabel) && yearRow.Year > 0)
                    yearRow.PeriodLabel = yearRow.Year.ToString();
                if (!yearRow.PeriodStart.HasValue && yearRow.Year > 0 && targetPlanGranularity == ObjectiveTargetPlanPeriodHelper.GranularityYearly)
                    yearRow.PeriodStart = new DateTime(yearRow.Year, 1, 1);
                if (!yearRow.PeriodEnd.HasValue && yearRow.Year > 0 && targetPlanGranularity == ObjectiveTargetPlanPeriodHelper.GranularityYearly)
                    yearRow.PeriodEnd = new DateTime(yearRow.Year, 12, 31);
                return yearRow;
            }).ToList();
            var duplicatePeriodKeys = normalizedPeriodRows
                .GroupBy(x => string.IsNullOrWhiteSpace(x.PeriodKey) ? x.Year.ToString() : x.PeriodKey, StringComparer.OrdinalIgnoreCase)
                .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            AddIf(errors, duplicatePeriodKeys.Count > 0, $"{p}.yearlyValues.periodKey", "Duplicate target-plan periods are not allowed.");

            if (expectedPeriods.Count > 0)
            {
                if (!isDraft)
                    AddIf(errors, normalizedPeriodRows.Count != expectedPeriods.Count, $"{p}.yearlyValues", "Target-plan rows must contain exactly one row per objective planning period.");
                foreach (var yearRow in normalizedPeriodRows)
                {
                    var rowKey = string.IsNullOrWhiteSpace(yearRow.PeriodKey) ? yearRow.Year.ToString() : yearRow.PeriodKey;
                    AddIf(errors, !expectedPeriodLookup.ContainsKey(rowKey), $"{p}.yearlyValues.periodKey", $"Target-plan row {rowKey} is outside the objective planning horizon.");
                    if (yearRow.PeriodStart.HasValue && objective.TimeHorizonStart.HasValue)
                        AddIf(errors, yearRow.PeriodStart.Value.Date < objective.TimeHorizonStart.Value.Date, $"{p}.yearlyValues.periodStart", $"Target-plan period {rowKey} starts before the objective horizon.");
                    if (yearRow.PeriodEnd.HasValue && objective.TimeHorizonEnd.HasValue)
                        AddIf(errors, yearRow.PeriodEnd.Value.Date > objective.TimeHorizonEnd.Value.Date, $"{p}.yearlyValues.periodEnd", $"Target-plan period {rowKey} ends after the objective horizon.");
                    if (!isDraft)
                        AddIf(errors, yearRow.TargetValue is null, $"{p}.yearlyValues.targetValue", $"Target value is required for planning period {rowKey}.");
                }
            }

            if (string.Equals(metricClass, "Inherited", StringComparison.OrdinalIgnoreCase))
            {
                AddIf(errors, string.IsNullOrWhiteSpace(row.ParentMetricAssignmentId), $"{p}.parentMetricAssignmentId", "Inherited metrics must preserve parent metric lineage.");
                AddIf(errors, string.IsNullOrWhiteSpace(row.ParentMetricAssignmentId), "metricAssignments[0].parentMetricAssignmentId", "Inherited metrics must preserve parent metric lineage.");
                if (!isDraft)
                    AddIf(errors, !string.Equals(metricRole, "Contribution", StringComparison.OrdinalIgnoreCase), $"{p}.metricRole", "Inherited metrics must use CONTRIBUTION metric role.");
            }
            else if (!isDraft)
            {
                AddIf(errors, !string.IsNullOrWhiteSpace(row.ParentMetricAssignmentId), $"{p}.parentMetricAssignmentId", "Local metrics cannot set parent metric assignment id.");
                var validLocalRole = string.Equals(metricRole, "Local", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(metricRole, "Benefit", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(metricRole, "Delivery", StringComparison.OrdinalIgnoreCase);
                AddIf(errors, !validLocalRole, $"{p}.metricRole", "Local metric role must be LOCAL, BENEFIT, or DELIVERY.");
                AddIf(errors, row.RollupEligibleFlag, $"{p}.rollupEligibleFlag", "Local metrics cannot roll up without approved mapping.");
            }
        }

        if (!isDraft)
            AddIf(errors, metricRows.Any(x => (x.YearlyValues ?? new List<ObjectiveMetricYearValueDto>()).Count == 0), "metricAssignments[0].yearlyValues", "Target-plan rows are required for metric assignments.");

        var budgetRows = objective.YearlyBudgets ?? new List<ObjectiveYearlyBudgetDto>();
        var budgetDuplicateYears = budgetRows.GroupBy(x => x.Year).Where(g => g.Key > 0 && g.Count() > 1).Select(g => g.Key).ToList();
        AddIf(errors, budgetDuplicateYears.Count > 0, "yearlyBudgets.year", "Duplicate years are not allowed in yearly budgets.");
        AddIf(errors, budgetDuplicateYears.Count > 0, "budgetYearlyValues", "Duplicate years are not allowed in yearly budgets.");
        if (!isDraft)
        {
            AddIf(errors, budgetRows.Count != expectedYearCount, "yearlyBudgets", "Objective yearly budget rows must match objective horizon.");
            AddIf(errors, budgetRows.Count != expectedYearCount, "budgetYearlyValues", "Objective yearly budget rows must match objective horizon.");
        }
        if (horizonStartYear.HasValue && horizonEndYear.HasValue)
        {
            foreach (var row in budgetRows)
                AddIf(errors, row.Year < horizonStartYear.Value || row.Year > horizonEndYear.Value, "yearlyBudgets.year", $"Budget year {row.Year} is outside objective horizon {horizonStartYear.Value}-{horizonEndYear.Value}.");
        }

        var dependencyRows = objective.DependencyLinks ?? new List<ObjectiveDependencyLinkDto>();
        for (var i = 0; i < dependencyRows.Count; i++)
        {
            var dep = dependencyRows[i];
            var p = $"dependencyLinks[{i}]";
            AddIf(errors, string.IsNullOrWhiteSpace(dep.DependencyTypeId), $"{p}.dependencyTypeId", "DependencyTypeId is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(dep.DependencyObjectType), $"{p}.dependencyObjectType", "DependencyObjectType is required.");
            AddIf(errors, string.IsNullOrWhiteSpace(dep.DependencyReferenceId) && string.IsNullOrWhiteSpace(dep.DependencyReferenceText), $"{p}.dependencyReference", "DependencyReferenceId or DependencyReferenceText is required.");
        }
        return errors;
    }

    public static Dictionary<string, List<string>> ValidateConnection(StrategyConnectionDto connection, bool allowDirectGoalLinks)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var isSelf = string.Equals(connection.FromType, connection.ToType, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(connection.FromId, connection.ToId, StringComparison.OrdinalIgnoreCase);
        AddIf(errors, isSelf, "selfReference", "Self references are not allowed.");
        AddIf(errors, connection.ContributionWeight < 0 || connection.ContributionWeight > 100, "contributionWeight", "Contribution weight must be between 0 and 100.");
        if (string.Equals(connection.CompanyScopeMode, "Explicit", StringComparison.OrdinalIgnoreCase))
            AddIf(errors, string.IsNullOrWhiteSpace(connection.CompanyId), "companyId", "Company is required when Company Scope Mode is Explicit.");

        var from = connection.FromType.Trim();
        var to = connection.ToType.Trim();
        var directGoalAllowed = allowDirectGoalLinks && string.Equals(from, "Goal", StringComparison.OrdinalIgnoreCase) &&
                                (string.Equals(to, "Initiative", StringComparison.OrdinalIgnoreCase) || string.Equals(to, "Project", StringComparison.OrdinalIgnoreCase));
        var validCore =
            (from, to) switch
            {
                ("Goal", "Objective") => true,
                ("Objective", "Initiative") => true,
                ("Initiative", "Project") => true,
                _ => false
            };

        AddIf(errors, !validCore && !directGoalAllowed, "lineage", $"Invalid lineage from {from} to {to}.");
        return errors;
    }

    public static bool IsActive(string status)
        => string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);

    public static bool IsArchived(string status)
        => string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase);

    public static bool IsCircularEdge(StrategyConnectionAggregate edge, IReadOnlyList<StrategyConnectionAggregate> all)
    {
        var adjacency = all
            .GroupBy(x => x.FromId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(y => y.ToId).ToList(), StringComparer.OrdinalIgnoreCase);

        if (!adjacency.ContainsKey(edge.FromId))
            adjacency[edge.FromId] = new List<string>();

        adjacency[edge.FromId].Add(edge.ToId);
        return HasPath(edge.ToId, edge.FromId, adjacency, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
    }

    private static bool HasPath(string current, string target, Dictionary<string, List<string>> adjacency, HashSet<string> visited)
    {
        if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!visited.Add(current))
            return false;

        if (!adjacency.TryGetValue(current, out var neighbors))
            return false;

        return neighbors.Any(next => HasPath(next, target, adjacency, visited));
    }

    private static void AddIf(IDictionary<string, List<string>> errors, bool condition, string key, string message)
    {
        if (!condition)
            return;

        if (!errors.TryGetValue(key, out var values))
        {
            values = new List<string>();
            errors[key] = values;
        }

        values.Add(message);
    }

    private static bool ThresholdModelRequiresValue(string? thresholdModelId)
    {
        var value = (thresholdModelId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Contains("RAG", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Green / Amber / Red", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Target Range", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Minimum Threshold", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Maximum Threshold", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Tolerance", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Band", StringComparison.OrdinalIgnoreCase);
    }
}