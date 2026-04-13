using Diten.Application.Dtos.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Adapters.Ppm;
using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Application.EnterpriseStrategy.Services;
using Diten.Application.EnterpriseStrategy.Shared;
using Diten.Domain.Aggregates.EnterpriseStrategy;
using System.Linq;
using System.Security.Claims;

namespace Diten.Application.Tests;

public sealed class EnterpriseStrategyModuleTests
{
    private const string ActiveStrategyPeriodId = "sp-active";
    private const string ActiveCompanyId = "cmp-001";
    private const string StrategicThemeId = "theme-growth";

    [Fact]
    public async Task DuplicateId_Rejected_For_Goal_Create()
    {
        var goals = new InMemoryGoalRepository(new GoalAggregate { Id = "goal-1", Name = "Existing" });
        var service = BuildGoalService(goals);

        var result = await service.CreateAsync(new GoalDto
        {
            Id = "goal-1",
            Name = "Duplicate attempt",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            OwnerRole = "usr-ceo",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Draft",
            Priority = "Medium",
            Statement = "Duplicate id validation",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        }, "tester", "corr");

        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.Conflict, result.Error?.Code);
    }

    [Fact]
    public async Task Activation_Rules_Enforced_For_Goal()
    {
        var service = BuildGoalService(new InMemoryGoalRepository());
        var result = await service.CreateAsync(new GoalDto
        {
            Id = "goal-2",
            Name = "Goal",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            OwnerRole = "usr-ceo",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Active",
            Priority = "Medium",
            Statement = "Activation rule test",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        }, "tester", "corr");

        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.ValidationError, result.Error?.Code);
        Assert.True(result.Error?.Details.ContainsKey("category"));
    }

    [Fact]
    public async Task End_Year_Before_Start_Year_Is_Rejected()
    {
        var service = BuildGoalService(new InMemoryGoalRepository());
        var result = await service.CreateAsync(new GoalDto
        {
            Name = "Planning Validation",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            OwnerRole = "usr-ceo",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Draft",
            Priority = "Medium",
            Statement = "Testing date validation",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            PlanningHorizonStart = new DateTime(2030, 1, 1),
            PlanningHorizonEnd = new DateTime(2029, 12, 31),
            ScopeMode = "Enterprise"
        }, "tester", "corr");

        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.ValidationError, result.Error?.Code);
        Assert.True(result.Error?.Details.ContainsKey("planning.endDate"));
    }

    [Fact]
    public async Task Enterprise_Scope_With_Companies_Is_Rejected()
    {
        var service = BuildGoalService(new InMemoryGoalRepository());
        var result = await service.CreateAsync(new GoalDto
        {
            Name = "Scope Validation",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            OwnerRole = "usr-ceo",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Draft",
            Priority = "Medium",
            Statement = "Testing enterprise scope",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2029, 12, 31),
            ScopeMode = "Enterprise",
            PrimaryCompanyId = "cmp-001",
            ApplicableCompanyIds = new() { "cmp-001" }
        }, "tester", "corr");

        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.ValidationError, result.Error?.Code);
        Assert.True(result.Error?.Details.ContainsKey("companyScope.applicableCompanyIds"));
    }

    [Fact]
    public async Task Draft_Save_Allows_Optional_Metrics_And_Budgets()
    {
        var service = BuildGoalService(new InMemoryGoalRepository());
        var result = await service.CreateAsync(new GoalDto
        {
            Name = "Draft Goal",
            Category = "Growth",
            OwnerRole = "usr-ceo",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Draft",
            Priority = "Medium",
            GoalStatement = "Draft-only save flow",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            ScopeMode = "Enterprise",
            Metrics = new(),
            YearlyBudgets = new()
        }, "tester", "corr");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Publish_Requires_Active_Kpi_YearlyTargets_And_Governance_Refs()
    {
        var service = BuildGoalService(new InMemoryGoalRepository());
        var result = await service.CreateAsync(new GoalDto
        {
            Name = "Publish Goal",
            Category = "Growth",
            OwnerRole = "usr-ceo",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Active",
            Priority = "Medium",
            GoalStatement = "Publish validation flow",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            ScopeMode = "Enterprise",
            Version = 1,
            Metrics = new()
            {
                new GoalMetricDto
                {
                    Id = "kpi-1",
                    MetricName = "Revenue Growth",
                    MetricType = "%",
                    UnitOfMeasure = "Percentage",
                    AggregationMethod = "Sum",
                    PolarityCode = "Increase",
                    ThresholdModelCode = "None",
                    ReportingFrequencyCode = "Annual",
                    MetricRole = "Strategic",
                    MetricOrigin = "Local",
                    MetricBindingStatus = "Bound",
                    YearlyValues = new()
                }
            }
        }, "tester", "corr");

        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.ValidationError, result.Error?.Code);
        Assert.True(result.Error?.Details.ContainsKey("metrics[0].yearlyValues"));
        Assert.True(result.Error?.Details.ContainsKey("changeLogRef"));
        Assert.True(result.Error?.Details.ContainsKey("decisionReference"));
    }

    [Fact]
    public async Task Publish_With_Active_Kpi_And_Yearly_Targets_Succeeds()
    {
        var service = BuildGoalService(new InMemoryGoalRepository());
        var result = await service.CreateAsync(new GoalDto
        {
            Name = "Publish Goal Success",
            Category = "Growth",
            OwnerRole = "usr-ceo",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Active",
            Priority = "Medium",
            GoalStatement = "Publish happy path",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "SingleCompany",
            ScopeMode = "SingleCompany",
            PrimaryCompanyId = ActiveCompanyId,
            ApplicableCompanyIds = new() { ActiveCompanyId },
            Version = 1,
            ChangeLogRef = "CL-001",
            DecisionReference = "DR-001",
            Metrics = new()
            {
                new GoalMetricDto
                {
                    Id = "kpi-pub-1",
                    MetricName = "Revenue Growth",
                    MetricType = "%",
                    UnitOfMeasure = "Percentage",
                    AggregationMethod = "Sum",
                    PolarityCode = "Increase",
                    ThresholdModelCode = "None",
                    ReportingFrequencyCode = "Annual",
                    MetricRole = "Strategic",
                    MetricOrigin = "Local",
                    MetricBindingStatus = "Bound",
                    YearlyValues = new()
                    {
                        new GoalMetricYearValueDto { Year = 2026, TargetValue = 12 }
                    }
                }
            }
        }, "tester", "corr");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Active", result.Data!.Status);
        Assert.Single(result.Data.Metrics);
    }

    [Fact]
    public async Task Update_Removes_Deleted_Kpi_And_Yearly_Target_Rows()
    {
        var goals = new InMemoryGoalRepository();
        var service = BuildGoalService(goals);
        var created = await service.CreateAsync(new GoalDto
        {
            Name = "Update Goal",
            Category = "Growth",
            OwnerRole = "usr-ceo",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Draft",
            Priority = "Medium",
            GoalStatement = "Update orchestration",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            ScopeMode = "Enterprise",
            Metrics = new()
            {
                new GoalMetricDto
                {
                    Id = "kpi-a",
                    MetricName = "Revenue",
                    MetricType = "%",
                    UnitOfMeasure = "Percentage",
                    AggregationMethod = "Sum",
                    PolarityCode = "Increase",
                    ThresholdModelCode = "None",
                    ReportingFrequencyCode = "Annual",
                    MetricOrigin = "Local",
                    MetricRole = "Strategic",
                    MetricBindingStatus = "Bound",
                    YearlyValues = new()
                    {
                        new GoalMetricYearValueDto { Year = 2026, TargetValue = 10 },
                        new GoalMetricYearValueDto { Year = 2027, TargetValue = 15 }
                    }
                },
                new GoalMetricDto
                {
                    Id = "kpi-b",
                    MetricName = "Margin",
                    MetricType = "%",
                    UnitOfMeasure = "Percentage",
                    AggregationMethod = "Sum",
                    PolarityCode = "Increase",
                    ThresholdModelCode = "None",
                    ReportingFrequencyCode = "Annual",
                    MetricOrigin = "Local",
                    MetricRole = "Strategic",
                    MetricBindingStatus = "Bound",
                    YearlyValues = new()
                    {
                        new GoalMetricYearValueDto { Year = 2026, TargetValue = 2 }
                    }
                }
            }
        }, "tester", "corr");

        Assert.True(created.Success);
        Assert.Equal(2, goals.PersistedMetrics.Count);
        Assert.Equal(3, goals.PersistedMetricYearlyTargets.Count);

        var updatePayload = created.Data!;
        updatePayload.Status = "Draft";
        updatePayload.Metrics = new()
        {
            new GoalMetricDto
            {
                Id = "kpi-a",
                MetricName = "Revenue",
                MetricType = "%",
                UnitOfMeasure = "Percentage",
                AggregationMethod = "Sum",
                PolarityCode = "Increase",
                ThresholdModelCode = "None",
                ReportingFrequencyCode = "Annual",
                MetricOrigin = "Local",
                MetricRole = "Strategic",
                MetricBindingStatus = "Bound",
                YearlyValues = new()
                {
                    new GoalMetricYearValueDto { Year = 2027, TargetValue = 16 }
                }
            }
        };
        updatePayload.YearlyBudgets = new();

        var updated = await service.UpdateAsync(updatePayload.Id, updatePayload, expectedVersion: created.Data!.Version, actor: "tester", correlationId: "corr");
        Assert.True(updated.Success);
        Assert.Single(goals.PersistedMetrics);
        Assert.Single(goals.PersistedMetricYearlyTargets);
        Assert.Empty(goals.PersistedBudgetEnvelopes);
    }

    [Fact]
    public void Repeated_Row_Normalization_Builds_Single_Goal_With_Metrics()
    {
        var normalization = new EnterpriseStrategyNormalizationService();
        var rows = new[]
        {
            new WorkbookGoalRowDto { GoalId = "goal-3", Name = "A", Category = "Growth", Owner = "Owner", MetricId = "m1", MetricName = "Revenue", MetricBaseline = 10, MetricTarget = 20 },
            new WorkbookGoalRowDto { GoalId = "goal-3", Name = "A", Category = "Growth", Owner = "Owner", MetricId = "m2", MetricName = "Margin", MetricBaseline = 1, MetricTarget = 3 },
            new WorkbookGoalRowDto { GoalId = "goal-3", Name = "A", Category = "Growth", Owner = "Owner", MetricId = "m1", MetricName = "Revenue", MetricBaseline = 10, MetricTarget = 20 }
        };

        var goal = normalization.NormalizeGoalRows(rows);

        Assert.Equal("goal-3", goal.Id);
        Assert.Equal(2, goal.Metrics.Count);
    }

    [Fact]
    public async Task Goal_Summary_Query_Returns_Downstream_Counts()
    {
        var goals = new InMemoryGoalRepository(new GoalAggregate { Id = "goal-4", Name = "Goal 4", Metrics = new() { new GoalMetric { Id = "m1" } } });
        var objectives = new InMemoryObjectiveRepository(new ObjectiveAggregate { Id = "obj-1", ParentGoalId = "goal-4", Status = "Active" });
        var initiative = new InMemoryInitiativeLinkRepository(new InitiativeStrategyLinkAggregate { Id = "i-1", ParentGoalId = "goal-4", InitiativeId = "init-1" });
        var project = new InMemoryProjectLinkRepository(new ProjectStrategyLinkAggregate { Id = "p-1", ParentGoalId = "goal-4", ProjectId = "prj-1" });
        var service = new GoalService(goals, objectives, initiative, project, new InMemoryStrategyPeriodRepository(), new NoOpEnterpriseStrategyAuditSink());

        var result = await service.GetSummaryAsync("goal-4");

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.MetricsCount);
        Assert.Equal(1, result.Data!.ChildObjectivesSummary.TotalObjectives);
        Assert.Equal(1, result.Data!.LinkedInitiativesCount);
        Assert.Equal(1, result.Data!.LinkedProjectsCount);
    }

    [Fact]
    public async Task PlanningCycle_Archive_Is_Blocked_When_Downstream_Usage_Exists()
    {
        var service = BuildPlanningCycleService(
            new InMemoryPlanningCycleRepository(new PlanningCycleAggregate
            {
                Id = "pc-archive-1",
                Code = "PC-ARCHIVE-1",
                Name = "Cycle Archive Guard",
                PlanningCycleType = "Annual",
                OwnerCompanyId = ActiveCompanyId,
                OwnerPositionId = "pos-1",
                Status = "Active",
                EffectiveFrom = new DateTime(2026, 4, 1),
                EffectiveTo = new DateTime(2026, 4, 20)
            }),
            new InMemoryStrategyPeriodRepository(new StrategyPeriodAggregate
            {
                Id = "sp-archive-1",
                PlanningCycleId = "pc-archive-1",
                Code = "SP-ARCHIVE-1",
                Name = "Cycle Linked Period",
                CompanyId = ActiveCompanyId,
                StartDate = new DateTime(2026, 4, 1),
                EndDate = new DateTime(2026, 4, 20),
                Status = "Active",
                OwnerEmployeeId = "usr-ceo",
                ReviewCadence = "Quarterly"
            }),
            new InMemoryGoalRepository(new GoalAggregate { Id = "goal-archive-1", StrategyPeriodId = "sp-archive-1" }),
            new InMemoryObjectiveRepository(new ObjectiveAggregate { Id = "obj-archive-1", ParentGoalId = "goal-archive-1" }),
            new InMemoryInitiativeLinkRepository(new InitiativeStrategyLinkAggregate { Id = "lnk-archive-1", InitiativeId = "init-archive-1", ParentGoalId = "goal-archive-1", ParentObjectiveId = "obj-archive-1" }));

        var result = await service.ChangePlanningCycleStatusAsync("pc-archive-1", "Archived", "tester");

        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.Conflict, result.Error?.Code);
        Assert.Contains("inUse", result.Error?.Details.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlanningCycle_Update_Blocks_Date_Shrink_When_Linked_Period_Would_Fall_Outside_Horizon()
    {
        var service = BuildPlanningCycleService(
            new InMemoryPlanningCycleRepository(new PlanningCycleAggregate
            {
                Id = "pc-shrink-1",
                Code = "PC-SHRINK-1",
                Name = "Cycle Shrink Guard",
                PlanningCycleType = "Annual",
                OwnerCompanyId = ActiveCompanyId,
                OwnerPositionId = "pos-1",
                Status = "Active",
                EffectiveFrom = new DateTime(2026, 4, 1),
                EffectiveTo = new DateTime(2026, 4, 20)
            }),
            new InMemoryStrategyPeriodRepository(new StrategyPeriodAggregate
            {
                Id = "sp-shrink-1",
                PlanningCycleId = "pc-shrink-1",
                Code = "SP-SHRINK-1",
                Name = "Cycle Linked Period",
                CompanyId = ActiveCompanyId,
                StartDate = new DateTime(2026, 4, 2),
                EndDate = new DateTime(2026, 4, 18),
                Status = "Draft",
                OwnerEmployeeId = "usr-ceo",
                ReviewCadence = "Quarterly"
            }));

        var result = await service.UpdatePlanningCycleAsync("pc-shrink-1", new PlanningCycleDto
        {
            Id = "pc-shrink-1",
            Code = "PC-SHRINK-1",
            Name = "Cycle Shrink Guard",
            PlanningCycleType = "Annual",
            OwnerCompanyId = ActiveCompanyId,
            OwnerPositionId = "pos-1",
            Status = "Active",
            EffectiveFrom = new DateTime(2026, 4, 5),
            EffectiveTo = new DateTime(2026, 4, 15)
        }, "tester");

        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.Conflict, result.Error?.Code);
        Assert.Contains("effectiveFrom", result.Error?.Details.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("effectiveTo", result.Error?.Details.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlanningCycle_Update_Allows_Date_Expand_When_Linked_Period_Remains_Inside_Horizon()
    {
        var service = BuildPlanningCycleService(
            new InMemoryPlanningCycleRepository(new PlanningCycleAggregate
            {
                Id = "pc-expand-1",
                Code = "PC-EXPAND-1",
                Name = "Cycle Expand Guard",
                PlanningCycleType = "Annual",
                OwnerCompanyId = ActiveCompanyId,
                OwnerPositionId = "pos-1",
                Status = "Active",
                EffectiveFrom = new DateTime(2026, 4, 1),
                EffectiveTo = new DateTime(2026, 4, 20)
            }),
            new InMemoryStrategyPeriodRepository(new StrategyPeriodAggregate
            {
                Id = "sp-expand-1",
                PlanningCycleId = "pc-expand-1",
                Code = "SP-EXPAND-1",
                Name = "Cycle Linked Period",
                CompanyId = ActiveCompanyId,
                StartDate = new DateTime(2026, 4, 2),
                EndDate = new DateTime(2026, 4, 18),
                Status = "Active",
                OwnerEmployeeId = "usr-ceo",
                ReviewCadence = "Quarterly"
            }));

        var result = await service.UpdatePlanningCycleAsync("pc-expand-1", new PlanningCycleDto
        {
            Id = "pc-expand-1",
            Code = "PC-EXPAND-1",
            Name = "Cycle Expand Guard",
            PlanningCycleType = "Annual",
            OwnerCompanyId = ActiveCompanyId,
            OwnerPositionId = "pos-1",
            Status = "Active",
            EffectiveFrom = new DateTime(2026, 3, 28),
            EffectiveTo = new DateTime(2026, 4, 25)
        }, "tester");

        Assert.True(result.Success);
        Assert.Equal(new DateTime(2026, 3, 28), result.Data!.EffectiveFrom);
        Assert.Equal(new DateTime(2026, 4, 25), result.Data.EffectiveTo);
    }

    [Fact]
    public async Task Rbac_Enforcement_Uses_Permission_Claims()
    {
        var service = new DefaultEnterpriseStrategyAuthorizationService();
        var identity = new ClaimsIdentity(new[] { new Claim("permission", "strategy.goal.view") }, "test");
        var principal = new ClaimsPrincipal(identity);

        var allowed = await service.HasPermissionAsync("strategy.goal.view", principal);
        var denied = await service.HasPermissionAsync("strategy.goal.edit", principal);

        Assert.True(allowed);
        Assert.False(denied);
    }

    [Fact]
    public async Task Rbac_Enforcement_Denies_Unauthenticated_Principal()
    {
        var service = new DefaultEnterpriseStrategyAuthorizationService();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var allowed = await service.HasPermissionAsync(EnterpriseStrategyPermissions.GoalView, principal);
        Assert.False(allowed);
    }

    [Fact]
    public async Task Initiative_Duplicate_Alignment_Rejected()
    {
        var goals = new InMemoryGoalRepository(new GoalAggregate
        {
            Id = "goal-1",
            Name = "G1",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            Status = "Draft",
            Priority = "Medium",
            Statement = "Fixture goal",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        });
        var objectives = new InMemoryObjectiveRepository(new ObjectiveAggregate { Id = "obj-1", ParentGoalId = "goal-1", Name = "O1" });
        var links = new InMemoryInitiativeLinkRepository(new InitiativeStrategyLinkAggregate { Id = "lnk-1", InitiativeId = "init-001", ParentObjectiveId = "obj-1", ParentGoalId = "goal-1" });
        var service = new InitiativeOrchestrationService(new MockPpmInitiativeReadAdapter(), new InMemoryPpmInitiativeCacheRepository(), links, new InMemoryProjectLinkRepository(), objectives, goals, new NoOpEnterpriseStrategyAuditSink());

        var result = await service.UpsertStrategyLinkAsync("init-002", new InitiativeStrategyLinkViewDto
        {
            InitiativeId = "init-002",
            ParentObjectiveId = "obj-1",
            StrategyLinkStatus = "Linked",
            SponsoringCompanyId = "cmp-001"
        }, 0, "tester", "corr");

        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.Conflict, result.Error?.Code);
    }

    [Fact]
    public async Task Initiative_Objective_Validation_Enforced()
    {
        var service = new InitiativeOrchestrationService(new MockPpmInitiativeReadAdapter(), new InMemoryPpmInitiativeCacheRepository(), new InMemoryInitiativeLinkRepository(), new InMemoryProjectLinkRepository(), new InMemoryObjectiveRepository(), new InMemoryGoalRepository(), new NoOpEnterpriseStrategyAuditSink());
        var result = await service.UpsertStrategyLinkAsync("init-001", new InitiativeStrategyLinkViewDto { InitiativeId = "init-001", ParentObjectiveId = "missing-obj", StrategyLinkStatus = "Linked" }, 0, "tester", "corr");
        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.ValidationError, result.Error?.Code);
    }

    [Fact]
    public async Task Adapter_Mapping_And_Sync_Failure_Handled()
    {
        var failingAdapter = new ThrowingPpmInitiativeReadAdapter();
        var service = new InitiativeOrchestrationService(failingAdapter, new InMemoryPpmInitiativeCacheRepository(), new InMemoryInitiativeLinkRepository(), new InMemoryProjectLinkRepository(), new InMemoryObjectiveRepository(), new InMemoryGoalRepository(), new NoOpEnterpriseStrategyAuditSink());
        var sync = await service.SyncAsync("corr-1", "tester");
        Assert.False(sync.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.DependencyUnavailable, sync.Error?.Code);
    }

    [Fact]
    public async Task Project_Orchestration_Only_Boundary_Enforced()
    {
        var goals = new InMemoryGoalRepository(new GoalAggregate
        {
            Id = "goal-1",
            Name = "G1",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            Status = "Draft",
            Priority = "Medium",
            Statement = "Fixture goal",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        });
        var objectives = new InMemoryObjectiveRepository(new ObjectiveAggregate { Id = "obj-1", ParentGoalId = "goal-1", Name = "O1" });
        var initiatives = new InMemoryInitiativeLinkRepository(new InitiativeStrategyLinkAggregate
        {
            Id = "lnk-i",
            InitiativeId = "init-001",
            ParentObjectiveId = "obj-1",
            ParentGoalId = "goal-1",
            StrategyLinkStatus = "Linked"
        });
        var projects = new InMemoryProjectLinkRepository();
        var service = new ProjectOrchestrationService(new MockPpmProjectReadAdapter(), new InMemoryPpmProjectCacheRepository(), projects, initiatives, objectives, goals, new InMemoryStrategyLibraryRepository(), new NoOpEnterpriseStrategyAuditSink());
        var result = await service.UpsertStrategyLinkAsync("prj-001", new ProjectStrategyLinkViewDto
        {
            ProjectId = "prj-001",
            ParentInitiativeId = "missing-init",
            StrategyLinkStatus = "Linked"
        }, 0, "tester", "corr");
        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.ValidationError, result.Error?.Code);
    }

    [Fact]
    public async Task Draft_Project_Save_Allows_Partial_Data_After_Anchor_Selection()
    {
        var goals = new InMemoryGoalRepository(new GoalAggregate
        {
            Id = "goal-draft-1",
            Name = "Goal Draft",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            Status = "Draft",
            Priority = "Medium",
            Statement = "Goal draft fixture",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        });
        var objectives = new InMemoryObjectiveRepository(new ObjectiveAggregate
        {
            Id = "obj-draft-1",
            ParentGoalId = "goal-draft-1",
            Name = "Objective Draft"
        });
        var initiatives = new InMemoryInitiativeLinkRepository(new InitiativeStrategyLinkAggregate
        {
            Id = "init-link-draft-1",
            InitiativeId = "init-draft-1",
            InitiativeName = "Initiative Draft",
            ParentObjectiveId = "obj-draft-1",
            ParentGoalId = "goal-draft-1",
            EntityScope = "Enterprise",
            Type = "Growth",
            StrategyLinkStatus = "Linked"
        });
        var service = new ProjectOrchestrationService(
            new MockPpmProjectReadAdapter(),
            new InMemoryPpmProjectCacheRepository(),
            new InMemoryProjectLinkRepository(),
            initiatives,
            objectives,
            goals,
            new InMemoryStrategyLibraryRepository(),
            new NoOpEnterpriseStrategyAuditSink());

        var result = await service.CreateAsync(new ProjectStrategyLinkViewDto
        {
            ParentInitiativeId = "init-draft-1",
            Status = "Draft"
        }, "tester", "corr");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("init-draft-1", result.Data!.ParentInitiativeId);
        Assert.Equal("goal-draft-1", result.Data.ParentGoalId);
        Assert.Equal("obj-draft-1", result.Data.ParentObjectiveId);
        Assert.Equal("Growth", result.Data.ParentType);
    }

    [Fact]
    public async Task Compatible_Project_Templates_Filter_By_ParentType_And_Active_Status()
    {
        var library = new InMemoryStrategyLibraryRepository(
            goalTemplates:
            [
                new GoalTemplate { Id = "goal-template-growth", Category = "Growth" },
                new GoalTemplate { Id = "goal-template-risk", Category = "Risk" }
            ],
            projectTemplates:
            [
                new ProjectTemplate
                {
                    Id = "tmpl-match",
                    Name = "Growth Enterprise",
                    ParentGoalTemplateId = "goal-template-growth",
                    EntityScope = "Enterprise",
                    LifecycleStatus = "Published"
                },
                new ProjectTemplate
                {
                    Id = "tmpl-wrong-scope",
                    Name = "Growth Single",
                    ParentGoalTemplateId = "goal-template-growth",
                    EntityScope = "SingleCompany",
                    LifecycleStatus = "Published"
                },
                new ProjectTemplate
                {
                    Id = "tmpl-wrong-type",
                    Name = "Risk Enterprise",
                    ParentGoalTemplateId = "goal-template-risk",
                    EntityScope = "Enterprise",
                    LifecycleStatus = "Published"
                },
                new ProjectTemplate
                {
                    Id = "tmpl-inactive",
                    Name = "Growth Draft",
                    ParentGoalTemplateId = "goal-template-growth",
                    EntityScope = "Enterprise",
                    LifecycleStatus = "Draft"
                }
            ]);

        var service = new ProjectOrchestrationService(
            new MockPpmProjectReadAdapter(),
            new InMemoryPpmProjectCacheRepository(),
            new InMemoryProjectLinkRepository(),
            new InMemoryInitiativeLinkRepository(),
            new InMemoryObjectiveRepository(),
            new InMemoryGoalRepository(),
            library,
            new NoOpEnterpriseStrategyAuditSink());

        var result = await service.GetCompatibleTemplatesAsync("Growth", "Enterprise");

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal(new[] { "tmpl-match", "tmpl-wrong-scope" }, result.Data!.Select(x => x.TemplateId).ToArray());
    }

    [Fact]
    public async Task Compatible_Project_Templates_Fall_Back_To_Draft_When_No_Active_Type_Match_Exists()
    {
        var library = new InMemoryStrategyLibraryRepository(
            goalTemplates:
            [
                new GoalTemplate { Id = "goal-template-growth", Category = "Growth" },
                new GoalTemplate { Id = "goal-template-risk", Category = "Risk" }
            ],
            projectTemplates:
            [
                new ProjectTemplate
                {
                    Id = "tmpl-draft-growth",
                    Name = "Growth Draft Template",
                    ParentGoalTemplateId = "goal-template-growth",
                    EntityScope = "Enterprise",
                    LifecycleStatus = "Draft"
                },
                new ProjectTemplate
                {
                    Id = "tmpl-active-risk",
                    Name = "Risk Active Template",
                    ParentGoalTemplateId = "goal-template-risk",
                    EntityScope = "Enterprise",
                    LifecycleStatus = "Published"
                }
            ]);

        var service = new ProjectOrchestrationService(
            new MockPpmProjectReadAdapter(),
            new InMemoryPpmProjectCacheRepository(),
            new InMemoryProjectLinkRepository(),
            new InMemoryInitiativeLinkRepository(),
            new InMemoryObjectiveRepository(),
            new InMemoryGoalRepository(),
            library,
            new NoOpEnterpriseStrategyAuditSink());

        var result = await service.GetCompatibleTemplatesAsync("Growth", "Enterprise");

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("tmpl-draft-growth", result.Data![0].TemplateId);
    }

    [Fact]
    public async Task Project_Template_Compatibility_Uses_Initiative_Type_Before_Goal_Category()
    {
        var goals = new InMemoryGoalRepository(new GoalAggregate
        {
            Id = "goal-parent-type-1",
            Name = "Goal Parent Type",
            Category = "Risk",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            Status = "Draft",
            Priority = "Medium",
            Statement = "Goal fixture",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        });
        var objectives = new InMemoryObjectiveRepository(new ObjectiveAggregate
        {
            Id = "obj-parent-type-1",
            ParentGoalId = "goal-parent-type-1",
            Name = "Objective Parent Type",
            EntityScope = "Enterprise"
        });
        var initiatives = new InMemoryInitiativeLinkRepository(new InitiativeStrategyLinkAggregate
        {
            Id = "init-link-parent-type-1",
            InitiativeId = "init-parent-type-1",
            InitiativeName = "Initiative Parent Type",
            ParentObjectiveId = "obj-parent-type-1",
            ParentGoalId = "goal-parent-type-1",
            EntityScope = "Enterprise",
            Type = "Growth Initiative",
            NormalizedType = "growth",
            StrategyLinkStatus = "Linked"
        });
        var library = new InMemoryStrategyLibraryRepository(
            goalTemplates:
            [
                new GoalTemplate { Id = "goal-template-risk-parent", Category = "Risk" }
            ],
            initiativeTemplates:
            [
                new InitiativeTemplate
                {
                    Id = "initiative-template-growth-parent",
                    ParentGoalTemplateId = "goal-template-risk-parent",
                    Type = "Growth Initiative",
                    NormalizedType = "growth",
                    EntityScope = "Enterprise",
                    LifecycleStatus = "Published"
                }
            ],
            projectTemplates:
            [
                new ProjectTemplate
                {
                    Id = "project-template-growth-parent",
                    Name = "Growth Project Template",
                    ParentInitiativeTemplateId = "initiative-template-growth-parent",
                    EntityScope = "Enterprise",
                    LifecycleStatus = "Published",
                    DeliveryType = "Technology"
                }
            ]);

        var service = new ProjectOrchestrationService(
            new MockPpmProjectReadAdapter(),
            new InMemoryPpmProjectCacheRepository(),
            new InMemoryProjectLinkRepository(),
            initiatives,
            objectives,
            goals,
            library,
            new NoOpEnterpriseStrategyAuditSink());

        var result = await service.CreateAsync(new ProjectStrategyLinkViewDto
        {
            ParentInitiativeId = "init-parent-type-1",
            CreationMode = "Template",
            SourceTemplateId = "project-template-growth-parent",
            Status = "Draft"
        }, "tester", "corr");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("project-template-growth-parent", result.Data!.SourceTemplateId);
        Assert.Equal("goal-parent-type-1", result.Data.ParentGoalId);
        Assert.Equal("Growth Initiative", result.Data.ParentType);
        Assert.Equal("Technology", result.Data.DeliveryType);
    }

    [Fact]
    public async Task Project_Template_And_Client_Payload_Cannot_Overwrite_Locked_Lineage()
    {
        var goals = new InMemoryGoalRepository(new GoalAggregate
        {
            Id = "goal-lock-1",
            Name = "Goal Lock",
            Category = "Risk",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            Status = "Draft",
            Priority = "Medium",
            Statement = "Goal fixture",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise",
            EntityScope = "Enterprise"
        });
        var objectives = new InMemoryObjectiveRepository(new ObjectiveAggregate
        {
            Id = "obj-lock-1",
            ParentGoalId = "goal-lock-1",
            Name = "Objective Lock",
            EntityScope = "Enterprise"
        });
        var initiatives = new InMemoryInitiativeLinkRepository(new InitiativeStrategyLinkAggregate
        {
            Id = "init-link-lock-1",
            InitiativeId = "init-lock-1",
            InitiativeName = "Initiative Lock",
            ParentObjectiveId = "obj-lock-1",
            ParentGoalId = "goal-lock-1",
            EntityScope = "Enterprise",
            Type = "Growth Initiative",
            NormalizedType = "growth",
            StrategyLinkStatus = "Linked"
        });
        var library = new InMemoryStrategyLibraryRepository(
            initiativeTemplates:
            [
                new InitiativeTemplate
                {
                    Id = "initiative-template-lock-1",
                    Type = "Growth Initiative",
                    NormalizedType = "growth",
                    EntityScope = "Enterprise",
                    LifecycleStatus = "Published"
                }
            ],
            projectTemplates:
            [
                new ProjectTemplate
                {
                    Id = "project-template-lock-1",
                    Name = "Lock Template",
                    ParentInitiativeTemplateId = "initiative-template-lock-1",
                    EntityScope = "Enterprise",
                    LifecycleStatus = "Published",
                    DeliveryType = "Operations"
                }
            ]);

        var service = new ProjectOrchestrationService(
            new MockPpmProjectReadAdapter(),
            new InMemoryPpmProjectCacheRepository(),
            new InMemoryProjectLinkRepository(),
            initiatives,
            objectives,
            goals,
            library,
            new NoOpEnterpriseStrategyAuditSink());

        var result = await service.CreateAsync(new ProjectStrategyLinkViewDto
        {
            ParentInitiativeId = "init-lock-1",
            ParentObjectiveName = "Wrong Objective",
            ParentGoalName = "Wrong Goal",
            ParentType = "Wrong Type",
            EntityScope = "Wrong Scope",
            CreationMode = "Template",
            SourceTemplateId = "project-template-lock-1",
            Status = "Draft"
        }, "tester", "corr");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("init-lock-1", result.Data!.ParentInitiativeId);
        Assert.Equal("Objective Lock", result.Data.ParentObjectiveName);
        Assert.Equal("Goal Lock", result.Data.ParentGoalName);
        Assert.Equal("Growth Initiative", result.Data.ParentType);
        Assert.Equal("Enterprise", result.Data.EntityScope);
        Assert.Equal("project-template-lock-1", result.Data.SourceTemplateId);
        Assert.Equal("Operations", result.Data.DeliveryType);
    }

    [Fact]
    public async Task NonDraft_Project_With_Budget_Required_Must_Capture_Budget_Owner_And_Approval_Route()
    {
        var goals = new InMemoryGoalRepository(new GoalAggregate
        {
            Id = "goal-budget-guard-1",
            Name = "Goal Budget Guard",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            Status = "Draft",
            Priority = "Medium",
            Statement = "Goal fixture",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        });
        var objectives = new InMemoryObjectiveRepository(new ObjectiveAggregate
        {
            Id = "obj-budget-guard-1",
            ParentGoalId = "goal-budget-guard-1",
            Name = "Objective Budget Guard",
            EntityScope = "Enterprise"
        });
        var initiatives = new InMemoryInitiativeLinkRepository(new InitiativeStrategyLinkAggregate
        {
            Id = "init-link-budget-guard-1",
            InitiativeId = "init-budget-guard-1",
            InitiativeName = "Initiative Budget Guard",
            ParentObjectiveId = "obj-budget-guard-1",
            ParentGoalId = "goal-budget-guard-1",
            EntityScope = "Enterprise",
            Type = "Growth",
            StrategyLinkStatus = "Linked"
        });
        var service = new ProjectOrchestrationService(
            new MockPpmProjectReadAdapter(),
            new InMemoryPpmProjectCacheRepository(),
            new InMemoryProjectLinkRepository(),
            initiatives,
            objectives,
            goals,
            new InMemoryStrategyLibraryRepository(),
            new NoOpEnterpriseStrategyAuditSink());

        var result = await service.CreateAsync(new ProjectStrategyLinkViewDto
        {
            ParentInitiativeId = "init-budget-guard-1",
            ProjectName = "Budget Governed Project",
            Description = "Needs finance governance",
            OwnerPm = "usr-pm",
            Sponsor = "usr-sponsor",
            DeliveryCompanyId = "cmp-001",
            ScopeSummary = "Budget-governed scope",
            Status = "Planned",
            Phase = "Plan",
            DeliveryType = "Technology",
            DeliveryMethodology = "Hybrid",
            Priority = "High",
            StartDate = new DateTime(2027, 1, 10),
            EndDate = new DateTime(2027, 4, 10),
            ReadinessStatus = "Ready",
            RiskRating = "Medium",
            BudgetRequired = true,
            BudgetAmount = 50000,
            CurrencyCode = "USD",
            BudgetType = "CapEx",
            BudgetBasis = "Estimate"
        }, "tester", "corr");

        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.ValidationError, result.Error?.Code);
        Assert.Contains("budgetOwner", result.Error?.Details.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("approvalRoute", result.Error?.Details.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Initiative_Stale_Write_Rejected()
    {
        var goals = new InMemoryGoalRepository(new GoalAggregate
        {
            Id = "goal-1",
            Name = "G1",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            Status = "Draft",
            Priority = "Medium",
            Statement = "Fixture goal",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        });
        var objectives = new InMemoryObjectiveRepository(new ObjectiveAggregate { Id = "obj-1", ParentGoalId = "goal-1", Name = "O1" });
        var links = new InMemoryInitiativeLinkRepository(new InitiativeStrategyLinkAggregate
        {
            Id = "lnk-1",
            InitiativeId = "init-001",
            ParentObjectiveId = "obj-1",
            ParentGoalId = "goal-1",
            Version = 2,
            StrategyLinkStatus = "Linked"
        });
        var service = new InitiativeOrchestrationService(new MockPpmInitiativeReadAdapter(), new InMemoryPpmInitiativeCacheRepository(), links, new InMemoryProjectLinkRepository(), objectives, goals, new NoOpEnterpriseStrategyAuditSink());

        var result = await service.UpsertStrategyLinkAsync("init-001", new InitiativeStrategyLinkViewDto
        {
            InitiativeId = "init-001",
            ParentObjectiveId = "obj-1",
            StrategyLinkStatus = "Linked",
            Version = 1,
            SponsoringCompanyId = "cmp-001"
        }, expectedVersion: 1, actor: "tester", correlationId: "corr");

        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.StaleVersion, result.Error?.Code);
    }

    [Fact]
    public async Task Connection_Graph_Validation_Rejects_Cycle()
    {
        var goals = new InMemoryGoalRepository(new GoalAggregate
        {
            Id = "goal-1",
            Name = "G1",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            Status = "Draft",
            Priority = "Medium",
            Statement = "Fixture goal",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2028, 12, 31),
            ScopeMode = "Enterprise"
        });
        var objectives = new InMemoryObjectiveRepository(new ObjectiveAggregate { Id = "obj-1", ParentGoalId = "goal-1", Name = "O1" });
        var connections = new InMemoryConnectionRepository(
            new StrategyConnectionAggregate { Id = "c1", FromType = "Goal", FromId = "goal-1", ToType = "Objective", ToId = "obj-1", Status = "Active" },
            new StrategyConnectionAggregate { Id = "c2", FromType = "Objective", FromId = "obj-1", ToType = "Goal", ToId = "goal-1", Status = "Active" }
        );
        var service = new ConnectionService(connections, goals, objectives, new NoOpEnterpriseStrategyAuditSink());
        var result = await service.ValidateGraphAsync();
        Assert.False(result.Success);
        Assert.Equal(EnterpriseStrategyErrorCodes.ValidationError, result.Error?.Code);
    }

    [Fact]
    public async Task Ppm_Adapter_Contract_Exposes_Required_Fields()
    {
        var adapter = new MockPpmProjectReadAdapter();
        var rows = await adapter.ListAsync(1, 10);
        var first = rows.First();
        Assert.False(string.IsNullOrWhiteSpace(first.ProjectId));
        Assert.False(string.IsNullOrWhiteSpace(first.ProjectName));
        Assert.False(string.IsNullOrWhiteSpace(first.SourceSystem));
    }

    [Fact]
    public async Task EndToEnd_Lineage_Flow_Covers_Goal_To_Project()
    {
        var goals = new InMemoryGoalRepository();
        var objectives = new InMemoryObjectiveRepository();
        var initiativeLinks = new InMemoryInitiativeLinkRepository();
        var projectLinks = new InMemoryProjectLinkRepository();
        var connections = new InMemoryConnectionRepository();
        var audit = new NoOpEnterpriseStrategyAuditSink();

        var strategyPeriods = BuildActiveStrategyPeriods();
        var goalService = new GoalService(goals, objectives, initiativeLinks, projectLinks, strategyPeriods, audit);
        var objectiveService = new ObjectiveService(objectives, goals, strategyPeriods, initiativeLinks, projectLinks, audit);
        var connectionService = new ConnectionService(connections, goals, objectives, audit);
        var initiativeService = new InitiativeOrchestrationService(new MockPpmInitiativeReadAdapter(), new InMemoryPpmInitiativeCacheRepository(), initiativeLinks, projectLinks, objectives, goals, audit);
        var projectService = new ProjectOrchestrationService(new MockPpmProjectReadAdapter(), new InMemoryPpmProjectCacheRepository(), projectLinks, initiativeLinks, objectives, goals, new InMemoryStrategyLibraryRepository(), audit);

        var createGoal = await goalService.CreateAsync(new GoalDto
        {
            Id = "goal-e2e",
            Name = "Revenue Growth",
            Category = "Growth",
            OwnerId = "usr-ceo",
            Owner = "Chief Executive Officer",
            OwnerRole = "usr-ceo",
            OwnerCompanyId = ActiveCompanyId,
            StrategicThemeId = StrategicThemeId,
            Status = "Draft",
            Priority = "Medium",
            Statement = "Improve growth through operational performance.",
            StrategyPeriodId = ActiveStrategyPeriodId,
            ApplicabilityMode = "Enterprise",
            PlanningHorizonStart = new DateTime(2027, 1, 1),
            PlanningHorizonEnd = new DateTime(2030, 12, 31),
            ScopeMode = "Enterprise",
            Metrics = new()
            {
                new GoalMetricDto
                {
                    Id = "gm-1",
                    MetricName = "Revenue",
                    BaselineValue = 10,
                    TargetValue = 20,
                    MetricType = "%",
                    UnitOfMeasure = "Percentage",
                    AggregationMethod = "Sum",
                    MetricOrigin = "Local",
                    MetricRole = "Strategic",
                    RestrictionMode = "GoalGovernedStructure",
                    SortOrder = 1,
                    MetricBindingStatus = "Bound",
                    YearlyValues = Enumerable.Range(2027, 4).Select(y => new GoalMetricYearValueDto { Year = y, TargetValue = 10 }).ToList()
                },
                new GoalMetricDto
                {
                    Id = "gm-2",
                    MetricName = "Margin",
                    BaselineValue = 5,
                    TargetValue = 8,
                    MetricType = "%",
                    UnitOfMeasure = "Percentage",
                    AggregationMethod = "Sum",
                    MetricOrigin = "Local",
                    MetricRole = "Strategic",
                    RestrictionMode = "GoalGovernedStructure",
                    SortOrder = 2,
                    MetricBindingStatus = "Bound",
                    YearlyValues = Enumerable.Range(2027, 4).Select(y => new GoalMetricYearValueDto { Year = y, TargetValue = 5 }).ToList()
                }
            }
        }, "tester", "corr");
        Assert.True(createGoal.Success);

        var createObjective = await objectiveService.CreateAsync(BuildValidObjective(
            id: "obj-e2e",
            parentGoalId: "goal-e2e",
            name: "Improve retention",
            startYear: 2027,
            endYear: 2030), "tester", "corr");
        Assert.True(createObjective.Success);

        var createConnection = await connectionService.CreateAsync(new StrategyConnectionDto
        {
            Id = "conn-e2e",
            FromType = "Goal",
            FromId = "goal-e2e",
            ToType = "Objective",
            ToId = "obj-e2e",
            Status = "Active",
            ContributionType = "Supports",
            RelationshipType = "Supports"
        }, "tester", "corr");
        Assert.True(createConnection.Success);

        var linkInitiative = await initiativeService.UpsertStrategyLinkAsync("init-001", new InitiativeStrategyLinkViewDto
        {
            InitiativeId = "init-001",
            ParentObjectiveId = "obj-e2e",
            StrategyLinkStatus = "Linked",
            ContributionType = "Direct",
            ContributionWeight = 60,
            SponsoringCompanyId = "cmp-001"
        }, 0, "tester", "corr");
        Assert.True(linkInitiative.Success);

        var linkProject = await projectService.UpsertStrategyLinkAsync("prj-001", new ProjectStrategyLinkViewDto
        {
            ProjectId = "prj-001",
            ParentInitiativeId = "init-001",
            StrategyLinkStatus = "Linked",
            ContributionNote = "Execution package",
            DeliveryCompanyId = "cmp-001"
        }, 0, "tester", "corr");
        Assert.True(linkProject.Success);

        var lineage = await projectService.UpstreamLineageAsync("prj-001");
        Assert.True(lineage.Success);
        Assert.Contains("goal-e2e", lineage.Data!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("obj-e2e", lineage.Data!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("init-001", lineage.Data!, StringComparison.OrdinalIgnoreCase);
    }

    private static GoalService BuildGoalService(InMemoryGoalRepository goals)
        => new(goals, new InMemoryObjectiveRepository(), new InMemoryInitiativeLinkRepository(), new InMemoryProjectLinkRepository(), BuildActiveStrategyPeriods(), new NoOpEnterpriseStrategyAuditSink());

    private static PlanningCycleService BuildPlanningCycleService(
        InMemoryPlanningCycleRepository planningCycles,
        InMemoryStrategyPeriodRepository? strategyPeriods = null,
        InMemoryGoalRepository? goals = null,
        InMemoryObjectiveRepository? objectives = null,
        InMemoryInitiativeLinkRepository? initiativeLinks = null)
        => new(
            planningCycles,
            strategyPeriods ?? new InMemoryStrategyPeriodRepository(),
            goals ?? new InMemoryGoalRepository(),
            objectives ?? new InMemoryObjectiveRepository(),
            initiativeLinks ?? new InMemoryInitiativeLinkRepository());

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
            OwnerPositionId = "pos-coo",
            CurrentOwnerPersonId = "usr-coo",
            Owner = "usr-coo",
            ExecutiveSponsor = "usr-ceo",
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

    private static InMemoryStrategyPeriodRepository BuildActiveStrategyPeriods()
        => new(new StrategyPeriodAggregate
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
}

sealed class InMemoryGoalRepository : IGoalRepository
{
    private readonly List<GoalAggregate> _masters = new();
    private readonly List<GoalMetric> _metrics = new();
    private readonly List<GoalMetricYearValue> _yearlyTargets = new();
    private readonly List<GoalYearlyBudgetEnvelope> _budgets = new();

    public InMemoryGoalRepository(params GoalAggregate[] rows)
    {
        foreach (var row in rows)
            SaveGraph(row, isUpdate: false);
    }

    public IReadOnlyList<GoalMetric> PersistedMetrics => _metrics.Select(CloneMetric).ToList();
    public IReadOnlyList<GoalMetricYearValue> PersistedMetricYearlyTargets => _yearlyTargets.Select(CloneYear).ToList();
    public IReadOnlyList<GoalYearlyBudgetEnvelope> PersistedBudgetEnvelopes => _budgets.Select(CloneBudget).ToList();

    public Task<GoalAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var row = _masters.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(row is null ? null : Compose(row));
    }

    public Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_masters.Any(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<GoalAggregate>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<GoalAggregate>>(_masters.Select(Compose).ToList());

    public Task AddAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default)
    {
        SaveGraph(aggregate, isUpdate: false);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default)
    {
        SaveGraph(aggregate, isUpdate: true);
        return Task.CompletedTask;
    }

    private void SaveGraph(GoalAggregate aggregate, bool isUpdate)
    {
        var backupMasters = _masters.Select(CloneMasterWithoutChildren).ToList();
        var backupMetrics = _metrics.Select(CloneMetric).ToList();
        var backupYears = _yearlyTargets.Select(CloneYear).ToList();
        var backupBudgets = _budgets.Select(CloneBudget).ToList();

        try
        {
            var master = CloneMasterWithoutChildren(aggregate);
            var idx = _masters.FindIndex(x => string.Equals(x.Id, aggregate.Id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) _masters[idx] = master;
            else _masters.Add(master);

            var oldMetricIds = _metrics.Where(x => string.Equals(x.GoalId, aggregate.Id, StringComparison.OrdinalIgnoreCase)).Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            _metrics.RemoveAll(x => string.Equals(x.GoalId, aggregate.Id, StringComparison.OrdinalIgnoreCase));
            _yearlyTargets.RemoveAll(x => oldMetricIds.Contains(x.GoalMetricId));
            _budgets.RemoveAll(x => string.Equals(x.GoalId, aggregate.Id, StringComparison.OrdinalIgnoreCase));

            foreach (var metric in aggregate.Metrics ?? new List<GoalMetric>())
            {
                var metricClone = CloneMetric(metric);
                metricClone.GoalId = aggregate.Id;
                _metrics.Add(metricClone);
                foreach (var row in metric.YearlyTargets ?? new List<GoalMetricYearValue>())
                {
                    var yearClone = CloneYear(row);
                    yearClone.GoalMetricId = metricClone.Id;
                    _yearlyTargets.Add(yearClone);
                }
            }

            foreach (var budget in aggregate.YearlyBudgets ?? new List<GoalYearlyBudgetEnvelope>())
            {
                var budgetClone = CloneBudget(budget);
                budgetClone.GoalId = aggregate.Id;
                _budgets.Add(budgetClone);
            }
        }
        catch
        {
            _masters.Clear();
            _masters.AddRange(backupMasters);
            _metrics.Clear();
            _metrics.AddRange(backupMetrics);
            _yearlyTargets.Clear();
            _yearlyTargets.AddRange(backupYears);
            _budgets.Clear();
            _budgets.AddRange(backupBudgets);
            throw;
        }
    }

    private GoalAggregate Compose(GoalAggregate master)
    {
        var row = CloneMasterWithoutChildren(master);
        var metrics = _metrics
            .Where(x => string.Equals(x.GoalId, row.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.SortOrder)
            .Select(CloneMetric)
            .ToList();
        var yearlyByMetricId = _yearlyTargets
            .GroupBy(x => x.GoalMetricId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Year).Select(CloneYear).ToList(), StringComparer.OrdinalIgnoreCase);
        foreach (var metric in metrics)
            metric.YearlyTargets = yearlyByMetricId.TryGetValue(metric.Id, out var years) ? years : new List<GoalMetricYearValue>();
        row.Metrics = metrics;
        row.YearlyBudgets = _budgets
            .Where(x => string.Equals(x.GoalId, row.Id, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Year)
            .Select(CloneBudget)
            .ToList();
        return row;
    }

    private static GoalAggregate CloneMasterWithoutChildren(GoalAggregate source) => new()
    {
        Id = source.Id,
        GoalId = source.GoalId,
        GoalTitle = source.GoalTitle,
        Category = source.Category,
        GoalStatement = source.GoalStatement,
        Status = source.Status,
        Priority = source.Priority,
        StrategyPeriodId = source.StrategyPeriodId,
        StartDate = source.StartDate,
        EndDate = source.EndDate,
        OwnerRole = source.OwnerRole,
        OwnerCompanyId = source.OwnerCompanyId,
        OwnerPersonId = source.OwnerPersonId,
        RelatedEntityScope = source.RelatedEntityScope,
        ApplicabilityMode = source.ApplicabilityMode,
        AppliesToAllCompanies = source.AppliesToAllCompanies,
        ApplicableCompanyIds = source.ApplicableCompanyIds?.ToList() ?? new(),
        ChangeLogRef = source.ChangeLogRef,
        DecisionReference = source.DecisionReference,
        EvidenceLink = source.EvidenceLink,
        Version = source.Version,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        ArchivedAt = source.ArchivedAt,
        CreatedBy = source.CreatedBy,
        UpdatedBy = source.UpdatedBy,
        SourceTemplateType = source.SourceTemplateType,
        SourceTemplateId = source.SourceTemplateId,
        SourceTemplateVersion = source.SourceTemplateVersion,
        SourceBlueprintPackId = source.SourceBlueprintPackId,
        InstantiationBatchId = source.InstantiationBatchId,
        CreatedFromLibrary = source.CreatedFromLibrary,
        Metrics = new(),
        YearlyBudgets = new()
    };

    private static GoalMetric CloneMetric(GoalMetric source) => new()
    {
        Id = source.Id,
        GoalId = source.GoalId,
        MetricAssignmentId = source.MetricAssignmentId,
        MetricDefinitionId = source.MetricDefinitionId,
        MetricName = source.MetricName,
        MetricType = source.MetricType,
        UnitOfMeasure = source.UnitOfMeasure,
        AggregationMethod = source.AggregationMethod,
        DirectionPolarity = source.DirectionPolarity,
        ThresholdModel = source.ThresholdModel,
        ReportingFrequency = source.ReportingFrequency,
        BaselineValue = source.BaselineValue,
        TargetValue = source.TargetValue,
        CascadeMetric = source.CascadeMetric,
        MetricOrigin = source.MetricOrigin,
        MetricRole = source.MetricRole,
        RestrictionMode = source.RestrictionMode,
        RollupEligible = source.RollupEligible,
        SortOrder = source.SortOrder,
        MetricBindingStatus = source.MetricBindingStatus,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        YearlyTargets = (source.YearlyTargets ?? new()).Select(CloneYear).ToList()
    };

    private static GoalMetricYearValue CloneYear(GoalMetricYearValue source) => new()
    {
        GoalMetricId = source.GoalMetricId,
        Year = source.Year,
        BaselineValue = source.BaselineValue,
        TargetValue = source.TargetValue,
        ActualValue = source.ActualValue,
        ForecastValue = source.ForecastValue,
        ThresholdMin = source.ThresholdMin,
        ThresholdMax = source.ThresholdMax,
        Commentary = source.Commentary
    };

    private static GoalYearlyBudgetEnvelope CloneBudget(GoalYearlyBudgetEnvelope source) => new()
    {
        GoalId = source.GoalId,
        Year = source.Year,
        RevenueTarget = source.RevenueTarget,
        EbitdaTarget = source.EbitdaTarget,
        CapexEnvelope = source.CapexEnvelope,
        OpexEnvelope = source.OpexEnvelope,
        SavingsTarget = source.SavingsTarget,
        FundingPool = source.FundingPool,
        Commentary = source.Commentary
    };
}

sealed class InMemoryStrategyLibraryRepository : IStrategyLibraryRepository
{
    private readonly List<GoalTemplate> _goalTemplates = new();
    private readonly List<InitiativeTemplate> _initiativeTemplates = new();
    private readonly List<ProjectTemplate> _projectTemplates = new();
    private readonly Dictionary<string, List<ProjectTemplateMetric>> _projectTemplateMetrics = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryStrategyLibraryRepository(
        IEnumerable<GoalTemplate>? goalTemplates = null,
        IEnumerable<InitiativeTemplate>? initiativeTemplates = null,
        IEnumerable<ProjectTemplate>? projectTemplates = null,
        IEnumerable<ProjectTemplateMetric>? projectTemplateMetrics = null)
    {
        if (goalTemplates is not null) _goalTemplates.AddRange(goalTemplates);
        if (initiativeTemplates is not null) _initiativeTemplates.AddRange(initiativeTemplates);
        if (projectTemplates is not null) _projectTemplates.AddRange(projectTemplates);
        if (projectTemplateMetrics is not null)
        {
            foreach (var metric in projectTemplateMetrics)
            {
                if (!_projectTemplateMetrics.TryGetValue(metric.ProjectTemplateId, out var list))
                {
                    list = new List<ProjectTemplateMetric>();
                    _projectTemplateMetrics[metric.ProjectTemplateId] = list;
                }
                list.Add(metric);
            }
        }
    }

    public Task<TemplateImportBatch?> GetImportBatchAsync(string batchId, CancellationToken cancellationToken = default) => Task.FromResult<TemplateImportBatch?>(null);
    public Task<IReadOnlyList<TemplateImportIssue>> ListImportIssuesAsync(string batchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TemplateImportIssue>>(Array.Empty<TemplateImportIssue>());
    public Task UpsertImportBatchAsync(TemplateImportBatch batch, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task UpsertImportIssuesAsync(IReadOnlyList<TemplateImportIssue> issues, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<GoalTemplate>> ListGoalTemplatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GoalTemplate>>(_goalTemplates.ToList());
    public Task<GoalTemplate?> GetGoalTemplateAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_goalTemplates.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)));
    public Task UpsertGoalTemplatesAsync(IReadOnlyList<GoalTemplate> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceGoalTemplateMetricsAsync(string goalTemplateId, IReadOnlyList<GoalTemplateMetric> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<GoalTemplateMetric>> ListGoalTemplateMetricsAsync(string goalTemplateId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GoalTemplateMetric>>(Array.Empty<GoalTemplateMetric>());
    public Task<IReadOnlyList<ObjectiveTemplate>> ListObjectiveTemplatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ObjectiveTemplate>>(Array.Empty<ObjectiveTemplate>());
    public Task<ObjectiveTemplate?> GetObjectiveTemplateAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult<ObjectiveTemplate?>(null);
    public Task UpsertObjectiveTemplatesAsync(IReadOnlyList<ObjectiveTemplate> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceObjectiveTemplateMetricsAsync(string objectiveTemplateId, IReadOnlyList<ObjectiveTemplateMetric> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<ObjectiveTemplateMetric>> ListObjectiveTemplateMetricsAsync(string objectiveTemplateId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ObjectiveTemplateMetric>>(Array.Empty<ObjectiveTemplateMetric>());
    public Task<IReadOnlyList<InitiativeTemplate>> ListInitiativeTemplatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InitiativeTemplate>>(_initiativeTemplates.ToList());
    public Task<InitiativeTemplate?> GetInitiativeTemplateAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_initiativeTemplates.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)));
    public Task UpsertInitiativeTemplatesAsync(IReadOnlyList<InitiativeTemplate> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceInitiativeTemplateMetricsAsync(string initiativeTemplateId, IReadOnlyList<InitiativeTemplateMetric> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<InitiativeTemplateMetric>> ListInitiativeTemplateMetricsAsync(string initiativeTemplateId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<InitiativeTemplateMetric>>(Array.Empty<InitiativeTemplateMetric>());
    public Task<IReadOnlyList<ProjectTemplate>> ListProjectTemplatesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProjectTemplate>>(_projectTemplates.ToList());
    public Task<ProjectTemplate?> GetProjectTemplateAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_projectTemplates.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)));
    public Task UpsertProjectTemplatesAsync(IReadOnlyList<ProjectTemplate> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task ReplaceProjectTemplateMetricsAsync(string projectTemplateId, IReadOnlyList<ProjectTemplateMetric> rows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<ProjectTemplateMetric>> ListProjectTemplateMetricsAsync(string projectTemplateId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ProjectTemplateMetric>>(_projectTemplateMetrics.TryGetValue(projectTemplateId, out var rows) ? rows.ToList() : Array.Empty<ProjectTemplateMetric>());
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

sealed class InMemoryObjectiveRepository : IObjectiveRepository
{
    private readonly List<ObjectiveAggregate> _rows = new();
    public InMemoryObjectiveRepository(params ObjectiveAggregate[] rows) => _rows.AddRange(rows);
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

sealed class InMemoryStrategyPeriodRepository : IStrategyPeriodRepository
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

sealed class InMemoryPlanningCycleRepository : IPlanningCycleRepository
{
    private readonly List<PlanningCycleAggregate> _rows = new();
    public InMemoryPlanningCycleRepository(params PlanningCycleAggregate[] rows) => _rows.AddRange(rows);
    public Task<PlanningCycleAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => x.Id == id));
    public Task<PlanningCycleAggregate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) => Task.FromResult(_rows.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)));
    public Task<IReadOnlyList<PlanningCycleAggregate>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PlanningCycleAggregate>>(_rows.ToList());
    public Task AddAsync(PlanningCycleAggregate aggregate, CancellationToken cancellationToken = default) { _rows.Add(aggregate); return Task.CompletedTask; }
    public Task UpdateAsync(PlanningCycleAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var idx = _rows.FindIndex(x => x.Id == aggregate.Id);
        if (idx >= 0) _rows[idx] = aggregate;
        return Task.CompletedTask;
    }
}

sealed class InMemoryInitiativeLinkRepository : IInitiativeStrategyLinkRepository
{
    private readonly List<InitiativeStrategyLinkAggregate> _rows = new();
    public InMemoryInitiativeLinkRepository(params InitiativeStrategyLinkAggregate[] rows) => _rows.AddRange(rows);
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

sealed class InMemoryProjectLinkRepository : IProjectStrategyLinkRepository
{
    private readonly List<ProjectStrategyLinkAggregate> _rows = new();
    public InMemoryProjectLinkRepository(params ProjectStrategyLinkAggregate[] rows) => _rows.AddRange(rows);
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

sealed class InMemoryPpmInitiativeCacheRepository : IPpmInitiativeCacheRepository
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

sealed class InMemoryPpmProjectCacheRepository : IPpmProjectCacheRepository
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

sealed class InMemoryConnectionRepository : IStrategyConnectionRepository
{
    private readonly List<StrategyConnectionAggregate> _rows = new();
    public InMemoryConnectionRepository(params StrategyConnectionAggregate[] rows) => _rows.AddRange(rows);
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

sealed class ThrowingPpmInitiativeReadAdapter : IPpmInitiativeReadAdapter
{
    public Task<PpmInitiativeReadModel?> GetByIdAsync(string initiativeId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ppm down");
    public Task<IReadOnlyList<PpmInitiativeReadModel>> ListAsync(int page, int pageSize, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ppm down");
    public Task<IReadOnlyList<PpmInitiativeReadModel>> SyncAsync(string correlationId, CancellationToken cancellationToken = default) => throw new InvalidOperationException("ppm down");
}
