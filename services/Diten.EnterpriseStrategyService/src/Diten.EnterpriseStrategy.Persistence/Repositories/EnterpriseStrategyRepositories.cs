using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Domain.Aggregates.EnterpriseStrategy;
using Diten.Persistence.Context;
using MongoDB.Driver;

namespace Diten.Persistence.Repositories;

public sealed class GoalRepository : IGoalRepository
{
    private readonly IMongoClient _client;
    private readonly IMongoCollection<GoalAggregate> _masterCollection;
    private readonly IMongoCollection<GoalMetric> _metricCollection;
    private readonly IMongoCollection<GoalMetricYearValue> _metricYearlyTargetCollection;
    private readonly IMongoCollection<GoalYearlyBudgetEnvelope> _budgetCollection;

    public GoalRepository(MongoDbContext context)
    {
        _client = context.GetClient();
        // Keep master collection name unchanged for backward compatibility with existing data.
        _masterCollection = context.GetCollection<GoalAggregate>(nameof(GoalAggregate));
        _metricCollection = context.GetCollection<GoalMetric>(nameof(StrategicGoalMetric));
        _metricYearlyTargetCollection = context.GetCollection<GoalMetricYearValue>(nameof(StrategicGoalMetricYearlyTarget));
        _budgetCollection = context.GetCollection<GoalYearlyBudgetEnvelope>(nameof(StrategicGoalBudgetEnvelope));
    }

