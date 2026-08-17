using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Adapters.Ppm;
using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Domain.Aggregates.EnterpriseStrategy;
using System.Linq;
using System.Security.Claims;

namespace Diten.EnterpriseStrategy.EndToEnd.Tests;

public sealed class EnterpriseStrategyLineageE2ETests
{
    private const string ActiveStrategyPeriodId = "sp-active";
    private const string ActiveCompanyId = "cmp-001";
    private const string StrategicThemeId = "theme-growth";

    [Fact]
    public async Task Goal_Objective_Initiative_Project_Lineage_EndToEnd_Works()
    {
        var ctx = new InMemoryContext();
        var services = ctx.Build();

        var goal = await services.Goals.CreateAsync(new GoalDto
        {
            Id = "goal-e2e-1",
            Name = "Operational Growth",
            Category = "Growth",
            OwnerId = "usr-coo",
            Owner = "Chief Operating Officer",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Draft",
            Priority = "Medium",
            Statement = "Improve enterprise performance over planning horizon.",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2031, 12, 31),
            ScopeMode = "Enterprise",
            Metrics = new()
            {
                new GoalMetricDto
                {
                    Id = "gm-1",
                    MetricName = "Revenue",
                    MetricType = "%",
                    BaselineValue = 10,
                    TargetValue = 20,
                    UnitOfMeasure = "Percentage",
                    AggregationMethod = "Sum",
                    MetricOrigin = "Local",
                    MetricRole = "Strategic",
                    RestrictionMode = "GoalGovernedStructure",
                    SortOrder = 1,
                    MetricBindingStatus = "Bound",
                    YearlyValues = Enumerable.Range(2027, 5).Select(y => new GoalMetricYearValueDto { Year = y, TargetValue = 10 + (y - 2027) }).ToList()
                },
                new GoalMetricDto
                {
                    Id = "gm-2",
                    MetricName = "Retention",
                    MetricType = "%",
                    BaselineValue = 70,
                    TargetValue = 80,
                    UnitOfMeasure = "Percentage",
                    AggregationMethod = "Sum",
                    MetricOrigin = "Local",
                    MetricRole = "Strategic",
                    RestrictionMode = "GoalGovernedStructure",
                    SortOrder = 2,
                    MetricBindingStatus = "Bound",
                    YearlyValues = Enumerable.Range(2027, 5).Select(y => new GoalMetricYearValueDto { Year = y, TargetValue = 70 + (y - 2027) }).ToList()
                }
            }
        }, "tester", "corr");
        Assert.True(goal.Success);

        var objective = await services.Objectives.CreateAsync(
            BuildValidObjective("obj-e2e-1", "goal-e2e-1", "Improve adoption", 2027, 2031),
            "tester",
            "corr");
        Assert.True(objective.Success);

        var connect = await services.Connections.CreateAsync(new StrategyConnectionDto
        {
            Id = "conn-e2e-1",
            FromType = "Goal",
            FromId = "goal-e2e-1",
            ToType = "Objective",
            ToId = "obj-e2e-1",
            RelationshipType = "Supports",
            ContributionType = "Supports",
            Status = "Active"
        }, "tester", "corr");
        Assert.True(connect.Success);

        var initiative = await services.Initiatives.UpsertStrategyLinkAsync("init-001", new InitiativeStrategyLinkViewDto
        {
            InitiativeId = "init-001",
            ParentObjectiveId = "obj-e2e-1",
            StrategyLinkStatus = "Linked",
            ContributionType = "Direct",
            ContributionWeight = 50,
            SponsoringCompanyId = "cmp-001"
        }, 0, "tester", "corr");
        Assert.True(initiative.Success);

        var project = await services.Projects.UpsertStrategyLinkAsync("prj-001", new ProjectStrategyLinkViewDto
        {
            ProjectId = "prj-001",
            ParentInitiativeId = "init-001",
            StrategyLinkStatus = "Linked",
            ContributionNote = "Delivery linkage",
            DeliveryCompanyId = "cmp-001"
        }, 0, "tester", "corr");
        Assert.True(project.Success);

