using Diten.Domain.Aggregates.EnterpriseStrategy;

namespace Diten.Application.EnterpriseStrategy.Repositories;

public interface IGoalRepository
{
    Task<GoalAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoalAggregate>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default);
    Task UpdateAsync(GoalAggregate aggregate, CancellationToken cancellationToken = default);
}

public interface IObjectiveRepository
{
    Task<ObjectiveAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ObjectiveAggregate>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ObjectiveAggregate aggregate, CancellationToken cancellationToken = default);
    Task UpdateAsync(ObjectiveAggregate aggregate, CancellationToken cancellationToken = default);
}

public interface IStrategyConnectionRepository
{
    Task<StrategyConnectionAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StrategyConnectionAggregate>> ListAsync(CancellationToken cancellationToken = default);
    Task<StrategyConnectionAggregate?> GetByEdgeAsync(string fromType, string fromId, string toType, string toId, CancellationToken cancellationToken = default);
    Task AddAsync(StrategyConnectionAggregate aggregate, CancellationToken cancellationToken = default);
    Task UpdateAsync(StrategyConnectionAggregate aggregate, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}

public interface IInitiativeStrategyLinkRepository
{
    Task<IReadOnlyList<InitiativeStrategyLinkAggregate>> ListAsync(CancellationToken cancellationToken = default);
    Task<InitiativeStrategyLinkAggregate?> GetByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InitiativeStrategyLinkAggregate>> ListByGoalIdAsync(string goalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InitiativeStrategyLinkAggregate>> ListByObjectiveIdAsync(string objectiveId, CancellationToken cancellationToken = default);
    Task AddOrUpdateAsync(InitiativeStrategyLinkAggregate aggregate, CancellationToken cancellationToken = default);
    Task DeleteByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default);
}

public interface IProjectStrategyLinkRepository
{
    Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListAsync(CancellationToken cancellationToken = default);
    Task<ProjectStrategyLinkAggregate?> GetByProjectIdAsync(string projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListByGoalIdAsync(string goalId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListByObjectiveIdAsync(string objectiveId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectStrategyLinkAggregate>> ListByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default);
    Task AddOrUpdateAsync(ProjectStrategyLinkAggregate aggregate, CancellationToken cancellationToken = default);
    Task DeleteByProjectIdAsync(string projectId, CancellationToken cancellationToken = default);
}

public interface IPpmInitiativeCacheRepository
{
    Task<IReadOnlyList<PpmInitiativeReadModelAggregate>> ListAsync(CancellationToken cancellationToken = default);
    Task<PpmInitiativeReadModelAggregate?> GetByInitiativeIdAsync(string initiativeId, CancellationToken cancellationToken = default);
    Task UpsertManyAsync(IReadOnlyList<PpmInitiativeReadModelAggregate> rows, CancellationToken cancellationToken = default);
}

public interface IPpmProjectCacheRepository
{
    Task<IReadOnlyList<PpmProjectReadModelAggregate>> ListAsync(CancellationToken cancellationToken = default);
    Task<PpmProjectReadModelAggregate?> GetByProjectIdAsync(string projectId, CancellationToken cancellationToken = default);
    Task UpsertManyAsync(IReadOnlyList<PpmProjectReadModelAggregate> rows, CancellationToken cancellationToken = default);
}

public interface IPlanningCycleRepository
{
    Task<PlanningCycleAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<PlanningCycleAggregate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlanningCycleAggregate>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(PlanningCycleAggregate aggregate, CancellationToken cancellationToken = default);
    Task UpdateAsync(PlanningCycleAggregate aggregate, CancellationToken cancellationToken = default);
}

public interface IStrategyPeriodRepository
{
    Task<StrategyPeriodAggregate?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<StrategyPeriodAggregate?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StrategyPeriodAggregate>> ListAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StrategyPeriodAggregate>> ListByPlanningCycleIdAsync(string planningCycleId, CancellationToken cancellationToken = default);
    Task AddAsync(StrategyPeriodAggregate aggregate, CancellationToken cancellationToken = default);
    Task UpdateAsync(StrategyPeriodAggregate aggregate, CancellationToken cancellationToken = default);
}

public interface IStrategyLibraryRepository
{
    Task<TemplateImportBatch?> GetImportBatchAsync(string batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemplateImportIssue>> ListImportIssuesAsync(string batchId, CancellationToken cancellationToken = default);
    Task UpsertImportBatchAsync(TemplateImportBatch batch, CancellationToken cancellationToken = default);
    Task UpsertImportIssuesAsync(IReadOnlyList<TemplateImportIssue> issues, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GoalTemplate>> ListGoalTemplatesAsync(CancellationToken cancellationToken = default);
    Task<GoalTemplate?> GetGoalTemplateAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertGoalTemplatesAsync(IReadOnlyList<GoalTemplate> rows, CancellationToken cancellationToken = default);
    Task ReplaceGoalTemplateMetricsAsync(string goalTemplateId, IReadOnlyList<GoalTemplateMetric> rows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoalTemplateMetric>> ListGoalTemplateMetricsAsync(string goalTemplateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ObjectiveTemplate>> ListObjectiveTemplatesAsync(CancellationToken cancellationToken = default);
    Task<ObjectiveTemplate?> GetObjectiveTemplateAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertObjectiveTemplatesAsync(IReadOnlyList<ObjectiveTemplate> rows, CancellationToken cancellationToken = default);
    Task ReplaceObjectiveTemplateMetricsAsync(string objectiveTemplateId, IReadOnlyList<ObjectiveTemplateMetric> rows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ObjectiveTemplateMetric>> ListObjectiveTemplateMetricsAsync(string objectiveTemplateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InitiativeTemplate>> ListInitiativeTemplatesAsync(CancellationToken cancellationToken = default);
    Task<InitiativeTemplate?> GetInitiativeTemplateAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertInitiativeTemplatesAsync(IReadOnlyList<InitiativeTemplate> rows, CancellationToken cancellationToken = default);
    Task ReplaceInitiativeTemplateMetricsAsync(string initiativeTemplateId, IReadOnlyList<InitiativeTemplateMetric> rows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InitiativeTemplateMetric>> ListInitiativeTemplateMetricsAsync(string initiativeTemplateId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectTemplate>> ListProjectTemplatesAsync(CancellationToken cancellationToken = default);
    Task<ProjectTemplate?> GetProjectTemplateAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertProjectTemplatesAsync(IReadOnlyList<ProjectTemplate> rows, CancellationToken cancellationToken = default);
    Task ReplaceProjectTemplateMetricsAsync(string projectTemplateId, IReadOnlyList<ProjectTemplateMetric> rows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectTemplateMetric>> ListProjectTemplateMetricsAsync(string projectTemplateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> CountProjectTemplateMetricsByProjectAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StrategyBlueprintPack>> ListBlueprintPacksAsync(CancellationToken cancellationToken = default);
    Task<StrategyBlueprintPack?> GetBlueprintPackAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertBlueprintPacksAsync(IReadOnlyList<StrategyBlueprintPack> rows, CancellationToken cancellationToken = default);
    Task ReplaceBlueprintPackItemsAsync(string packId, IReadOnlyList<StrategyBlueprintPackItem> rows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StrategyBlueprintPackItem>> ListBlueprintPackItemsAsync(string packId, CancellationToken cancellationToken = default);

    Task AddTemplateVersionAsync(TemplateVersion version, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemplateVersion>> ListTemplateVersionsAsync(string templateType, string templateId, CancellationToken cancellationToken = default);
    Task AddPublishHistoryAsync(TemplatePublishHistory row, CancellationToken cancellationToken = default);

    Task AddInstantiationBatchAsync(InstantiationBatch batch, CancellationToken cancellationToken = default);
    Task AddInstantiationRecordsAsync(IReadOnlyList<InstantiationRecord> rows, CancellationToken cancellationToken = default);
    Task AddOverrideLogsAsync(IReadOnlyList<TemplateOverrideLog> rows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InstantiationBatch>> ListInstantiationBatchesAsync(CancellationToken cancellationToken = default);

    Task UpsertUsageStatsAsync(IReadOnlyList<TemplateUsageStat> rows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TemplateUsageStat>> ListUsageStatsAsync(CancellationToken cancellationToken = default);
}

public interface IKpiScorecardRepository
{
    Task<IReadOnlyList<KpiTemplateAggregate>> ListKpiTemplatesAsync(CancellationToken cancellationToken = default);
    Task<KpiTemplateAggregate?> GetKpiTemplateAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertKpiTemplatesAsync(IReadOnlyList<KpiTemplateAggregate> rows, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KpiThresholdModelAggregate>> ListThresholdModelsAsync(CancellationToken cancellationToken = default);
    Task<KpiThresholdModelAggregate?> GetThresholdModelAsync(string idOrCode, CancellationToken cancellationToken = default);
    Task UpsertThresholdModelsAsync(IReadOnlyList<KpiThresholdModelAggregate> rows, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KpiScorecardPackAggregate>> ListScorecardPacksAsync(CancellationToken cancellationToken = default);
    Task<KpiScorecardPackAggregate?> GetScorecardPackAsync(string id, CancellationToken cancellationToken = default);
    Task UpsertScorecardPacksAsync(IReadOnlyList<KpiScorecardPackAggregate> rows, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KpiScorecardPackItemAggregate>> ListScorecardPackItemsAsync(string packId, CancellationToken cancellationToken = default);
    Task ReplaceScorecardPackItemsAsync(string packId, IReadOnlyList<KpiScorecardPackItemAggregate> rows, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KpiCatalogItemAggregate>> ListRuntimeKpisAsync(CancellationToken cancellationToken = default);
    Task<KpiCatalogItemAggregate?> GetRuntimeKpiAsync(string id, CancellationToken cancellationToken = default);
    Task AddRuntimeKpiAsync(KpiCatalogItemAggregate row, CancellationToken cancellationToken = default);
    Task UpdateRuntimeKpiAsync(KpiCatalogItemAggregate row, CancellationToken cancellationToken = default);

    Task AddGovernanceActionAsync(KpiGovernanceActionAggregate row, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KpiGovernanceActionAggregate>> ListGovernanceActionsAsync(CancellationToken cancellationToken = default);
}