    public async Task<GoalAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var goal = await _masterCollection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);
        if (goal is null) return null;
        return await ComposeAsync(goal, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default) =>
        await _masterCollection.Find(x => x.Id == id).AnyAsync(cancellationToken);

    public async Task<IReadOnlyList<GoalAggregate>> ListAsync(CancellationToken cancellationToken = default)
    {
        var goals = await _masterCollection.Find(_ => true).ToListAsync(cancellationToken);
        if (goals.Count == 0) return goals;

        var goalIds = goals.Select(g => g.Id).ToList();
        var metrics = await _metricCollection.Find(x => goalIds.Contains(x.GoalId)).ToListAsync(cancellationToken);
        var metricIds = metrics.Select(m => m.Id).ToList();
        var yearlyTargets = metricIds.Count == 0
            ? new List<GoalMetricYearValue>()
            : await _metricYearlyTargetCollection.Find(x => metricIds.Contains(x.GoalMetricId)).ToListAsync(cancellationToken);
        var budgets = await _budgetCollection.Find(x => goalIds.Contains(x.GoalId)).ToListAsync(cancellationToken);

        var yearlyByMetricId = yearlyTargets
            .GroupBy(x => x.GoalMetricId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Year).ToList(), StringComparer.OrdinalIgnoreCase);
        var metricsByGoalId = metrics
            .GroupBy(x => x.GoalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.SortOrder).ToList(), StringComparer.OrdinalIgnoreCase);
        var budgetsByGoalId = budgets
            .GroupBy(x => x.GoalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Year).ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var row in goals)
        {
            if (metricsByGoalId.TryGetValue(row.Id, out var rows))
            {
                foreach (var metric in rows)
                    metric.YearlyTargets = yearlyByMetricId.TryGetValue(metric.Id, out var years) ? years : new List<GoalMetricYearValue>();
                row.Metrics = rows;
            }
            else if (row.Metrics is null)
            {
                row.Metrics = new List<GoalMetric>();
            }

            if (budgetsByGoalId.TryGetValue(row.Id, out var budgetRows))
                row.YearlyBudgets = budgetRows;
            else if (row.YearlyBudgets is null)
                row.YearlyBudgets = new List<GoalYearlyBudgetEnvelope>();
        }

        return goals;
    }

    public async Task AddAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var fallbackSnapshot = CaptureSnapshot(aggregate.Id, cancellationToken);
        await SaveGraphAsync(aggregate, isUpdate: false, fallbackSnapshot, cancellationToken);
    }

    public async Task UpdateAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var fallbackSnapshot = CaptureSnapshot(aggregate.Id, cancellationToken);
        await SaveGraphAsync(aggregate, isUpdate: true, fallbackSnapshot, cancellationToken);
    }

    private async Task<GoalAggregate> ComposeAsync(GoalAggregate goal, CancellationToken cancellationToken)
    {
        var metrics = await _metricCollection.Find(x => x.GoalId == goal.Id).SortBy(x => x.SortOrder).ToListAsync(cancellationToken);
        if (metrics.Count > 0)
        {
            var metricIds = metrics.Select(x => x.Id).ToList();
            var yearlyTargets = await _metricYearlyTargetCollection.Find(x => metricIds.Contains(x.GoalMetricId)).ToListAsync(cancellationToken);
            var yearlyByMetricId = yearlyTargets
                .GroupBy(x => x.GoalMetricId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Year).ToList(), StringComparer.OrdinalIgnoreCase);
            foreach (var metric in metrics)
                metric.YearlyTargets = yearlyByMetricId.TryGetValue(metric.Id, out var rows) ? rows : new List<GoalMetricYearValue>();
            goal.Metrics = metrics;
        }

        var budgets = await _budgetCollection.Find(x => x.GoalId == goal.Id).SortBy(x => x.Year).ToListAsync(cancellationToken);
        if (budgets.Count > 0)
            goal.YearlyBudgets = budgets;

        goal.Metrics ??= new List<GoalMetric>();
        goal.YearlyBudgets ??= new List<GoalYearlyBudgetEnvelope>();
        return goal;
    }

    private async Task SaveGraphAsync(
        GoalAggregate aggregate,
        bool isUpdate,
        Task<GoalRepositorySnapshot> fallbackSnapshot,
        CancellationToken cancellationToken)
    {
        // Keep KPI and budget records out of master document to enforce separated persistence model.
        var master = CloneMasterOnly(aggregate);
        var metrics = (aggregate.Metrics ?? new List<GoalMetric>()).Select(CloneMetricWithoutYears).ToList();
        foreach (var metric in metrics)
        {
            metric.GoalId = aggregate.Id;
            metric.MetricAssignmentId = string.IsNullOrWhiteSpace(metric.MetricAssignmentId) ? metric.Id : metric.MetricAssignmentId;
        }

        var yearlyTargets = new List<GoalMetricYearValue>();
        foreach (var metric in aggregate.Metrics ?? new List<GoalMetric>())
        {
            foreach (var row in metric.YearlyTargets ?? new List<GoalMetricYearValue>())
            {
                yearlyTargets.Add(new GoalMetricYearValue
                {
                    GoalMetricId = string.IsNullOrWhiteSpace(row.GoalMetricId) ? metric.Id : row.GoalMetricId,
                    Year = row.Year,
                    TargetValue = row.TargetValue,
                    ThresholdMin = row.ThresholdMin,
                    ThresholdMax = row.ThresholdMax,
                    Commentary = row.Commentary,
                    BaselineValue = row.BaselineValue,
                    ActualValue = row.ActualValue,
                    ForecastValue = row.ForecastValue
                });
            }
        }

        var budgets = (aggregate.YearlyBudgets ?? new List<GoalYearlyBudgetEnvelope>())
            .Select(x => new GoalYearlyBudgetEnvelope
            {
                GoalId = aggregate.Id,
                Year = x.Year,
                RevenueTarget = x.RevenueTarget,
                EbitdaTarget = x.EbitdaTarget,
                CapexEnvelope = x.CapexEnvelope,
                OpexEnvelope = x.OpexEnvelope,
                SavingsTarget = x.SavingsTarget,
                FundingPool = x.FundingPool,
                Commentary = x.Commentary
            })
            .ToList();

        try
        {
            using var session = await _client.StartSessionAsync(cancellationToken: cancellationToken);
            session.StartTransaction();
            await UpsertGraphInternalAsync(session, master, metrics, yearlyTargets, budgets, isUpdate, cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return;
        }
        catch (Exception ex) when (ShouldUseNonTransactionalFallback(ex))
        {
            // Some local/self-hosted Mongo deployments do not support sessions/transactions.
            // Fallback to non-transactional save with compensating rollback.
        }

        var snapshot = await fallbackSnapshot;
        try
        {
            await UpsertGraphInternalAsync(null, master, metrics, yearlyTargets, budgets, isUpdate, cancellationToken);
        }
        catch
        {
            await RestoreSnapshotAsync(snapshot, cancellationToken);
            throw;
        }
    }

    private static bool ShouldUseNonTransactionalFallback(Exception ex)
    {
        static bool HasKnownText(string message) =>
            message.Contains("Transaction numbers are only allowed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("replica set member or mongos", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("sessions are not supported", StringComparison.OrdinalIgnoreCase);

        return ex switch
        {
            MongoCommandException mce => HasKnownText(mce.Message),
            MongoClientException mcl => HasKnownText(mcl.Message),
            NotSupportedException => true,
            InvalidOperationException ioe => HasKnownText(ioe.Message),
            _ => false
        };
    }

    private async Task UpsertGraphInternalAsync(
        IClientSessionHandle? session,
        GoalAggregate master,
        List<GoalMetric> metrics,
        List<GoalMetricYearValue> yearlyTargets,
        List<GoalYearlyBudgetEnvelope> budgets,
        bool isUpdate,
        CancellationToken cancellationToken)
    {
        var previousMetricIds = session is null
            ? (await _metricCollection.Find(x => x.GoalId == master.Id).ToListAsync(cancellationToken)).Select(x => x.Id).ToList()
            : (await _metricCollection.Find(session, x => x.GoalId == master.Id).ToListAsync(cancellationToken)).Select(x => x.Id).ToList();
        var masterMetricFilter = Builders<GoalMetric>.Filter.Eq(x => x.GoalId, master.Id);
        var masterBudgetFilter = Builders<GoalYearlyBudgetEnvelope>.Filter.Eq(x => x.GoalId, master.Id);
        var previousYearlyFilter = Builders<GoalMetricYearValue>.Filter.In(x => x.GoalMetricId, previousMetricIds);

        if (session is null)
        {
            await _masterCollection.ReplaceOneAsync(x => x.Id == master.Id, master, new ReplaceOptions { IsUpsert = !isUpdate }, cancellationToken);
            await _metricCollection.DeleteManyAsync(x => x.GoalId == master.Id, cancellationToken);
            if (previousMetricIds.Count > 0)
                await _metricYearlyTargetCollection.DeleteManyAsync(x => previousMetricIds.Contains(x.GoalMetricId), cancellationToken);
            await _budgetCollection.DeleteManyAsync(x => x.GoalId == master.Id, cancellationToken);
        }
        else
        {
            await _masterCollection.ReplaceOneAsync(session, x => x.Id == master.Id, master, new ReplaceOptions { IsUpsert = !isUpdate }, cancellationToken);
            await _metricCollection.DeleteManyAsync(session, masterMetricFilter, cancellationToken: cancellationToken);
            if (previousMetricIds.Count > 0)
                await _metricYearlyTargetCollection.DeleteManyAsync(session, previousYearlyFilter, cancellationToken: cancellationToken);
            await _budgetCollection.DeleteManyAsync(session, masterBudgetFilter, cancellationToken: cancellationToken);
        }

        if (metrics.Count > 0)
        {
            if (session is null)
                await _metricCollection.InsertManyAsync(metrics, cancellationToken: cancellationToken);
            else
                await _metricCollection.InsertManyAsync(session, metrics, cancellationToken: cancellationToken);
        }

        if (yearlyTargets.Count > 0)
        {
            if (session is null)
                await _metricYearlyTargetCollection.InsertManyAsync(yearlyTargets, cancellationToken: cancellationToken);
            else
                await _metricYearlyTargetCollection.InsertManyAsync(session, yearlyTargets, cancellationToken: cancellationToken);
        }

        if (budgets.Count > 0)
        {
            if (session is null)
                await _budgetCollection.InsertManyAsync(budgets, cancellationToken: cancellationToken);
            else
                await _budgetCollection.InsertManyAsync(session, budgets, cancellationToken: cancellationToken);
        }
    }

    private async Task<GoalRepositorySnapshot> CaptureSnapshot(string goalId, CancellationToken cancellationToken)
    {
        var master = await _masterCollection.Find(x => x.Id == goalId).FirstOrDefaultAsync(cancellationToken);
        var metrics = await _metricCollection.Find(x => x.GoalId == goalId).ToListAsync(cancellationToken);
        var metricIds = metrics.Select(x => x.Id).ToList();
        var yearlyTargets = metricIds.Count == 0
            ? new List<GoalMetricYearValue>()
            : await _metricYearlyTargetCollection.Find(x => metricIds.Contains(x.GoalMetricId)).ToListAsync(cancellationToken);
        var budgets = await _budgetCollection.Find(x => x.GoalId == goalId).ToListAsync(cancellationToken);
        return new GoalRepositorySnapshot(goalId, master, metrics, yearlyTargets, budgets);
    }

    private async Task RestoreSnapshotAsync(GoalRepositorySnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.Master is null)
        {
            await _masterCollection.DeleteOneAsync(x => x.Id == snapshot.GoalId, cancellationToken);
            var orphanMetricIds = (await _metricCollection.Find(x => x.GoalId == snapshot.GoalId).ToListAsync(cancellationToken)).Select(x => x.Id).ToList();
            if (orphanMetricIds.Count > 0)
                await _metricYearlyTargetCollection.DeleteManyAsync(x => orphanMetricIds.Contains(x.GoalMetricId), cancellationToken);
            await _metricCollection.DeleteManyAsync(x => x.GoalId == snapshot.GoalId, cancellationToken);
            await _budgetCollection.DeleteManyAsync(x => x.GoalId == snapshot.GoalId, cancellationToken);
            return;
        }

        await _masterCollection.ReplaceOneAsync(x => x.Id == snapshot.Master.Id, snapshot.Master, new ReplaceOptions { IsUpsert = true }, cancellationToken);
        var existingMetricIds = (await _metricCollection.Find(x => x.GoalId == snapshot.Master.Id).ToListAsync(cancellationToken)).Select(x => x.Id).ToList();
        await _metricCollection.DeleteManyAsync(x => x.GoalId == snapshot.Master.Id, cancellationToken);
        if (snapshot.Metrics.Count > 0)
            await _metricCollection.InsertManyAsync(snapshot.Metrics, cancellationToken: cancellationToken);

        var restoredMetricIds = snapshot.Metrics.Select(x => x.Id).Concat(existingMetricIds).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (restoredMetricIds.Count > 0)
            await _metricYearlyTargetCollection.DeleteManyAsync(x => restoredMetricIds.Contains(x.GoalMetricId), cancellationToken);
        if (snapshot.YearlyTargets.Count > 0)
            await _metricYearlyTargetCollection.InsertManyAsync(snapshot.YearlyTargets, cancellationToken: cancellationToken);

        await _budgetCollection.DeleteManyAsync(x => x.GoalId == snapshot.Master.Id, cancellationToken);
        if (snapshot.Budgets.Count > 0)
            await _budgetCollection.InsertManyAsync(snapshot.Budgets, cancellationToken: cancellationToken);
    }

    private static GoalAggregate CloneMasterOnly(GoalAggregate source) => new()
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
        ApplicableCompanyIds = source.ApplicableCompanyIds?.ToList() ?? new List<string>(),
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
        Metrics = new List<GoalMetric>(),
        YearlyBudgets = new List<GoalYearlyBudgetEnvelope>()
    };

    private static GoalMetric CloneMetricWithoutYears(GoalMetric source) => new()
    {
        Id = source.Id,
        MetricAssignmentId = source.MetricAssignmentId,
        GoalId = source.GoalId,
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
        YearlyTargets = new List<GoalMetricYearValue>()
    };

    private sealed record GoalRepositorySnapshot(
        string GoalId,
        GoalAggregate? Master,
        List<GoalMetric> Metrics,
        List<GoalMetricYearValue> YearlyTargets,
        List<GoalYearlyBudgetEnvelope> Budgets);
}