        var lineage = await services.Projects.UpstreamLineageAsync("prj-001");
        Assert.True(lineage.Success);
        Assert.Contains("goal-e2e-1", lineage.Data!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("obj-e2e-1", lineage.Data!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("init-001", lineage.Data!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Circular_Connection_Is_Rejected()
    {
        var ctx = new InMemoryContext();
        var services = ctx.Build();

        await services.Goals.CreateAsync(new GoalDto
        {
            Id = "g1",
            Name = "G1",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Draft",
            Priority = "Medium",
            Statement = "Goal 1 statement",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        }, "tester", "corr");
        var objective = await services.Objectives.CreateAsync(
            BuildValidObjective("o1", "g1", "O1", 2027, 2028),
            "tester",
            "corr");
        Assert.True(objective.Success);
        await services.Connections.CreateAsync(new StrategyConnectionDto { Id = "edge-1", FromType = "Goal", FromId = "g1", ToType = "Objective", ToId = "o1", RelationshipType = "Supports", ContributionType = "Supports" }, "tester", "corr");

        var invalid = await services.Connections.CreateAsync(new StrategyConnectionDto
        {
            Id = "edge-2",
            FromType = "Objective",
            FromId = "o1",
            ToType = "Goal",
            ToId = "g1",
            RelationshipType = "Supports",
            ContributionType = "Supports"
        }, "tester", "corr");

        Assert.False(invalid.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.ValidationError, invalid.Error?.Code);
    }

    [Fact]
    public async Task Stale_Write_And_Missing_Permission_Are_Handled()
    {
        var ctx = new InMemoryContext();
        var services = ctx.Build();

        await services.Goals.CreateAsync(new GoalDto
        {
            Id = "g2",
            Name = "G2",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Draft",
            Priority = "Medium",
            Statement = "Goal 2 statement",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        }, "tester", "corr");
        var objective = await services.Objectives.CreateAsync(
            BuildValidObjective("o2", "g2", "O2", 2027, 2028),
            "tester",
            "corr");
        Assert.True(objective.Success);
        var link = await services.Initiatives.UpsertStrategyLinkAsync("init-001", new InitiativeStrategyLinkViewDto
        {
            InitiativeId = "init-001",
            ParentObjectiveId = "o2",
            StrategyLinkStatus = "Linked",
            SponsoringCompanyId = "cmp-001"
        }, 0, "tester", "corr");
        Assert.True(link.Success);

        var stale = await services.Initiatives.UpsertStrategyLinkAsync("init-001", new InitiativeStrategyLinkViewDto
        {
            InitiativeId = "init-001",
            ParentObjectiveId = "o2",
            StrategyLinkStatus = "Linked",
            Version = 1,
            SponsoringCompanyId = "cmp-001"
        }, 99, "tester", "corr");
        Assert.False(stale.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.StaleVersion, stale.Error?.Code);

        var auth = new DefaultEnterpriseStrategyAuthorizationService();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var allowed = await auth.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, principal);
        Assert.False(allowed);
    }

    private static ObjectiveDto BuildValidObjective(string id, string parentGoalId, string name, int startYear, int endYear)
    {
        var yearlyValues = Enumerable.Range(startYear, endYear - startYear + 1)
            .Select(y => new ObjectiveMetricYearValueDto
            {
                Year = y,
                TargetValue = 10 + (y - startYear)
            })
            .ToList();

        var yearlyBudgets = Enumerable.Range(startYear, endYear - startYear + 1)
            .Select(y => new ObjectiveYearlyBudgetDto
            {
                Year = y
            })
            .ToList();

        return new ObjectiveDto
        {
            Id = id,
            ParentGoalId = parentGoalId,
            Name = name,
            Statement = $"{name} statement",
            StrategicThemeId = StrategicThemeId,
            StrategyPeriodId = ActiveStrategyPeriodId,
            OwnerCompanyId = ActiveCompanyId,
            OwnerPositionId = "pos-owner",
            CurrentOwnerPersonId = "usr-owner",
            Owner = "usr-owner",
            ExecutiveSponsor = "usr-sponsor",
            ApproverId = "usr-approver",
            Type = "Growth",
            Status = "Draft",
            ApprovalStatus = "Draft",
            Priority = "High",
            ContributionType = "Direct",
            ContributionWeight = 40,
            DependencyType = "Predecessor",
            PrimaryKpiMetric = "CustomerRetentionRate",
            UnitOfMeasure = "Percentage",
            DirectionOfPerformance = "Increase",
            ReportingFrequency = "Quarterly",
            ThresholdModel = "None",
            TimeHorizonStart = new DateTime(startYear, 1, 1),
            TimeHorizonEnd = new DateTime(endYear, 12, 31),
            InheritCompanyScope = false,
            PrimaryCompanyId = ActiveCompanyId,
            ReviewOwner = "usr-review",
            NextReviewDate = new DateTime(startYear, 6, 30),
            Metrics = new()
            {
                new ObjectiveMetricDto
                {
                    Id = $"{id}-metric-1",
                    MetricName = "CustomerRetentionRate",
                    Direction = "Increase",
                    AggregationMethod = "Average",
                    UnitOfMeasure = "Percentage",
                    MetricClass = "Local",
                    MetricRole = "Local",
                    YearlyValues = yearlyValues
                }
            },
            YearlyBudgets = yearlyBudgets
        };
    }

    [Fact]
    public async Task Dependency_Unavailable_Triggers_Degraded_Failure()
    {
        var ctx = new InMemoryContext(failInitiativeSync: true, failProjectSync: true);
        var services = ctx.Build();

        var syncInitiative = await services.Initiatives.SyncAsync("corr-i", "tester");
        var syncProject = await services.Projects.SyncAsync("corr-p", "tester");

        Assert.False(syncInitiative.Success);
        Assert.False(syncProject.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.DependencyUnavailable, syncInitiative.Error?.Code);
        Assert.Equal(EnterpriseStrategyErrorCodes.DependencyUnavailable, syncProject.Error?.Code);
    }
}

file sealed class InMemoryContext
{
    private const string ActiveStrategyPeriodId = "sp-active";
    private const string ActiveCompanyId = "cmp-001";
    private readonly bool _failInitiativeSync;
    private readonly bool _failProjectSync;

    public InMemoryContext(bool failInitiativeSync = false, bool failProjectSync = false)
    {
        _failInitiativeSync = failInitiativeSync;
        _failProjectSync = failProjectSync;
    }

    public (GoalService Goals, ObjectiveService Objectives, ConnectionService Connections, InitiativeOrchestrationService Initiatives, ProjectOrchestrationService Projects) Build()
    {
        var goals = new InMemoryGoalRepository();
        var objectives = new InMemoryObjectiveRepository();
        var connections = new InMemoryConnectionRepository();
        var initiativeLinks = new InMemoryInitiativeLinkRepository();
        var projectLinks = new InMemoryProjectLinkRepository();
        var strategyPeriods = new InMemoryStrategyPeriodRepository(new StrategyPeriodAggregate
        {
            Id = ActiveStrategyPeriodId,
            PlanningCycleId = "pc-1",
            Code = "SP-ACT",
            Name = "Active Strategy Period",
            CompanyId = ActiveCompanyId,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2035, 12, 31),
            Status = "Active",
            OwnerEmployeeId = "usr-ceo",
            ReviewCadence = "Quarterly"
        });
        var initiativeCache = new InMemoryPpmInitiativeCacheRepository();
        var projectCache = new InMemoryPpmProjectCacheRepository();
        IPpmInitiativeReadAdapter initiativeAdapter = _failInitiativeSync ? new ThrowingInitiativeAdapter() : new MockPpmInitiativeReadAdapter();
        IPpmProjectReadAdapter projectAdapter = _failProjectSync ? new ThrowingProjectAdapter() : new MockPpmProjectReadAdapter();
        var audit = new NoOpEnterpriseStrategyAuditSink();

        var goalService = new GoalService(goals, objectives, initiativeLinks, projectLinks, strategyPeriods, audit);
        var objectiveService = new ObjectiveService(objectives, goals, strategyPeriods, initiativeLinks, projectLinks, audit);
        var connectionService = new ConnectionService(connections, goals, objectives, audit);
        var initiativeService = new InitiativeOrchestrationService(initiativeAdapter, initiativeCache, initiativeLinks, projectLinks, objectives, goals, audit);
        var projectService = new ProjectOrchestrationService(projectAdapter, projectCache, projectLinks, initiativeLinks, objectives, goals, new InMemoryStrategyLibraryRepository(), audit);

        return (goalService, objectiveService, connectionService, initiativeService, projectService);
    }
}

file sealed class InMemoryGoalRepository : IGoalRepository
{
    private readonly List<GoalAggregate> _rows = new();
    public Task<GoalAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => x.Id == id));
    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_rows.Any(x => x.Id == id));
    public Task<IReadOnlyList<GoalAggregate>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GoalAggregate>>(_rows.ToList());
    public Task AddAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default) { _rows.Add(aggregate); return Task.CompletedTask; }
    public Task UpdateAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var idx = _rows.FindIndex(x => x.Id == aggregate.Id);
        if (idx >= 0) _rows[idx] = aggregate;
        return Task.CompletedTask;
    }
}

