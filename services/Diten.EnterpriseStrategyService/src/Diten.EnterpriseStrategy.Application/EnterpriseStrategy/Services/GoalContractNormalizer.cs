using Diten.Application.Dtos.EnterpriseStrategy;

namespace Diten.Application.EnterpriseStrategy.Services;

/// <summary>
/// Aligns inbound Goal API payloads to the canonical Strategic Goal contract,
/// while still accepting temporary legacy aliases.
/// </summary>
public static class GoalContractNormalizer
{
    public static void Normalize(GoalDto goal)
    {
        ApplyPlanningHorizon(goal);
        ApplyCompanyScopeMetadata(goal);
        ApplyBudgetAliases(goal);
        ApplyMetricYearlyAliases(goal);
    }

    public static void ApplyPlanningHorizon(GoalDto goal)
    {
        if (goal.StartDate.HasValue)
            goal.StartDate = DateTime.SpecifyKind(goal.StartDate.Value.Date, DateTimeKind.Utc);
        if (goal.EndDate.HasValue)
            goal.EndDate = DateTime.SpecifyKind(goal.EndDate.Value.Date, DateTimeKind.Utc);
    }

    public static void ApplyMetricYearlyAliases(GoalDto goal)
    {
        foreach (var m in goal.Metrics ?? new List<GoalMetricDto>())
        {
            if (m.LegacyYearlyTargets is { Count: > 0 } && (m.YearlyValues is null || m.YearlyValues.Count == 0))
                m.YearlyValues = m.LegacyYearlyTargets;
            foreach (var row in m.YearlyValues ?? new())
            {
                if (string.IsNullOrWhiteSpace(row.Commentary) && !string.IsNullOrWhiteSpace(row.ThresholdCommentary))
                    row.Commentary = row.ThresholdCommentary;
                if (string.IsNullOrWhiteSpace(row.ThresholdCommentary) && !string.IsNullOrWhiteSpace(row.Commentary))
                    row.ThresholdCommentary = row.Commentary;
            }
            m.LegacyYearlyTargets = null;
            m.MetricRole = "Strategic";
            if (string.IsNullOrWhiteSpace(m.MetricOrigin))
                m.MetricOrigin = "Local";
            if (string.IsNullOrWhiteSpace(m.DirectionPolarity))
                m.DirectionPolarity = "Increase";
            if (string.IsNullOrWhiteSpace(m.ThresholdModel))
                m.ThresholdModel = "None";
            if (string.IsNullOrWhiteSpace(m.ReportingFrequency))
                m.ReportingFrequency = "Quarterly";
        }
    }

    private static void ApplyBudgetAliases(GoalDto goal)
    {
        foreach (var b in goal.BudgetEnvelopes ?? new List<GoalYearlyBudgetEnvelopeDto>())
        {
            if (b.FundingPool is null && b.FundingPoolEnvelope is not null)
                b.FundingPool = b.FundingPoolEnvelope;
        }
    }

    private static void ApplyCompanyScopeMetadata(GoalDto goal)
    {
        var scope = NormalizeScopeMode(string.IsNullOrWhiteSpace(goal.ApplicabilityMode) ? goal.ScopeMode : goal.ApplicabilityMode);
        var applicable = goal.ApplicableCompanyIds ?? new List<string>();
        goal.ApplicabilityMode = scope;
        goal.AppliesToAllCompanies = goal.AppliesToAllCompanies || goal.AppliesToAllCompaniesFlag || scope.Equals("Enterprise", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(goal.OwnerRole))
            goal.OwnerRole = string.IsNullOrWhiteSpace(goal.OwnerId) ? goal.Owner : goal.OwnerId;
        if (string.IsNullOrWhiteSpace(goal.OwnerCompanyId))
            goal.OwnerCompanyId = !string.IsNullOrWhiteSpace(goal.PrimaryCompanyId)
                ? goal.PrimaryCompanyId
                : (applicable.FirstOrDefault() ?? string.Empty);
    }

    private static string NormalizeScopeMode(string? code)
    {
        var value = (code ?? string.Empty).Trim();
        if (value.Equals("SINGLE_COMPANY", StringComparison.OrdinalIgnoreCase)) return "SingleCompany";
        if (value.Equals("MULTI_COMPANY", StringComparison.OrdinalIgnoreCase)) return "MultiCompany";
        if (value.Equals("ENTERPRISE", StringComparison.OrdinalIgnoreCase)) return "Enterprise";
        if (value.Equals("AppliesToSelectedCompanies", StringComparison.OrdinalIgnoreCase)) return "MultiCompany";
        return value;
    }

    public static IReadOnlyList<CreateGoalMetricYearDto> CoalesceCreateMetricYears(CreateGoalMetricDto m)
    {
        if (m.YearlyValues is { Count: > 0 })
            return m.YearlyValues;
        if (m.LegacyYearlyTargets is { Count: > 0 })
            return m.LegacyYearlyTargets;
        return Array.Empty<CreateGoalMetricYearDto>();
    }
}