public sealed class ObjectiveRepository : IObjectiveRepository
{
    private readonly IMongoCollection<ObjectiveAggregate> _collection;

    public ObjectiveRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<ObjectiveAggregate>(nameof(ObjectiveAggregate));
    }

    public async Task<ObjectiveAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ObjectiveAggregate>> ListAsync(CancellationToken cancellationToken = default) =>
        await _collection.Find(_ => true).ToListAsync(cancellationToken);

    public Task AddAsync(ObjectiveAggregate aggregate, CancellationToken cancellationToken = default) =>
        _collection.InsertOneAsync(aggregate, cancellationToken: cancellationToken);

    public Task UpdateAsync(ObjectiveAggregate aggregate, CancellationToken cancellationToken = default) =>
        _collection.ReplaceOneAsync(x => x.Id == aggregate.Id, aggregate, cancellationToken: cancellationToken);
}

public sealed class StrategyConnectionRepository : IStrategyConnectionRepository
{
    private readonly IMongoCollection<StrategyConnectionAggregate> _collection;

    public StrategyConnectionRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<StrategyConnectionAggregate>(nameof(StrategyConnectionAggregate));
    }

    public async Task<StrategyConnectionAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<StrategyConnectionAggregate>> ListAsync(CancellationToken cancellationToken = default) =>
        await _collection.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<StrategyConnectionAggregate?> GetByEdgeAsync(string fromType, string fromId, string toType, string toId, CancellationToken cancellationToken = default) =>
        await _collection.Find(x =>
            x.FromType == fromType &&
            x.FromId == fromId &&
            x.ToType == toType &&
            x.ToId == toId).FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(StrategyConnectionAggregate aggregate, CancellationToken cancellationToken = default) =>
        _collection.InsertOneAsync(aggregate, cancellationToken: cancellationToken);

    public Task UpdateAsync(StrategyConnectionAggregate aggregate, CancellationToken cancellationToken = default) =>
        _collection.ReplaceOneAsync(x => x.Id == aggregate.Id, aggregate, cancellationToken: cancellationToken);

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        _collection.DeleteOneAsync(x => x.Id == id, cancellationToken);
}