file sealed class InMemoryObjectiveRepository : IObjectiveRepository
{
    private readonly List<ObjectiveAggregate> _rows = new();
    public Task<ObjectiveAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => x.Id == id));
    public Task<IReadOnlyList<ObjectiveAggregate>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ObjectiveAggregate>>(_rows.ToList());
    public Task AddAsync(ObjectiveAggregate aggregate, CancellationToken cancellationToken = default) { _rows.Add(aggregate); return Task.CompletedTask; }
    public Task UpdateAsync(ObjectiveAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var idx = _rows.FindIndex(x => x.Id == aggregate.Id);
        if (idx >= 0) _rows[idx] = aggregate;
        return Task.CompletedTask;
    }
}

file sealed class InMemoryStrategyPeriodRepository : IStrategyPeriodRepository
{
    private readonly List<StrategyPeriodAggregate> _rows = new();
    public InMemoryStrategyPeriodRepository(params StrategyPeriodAggregate[] rows) => _rows.AddRange(rows);
    public Task<StrategyPeriodAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => x.Id == id));
    public Task<StrategyPeriodAggregate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)));
    public Task<IReadOnlyList<StrategyPeriodAggregate>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StrategyPeriodAggregate>>(_rows.ToList());
    public Task<IReadOnlyList<StrategyPeriodAggregate>> ListByPlanningCycleIdAsync(string planningCycleId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<StrategyPeriodAggregate>>(_rows.Where(x => string.Equals(x.PlanningCycleId, planningCycleId, StringComparison.OrdinalIgnoreCase)).ToList());
    public Task AddAsync(StrategyPeriodAggregate aggregate, CancellationToken cancellationToken = default) { _rows.Add(aggregate); return Task.CompletedTask; }
    public Task UpdateAsync(StrategyPeriodAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var idx = _rows.FindIndex(x => x.Id == aggregate.Id);
        if (idx >= 0) _rows[idx] = aggregate;
        return Task.CompletedTask;
    }
}

