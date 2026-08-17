using Diten.Web.Models.EnterpriseStrategy;

namespace Diten.Web.Services.EnterpriseStrategy;

public sealed class MockEnterpriseStrategyFrontendAdapter : IEnterpriseStrategyFrontendAdapter
{
    public IReadOnlyList<Goal> UseGoals() =>
        NormalizeById(
        new[]
        {
            new Goal
            {
                Id = "goal-001",
                Name = "Improve operating margin",
                Category = "Financial",
                Statement = "Improve operating margin by optimization.",
                Owner = "Strategy Office",
                Status = "Active",
                Priority = "High",
                EntityScope = "Enterprise",
                ScopeMode = "Enterprise",
                Version = 2,
                DecisionReference = "DEC-2301",
                EvidenceReference = "EVD-812",
                Metrics = new[] { new GoalMetric { Id = "gm-001", Name = "Margin %", Unit = "%", CurrentValue = 11.2m, TargetValue = 14m } }
            },
            new Goal
            {
                Id = "goal-002",
                Name = "Increase retention in enterprise segment",
                Category = "Customer",
                Statement = "Reduce logo churn in enterprise accounts.",
                Owner = "Commercial",
                Status = "Draft",
                Priority = "Medium",
                EntityScope = "Enterprise",
                ScopeMode = "MultiCompany",
                ApplicableCompanyIds = new[] { "cmp-001", "cmp-002" },
                Version = 1,
                Metrics = new[] { new GoalMetric { Id = "gm-002", Name = "Retention", Unit = "%", CurrentValue = 89m, TargetValue = 93m } }
            }
        },
        x => x.Id);

    public IReadOnlyList<Objective> UseObjectives() =>
        NormalizeById(
        new[]
        {
            new Objective
            {
                Id = "obj-001",
                ParentGoalId = "goal-001",
                Name = "Reduce quote-to-cash lead time",
                Statement = "Simplify approval and contracting workflow.",
                Owner = "COO",
                Status = "Active",
                Type = "Outcome",
                Priority = "High",
                ContributionType = "Direct",
                ContributionWeight = 40,
                EntityScope = "Enterprise",
                InheritCompanyScope = true,
                Version = 1,
                DecisionReference = "DEC-2338",
                EvidenceReference = "EVD-477",
                Metrics = new[] { new ObjectiveMetric { Id = "om-001", Name = "Cycle Time", Unit = "days", CurrentValue = 12m, TargetValue = 8m } }
            }
        },
        x => x.Id);

    public IReadOnlyList<StrategyConnection> UseConnections() =>
        NormalizeById(
        new[]
        {
            new StrategyConnection
            {
                Id = "conn-001",
                FromType = "Goal",
                FromId = "goal-001",
                ToType = "Objective",
                ToId = "obj-001",
                RelationshipType = "Supports",
                ContributionType = "Direct",
                ContributionWeight = 40,
                CompanyScopeMode = "Explicit",
                CompanyId = "cmp-001",
                Status = "Active",
                Version = 1,
                DecisionReferencesJson = "[\"DEC-2338\"]",
                EvidenceReferencesJson = "[\"EVD-477\",\"EVD-478\"]"
            }
        },
        x => x.Id);

    public IReadOnlyList<InitiativeStrategyLinkView> UseInitiativeLinks() =>
        NormalizeById(
        new[]
        {
            new InitiativeStrategyLinkView
            {
                LinkId = "lnk-init-001",
                InitiativeId = "I-145",
                InitiativeName = "Customer Data Platform",
                SourceSystem = "PPM",
                SourceRecordId = "PPM-I-145",
                LinkStatus = "Linked",
                TraceabilityStatus = "Under Review"
            }
        },
        x => x.LinkId);

    public IReadOnlyList<ProjectStrategyLinkView> UseProjectLinks() =>
        NormalizeById(
        new[]
        {
            new ProjectStrategyLinkView
            {
                LinkId = "lnk-prj-004",
                ProjectId = "P-004",
                ProjectName = "ERP Migration Wave 2",
                SourceSystem = "PPM",
                SourceRecordId = "PPM-P-004",
                LinkStatus = "Linked",
                TraceabilityStatus = "Blocked"
            }
        },
        x => x.LinkId);

    public IReadOnlyList<StrategyMetricSummaryCard> UseMetricCards() =>
        new[]
        {
            new StrategyMetricSummaryCard { Label = "Active Goals", Value = "8", Trend = "+1 this quarter" },
            new StrategyMetricSummaryCard { Label = "Objectives Off Track", Value = "3", Trend = "-2 vs last month" },
            new StrategyMetricSummaryCard { Label = "Connections Needs Review", Value = "5", Trend = "stable" }
        };

    private static IReadOnlyList<T> NormalizeById<T>(IEnumerable<T> source, Func<T, string> idSelector) =>
        source
            .Where(x => !string.IsNullOrWhiteSpace(idSelector(x)))
            .GroupBy(idSelector, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
}