public sealed class InitiativeStrategyLinkRepository : IInitiativeStrategyLinkRepository
{
    private readonly IMongoCollection<InitiativeStrategyLinkAggregate> _collection;

    public InitiativeStrategyLinkRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<InitiativeStrategyLinkAggregate>(nameof(InitiativeStrategyLinkAggregate));
    }

    public async Task<IReadOnlyList<InitiativeStrategyLinkAggregate>> ListAsync(CancellationToken cancellationToken = default) =>
        await _collection.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<InitiativeStrategyLinkAggregate?> GetByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.InitiativeId == initiativeId).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<InitiativeStrategyLinkAggregate>> ListByGoalIdAsync(string goalId, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.ParentGoalId == goalId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InitiativeStrategyLinkAggregate>> ListByObjectiveIdAsync(string objectiveId, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.ParentObjectiveId == objectiveId).ToListAsync(cancellationToken);

    public async Task AddOrUpdateAsync(InitiativeStrategyLinkAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await GetByInitiativeIdAsync(aggregate.InitiativeId, cancellationToken);
        if (existing is null)
            await _collection.InsertOneAsync(aggregate, cancellationToken: cancellationToken);
        else
            await _collection.ReplaceOneAsync(x => x.Id == existing.Id, aggregate, cancellationToken: cancellationToken);
    }

    public Task DeleteByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default) =>
        _collection.DeleteOneAsync(x => x.InitiativeId == initiativeId, cancellationToken);
}