file sealed class InMemoryConnectionRepository : IStrategyConnectionRepository
{
    private readonly List<StrategyConnectionAggregate> _rows = new();
    public Task<StrategyConnectionAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => x.Id == id));
    public Task<IReadOnlyList<StrategyConnectionAggregate>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StrategyConnectionAggregate>>(_rows.ToList());
    public Task<StrategyConnectionAggregate?> GetByEdgeAsync(string fromType, string fromId, string toType, string toId, CancellationToken cancellationToken = default)
        => Task.FromResult(_rows.FirstOrDefault(x => x.FromType == fromType && x.FromId == fromId && x.ToType == toType && x.ToId == toId));
    public Task AddAsync(StrategyConnectionAggregate aggregate, CancellationToken cancellationToken = default) { _rows.Add(aggregate); return Task.CompletedTask; }
    public Task UpdateAsync(StrategyConnectionAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var idx = _rows.FindIndex(x => x.Id == aggregate.Id);
        if (idx >= 0) _rows[idx] = aggregate;
        return Task.CompletedTask;
    }
    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _rows.RemoveAll(x => x.Id == id);
        return Task.CompletedTask;
    }
}

file sealed class InMemoryInitiativeLinkRepository : IInitiativeStrategyLinkRepository
{
    private readonly List<InitiativeStrategyLinkAggregate> _rows = new();
    public Task<IReadOnlyList<InitiativeStrategyLinkAggregate>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InitiativeStrategyLinkAggregate>>(_rows.ToList());
    public Task<InitiativeStrategyLinkAggregate?> GetByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => x.InitiativeId == initiativeId));
    public Task<IReadOnlyList<InitiativeStrategyLinkAggregate>> ListByGoalIdAsync(string goalId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InitiativeStrategyLinkAggregate>>(_rows.Where(x => x.ParentGoalId == goalId).ToList());
    public Task<IReadOnlyList<InitiativeStrategyLinkAggregate>> ListByObjectiveIdAsync(string objectiveId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InitiativeStrategyLinkAggregate>>(_rows.Where(x => x.ParentObjectiveId == objectiveId).ToList());
    public Task AddOrUpdateAsync(InitiativeStrategyLinkAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var idx = _rows.FindIndex(x => x.InitiativeId == aggregate.InitiativeId);
        if (idx >= 0) _rows[idx] = aggregate; else _rows.Add(aggregate);
        return Task.CompletedTask;
    }
    public Task DeleteByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default)
    {
        _rows.RemoveAll(x => x.InitiativeId == initiativeId);
        return Task.CompletedTask;
    }
}