public sealed class ProjectStrategyLinkRepository : IProjectStrategyLinkRepository
{
    private readonly IMongoCollection<ProjectStrategyLinkAggregate> _collection;

    public ProjectStrategyLinkRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<ProjectStrategyLinkAggregate>(nameof(ProjectStrategyLinkAggregate));
    }

    public async Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListAsync(CancellationToken cancellationToken = default) =>
        await _collection.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<ProjectStrategyLinkAggregate?> GetByProjectIdAsync(string projectId, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.ProjectId == projectId).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListByGoalIdAsync(string goalId, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.ParentGoalId == goalId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListByObjectiveIdAsync(string objectiveId, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.ParentObjectiveId == objectiveId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.ParentInitiativeId == initiativeId).ToListAsync(cancellationToken);

    public async Task AddOrUpdateAsync(ProjectStrategyLinkAggregate aggregate, CancellationToken cancellationToken = default)
    {
        var existing = await GetByProjectIdAsync(aggregate.ProjectId, cancellationToken);
        if (existing is null)
            await _collection.InsertOneAsync(aggregate, cancellationToken: cancellationToken);
        else
            await _collection.ReplaceOneAsync(x => x.Id == existing.Id, aggregate, cancellationToken: cancellationToken);
    }

    public Task DeleteByProjectIdAsync(string projectId, CancellationToken cancellationToken = default) =>
        _collection.DeleteOneAsync(x => x.ProjectId == projectId, cancellationToken);
}

public sealed class PpmInitiativeCacheRepository : IPpmInitiativeCacheRepository
{
    private readonly IMongoCollection<PpmInitiativeReadModelAggregate> _collection;
    public PpmInitiativeCacheRepository(MongoDbContext context) => _collection = context.GetCollection<PpmInitiativeReadModelAggregate>(nameof(PpmInitiativeReadModelAggregate));
    public async Task<IReadOnlyList<PpmInitiativeReadModelAggregate>> ListAsync(CancellationToken cancellationToken = default) => await _collection.Find(_ => true).ToListAsync(cancellationToken);
    public async Task<PpmInitiativeReadModelAggregate?> GetByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default) => await _collection.Find(x => x.InitiativeId == initiativeId).FirstOrDefaultAsync(cancellationToken);
    public async Task UpsertManyAsync(IReadOnlyList<PpmInitiativeReadModelAggregate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
            await _collection.ReplaceOneAsync(x => x.InitiativeId == row.InitiativeId, row, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }
}

public sealed class PpmProjectCacheRepository : IPpmProjectCacheRepository
{
    private readonly IMongoCollection<PpmProjectReadModelAggregate> _collection;
    public PpmProjectCacheRepository(MongoDbContext context) => _collection = context.GetCollection<PpmProjectReadModelAggregate>(nameof(PpmProjectReadModelAggregate));
    public async Task<IReadOnlyList<PpmProjectReadModelAggregate>> ListAsync(CancellationToken cancellationToken = default) => await _collection.Find(_ => true).ToListAsync(cancellationToken);
    public async Task<PpmProjectReadModelAggregate?> GetByProjectIdAsync(string projectId, CancellationToken cancellationToken = default) => await _collection.Find(x => x.ProjectId == projectId).FirstOrDefaultAsync(cancellationToken);
    public async Task UpsertManyAsync(IReadOnlyList<PpmProjectReadModelAggregate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
            await _collection.ReplaceOneAsync(x => x.ProjectId == row.ProjectId, row, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }
}

public sealed class PlanningCycleRepository : IPlanningCycleRepository
{
    private readonly IMongoCollection<PlanningCycleAggregate> _collection;

    public PlanningCycleRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<PlanningCycleAggregate>(nameof(PlanningCycleAggregate));
    }

    public async Task<PlanningCycleAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<PlanningCycleAggregate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.Code == code).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PlanningCycleAggregate>> ListAsync(CancellationToken cancellationToken = default) =>
        await _collection.Find(_ => true).ToListAsync(cancellationToken);

    public Task AddAsync(PlanningCycleAggregate aggregate, CancellationToken cancellationToken = default) =>
        _collection.InsertOneAsync(aggregate, cancellationToken: cancellationToken);

    public Task UpdateAsync(PlanningCycleAggregate aggregate, CancellationToken cancellationToken = default) =>
        _collection.ReplaceOneAsync(x => x.Id == aggregate.Id, aggregate, cancellationToken: cancellationToken);
}

public sealed class StrategyPeriodRepository : IStrategyPeriodRepository
{
    private readonly IMongoCollection<StrategyPeriodAggregate> _collection;

    public StrategyPeriodRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<StrategyPeriodAggregate>(nameof(StrategyPeriodAggregate));
    }

    public async Task<StrategyPeriodAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<StrategyPeriodAggregate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.Code == code).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<StrategyPeriodAggregate>> ListAsync(CancellationToken cancellationToken = default) =>
        await _collection.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StrategyPeriodAggregate>> ListByPlanningCycleIdAsync(string planningCycleId, CancellationToken cancellationToken = default) =>
        await _collection.Find(x => x.PlanningCycleId == planningCycleId).ToListAsync(cancellationToken);

    public Task AddAsync(StrategyPeriodAggregate aggregate, CancellationToken cancellationToken = default) =>
        _collection.InsertOneAsync(aggregate, cancellationToken: cancellationToken);

    public Task UpdateAsync(StrategyPeriodAggregate aggregate, CancellationToken cancellationToken = default) =>
        _collection.ReplaceOneAsync(x => x.Id == aggregate.Id, aggregate, cancellationToken: cancellationToken);
}