file sealed class InMemoryProjectLinkRepository : IProjectStrategyLinkRepository
{
    private readonly List<ProjectStrategyLinkAggregate> _rows = new();
    public Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectStrategyLinkAggregate>>(_rows.ToList());
    public Task<ProjectStrategyLinkAggregate?> GetByProjectIdAsync(string projectId, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => x.ProjectId == projectId));
    public Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListByGoalIdAsync(string goalId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectStrategyLinkAggregate>>(_rows.Where(x => x.ParentGoalId == goalId).ToList());
    public Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListByObjectiveIdAsync(string objectiveId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectStrategyLinkAggregate>>(_rows.Where(x => x.ParentObjectiveId == objectiveId).ToList());
    public Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectStrategyLinkAggregate>>(_rows.Where(x => x.ParentInitiativeId == initiativeId).ToList());
    public Task AddOrUpdateAsync(ProjectStrategyLinkAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var idx = _rows.FindIndex(x => x.ProjectId == aggregate.ProjectId);
        if (idx >= 0) _rows[idx] = aggregate; else _rows.Add(aggregate);
        return Task.CompletedTask;
    }
    public Task DeleteByProjectIdAsync(string projectId, CancellationToken cancellationToken = default)
    {
        _rows.RemoveAll(x => x.ProjectId == projectId);
        return Task.CompletedTask;
    }
}

file sealed class InMemoryPpmInitiativeCacheRepository : IPpmInitiativeCacheRepository
{
    private readonly List<PpmInitiativeReadModelAggregate> _rows = new();
    public Task<IReadOnlyList<PpmInitiativeReadModelAggregate>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PpmInitiativeReadModelAggregate>>(_rows.ToList());
    public Task<PpmInitiativeReadModelAggregate?> GetByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => x.InitiativeId == initiativeId));
    public Task UpsertManyAsync(IReadOnlyList<PpmInitiativeReadModelAggregate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            var idx = _rows.FindIndex(x => x.InitiativeId == row.InitiativeId);
            if (idx >= 0) _rows[idx] = row; else _rows.Add(row);
        }
        return Task.CompletedTask;
    }
}

file sealed class InMemoryPpmProjectCacheRepository : IPpmProjectCacheRepository
{
    private readonly List<PpmProjectReadModelAggregate> _rows = new();
    public Task<IReadOnlyList<PpmProjectReadModelAggregate>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PpmProjectReadModelAggregate>>(_rows.ToList());
    public Task<PpmProjectReadModelAggregate?> GetByProjectIdAsync(string projectId, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => x.ProjectId == projectId));
    public Task UpsertManyAsync(IReadOnlyList<PpmProjectReadModelAggregate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            var idx = _rows.FindIndex(x => x.ProjectId == row.ProjectId);
            if (idx >= 0) _rows[idx] = row; else _rows.Add(row);
        }
        return Task.CompletedTask;
    }
}

file sealed class ThrowingInitiativeAdapter : IPpmInitiativeReadAdapter
{
    public Task<PpmInitiativeReadModel?> GetByIdAsync(string initiativeId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ppm unavailable");
    public Task<IReadOnlyList<PpmInitiativeReadModel>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ppm unavailable");
    public Task<IReadOnlyList<PpmInitiativeReadModel>> SyncAsync(string correlationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ppm unavailable");
}

file sealed class ThrowingProjectAdapter : IPpmProjectReadAdapter
{
    public Task<PpmProjectReadModel?> GetByIdAsync(string projectId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ppm unavailable");
    public Task<IReadOnlyList<PpmProjectReadModel>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ppm unavailable");
    public Task<IReadOnlyList<PpmProjectReadModel>> SyncAsync(string correlationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ppm unavailable");
}

file sealed class InMemoryStrategyLibraryRepository : IStrategyLibraryRepository
{
    public Task<TemplateImportBatch?> GetImportBatchAsync(string batchId, CancellationToken cancellationToken = default) => Task.FromResult<TemplateImportBatch?>(null);
    public Task<IReadOnlyList<TemplateImportIssue>> ListImportIssuesAsync(string batchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TemplateImportIssue>>(Array.Empty<TemplateImportIssue>());
    public Task UpsertImportBatchAsync(TemplateImportBatch batch, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpsertImportIssuesAsync(IReadOnlyList<TemplateImportIssue> issues, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<GoalTemplate>> ListGoalTemplatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GoalTemplate>>(Array.Empty<GoalTemplate>());
    public Task<GoalTemplate?> GetGoalTemplateAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<GoalTemplate?>(null);
    public Task UpsertGoalTemplatesAsync(IReadOnlyList<GoalTemplate> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceGoalTemplateMetricsAsync(string goalTemplateId, IReadOnlyList<GoalTemplateMetric> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<GoalTemplateMetric>> ListGoalTemplateMetricsAsync(string goalTemplateId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GoalTemplateMetric>>(Array.Empty<GoalTemplateMetric>());
    public Task<IReadOnlyList<ObjectiveTemplate>> ListObjectiveTemplatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ObjectiveTemplate>>(Array.Empty<ObjectiveTemplate>());
    public Task<ObjectiveTemplate?> GetObjectiveTemplateAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ObjectiveTemplate?>(null);
    public Task UpsertObjectiveTemplatesAsync(IReadOnlyList<ObjectiveTemplate> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceObjectiveTemplateMetricsAsync(string objectiveTemplateId, IReadOnlyList<ObjectiveTemplateMetric> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<ObjectiveTemplateMetric>> ListObjectiveTemplateMetricsAsync(string objectiveTemplateId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ObjectiveTemplateMetric>>(Array.Empty<ObjectiveTemplateMetric>());
    public Task<IReadOnlyList<InitiativeTemplate>> ListInitiativeTemplatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InitiativeTemplate>>(Array.Empty<InitiativeTemplate>());
    public Task<InitiativeTemplate?> GetInitiativeTemplateAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<InitiativeTemplate?>(null);
    public Task UpsertInitiativeTemplatesAsync(IReadOnlyList<InitiativeTemplate> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceInitiativeTemplateMetricsAsync(string initiativeTemplateId, IReadOnlyList<InitiativeTemplateMetric> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<InitiativeTemplateMetric>> ListInitiativeTemplateMetricsAsync(string initiativeTemplateId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InitiativeTemplateMetric>>(Array.Empty<InitiativeTemplateMetric>());
    public Task<IReadOnlyList<ProjectTemplate>> ListProjectTemplatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectTemplate>>(Array.Empty<ProjectTemplate>());
    public Task<ProjectTemplate?> GetProjectTemplateAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ProjectTemplate?>(null);
    public Task UpsertProjectTemplatesAsync(IReadOnlyList<ProjectTemplate> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceProjectTemplateMetricsAsync(string projectTemplateId, IReadOnlyList<ProjectTemplateMetric> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<ProjectTemplateMetric>> ListProjectTemplateMetricsAsync(string projectTemplateId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectTemplateMetric>>(Array.Empty<ProjectTemplateMetric>());
    public Task<IReadOnlyDictionary<string, int>> CountProjectTemplateMetricsByProjectAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
    public Task<IReadOnlyList<StrategyBlueprintPack>> ListBlueprintPacksAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StrategyBlueprintPack>>(Array.Empty<StrategyBlueprintPack>());
    public Task<StrategyBlueprintPack?> GetBlueprintPackAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<StrategyBlueprintPack?>(null);
    public Task UpsertBlueprintPacksAsync(IReadOnlyList<StrategyBlueprintPack> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceBlueprintPackItemsAsync(string packId, IReadOnlyList<StrategyBlueprintPackItem> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<StrategyBlueprintPackItem>> ListBlueprintPackItemsAsync(string packId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StrategyBlueprintPackItem>>(Array.Empty<StrategyBlueprintPackItem>());
    public Task AddTemplateVersionAsync(TemplateVersion version, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<TemplateVersion>> ListTemplateVersionsAsync(string templateType, string templateId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TemplateVersion>>(Array.Empty<TemplateVersion>());
    public Task AddPublishHistoryAsync(TemplatePublishHistory row, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddInstantiationBatchAsync(InstantiationBatch batch, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddInstantiationRecordsAsync(IReadOnlyList<InstantiationRecord> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task AddOverrideLogsAsync(IReadOnlyList<TemplateOverrideLog> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<InstantiationBatch>> ListInstantiationBatchesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InstantiationBatch>>(Array.Empty<InstantiationBatch>());
    public Task UpsertUsageStatsAsync(IReadOnlyList<TemplateUsageStat> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<TemplateUsageStat>> ListUsageStatsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TemplateUsageStat>>(Array.Empty<TemplateUsageStat>());
}
