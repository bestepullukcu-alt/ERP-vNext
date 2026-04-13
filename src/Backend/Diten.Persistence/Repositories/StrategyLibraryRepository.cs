using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Domain.Aggregates.EnterpriseStrategy;
using Diten.Persistence.Context;
using MongoDB.Driver;

namespace Diten.Persistence.Repositories;

public sealed class StrategyLibraryRepository : IStrategyLibraryRepository
{
    private readonly IMongoCollection<TemplateImportBatch> _importBatches;
    private readonly IMongoCollection<TemplateImportIssue> _importIssues;
    private readonly IMongoCollection<GoalTemplate> _goalTemplates;
    private readonly IMongoCollection<GoalTemplateMetric> _goalTemplateMetrics;
    private readonly IMongoCollection<ObjectiveTemplate> _objectiveTemplates;
    private readonly IMongoCollection<ObjectiveTemplateMetric> _objectiveTemplateMetrics;
    private readonly IMongoCollection<InitiativeTemplate> _initiativeTemplates;
    private readonly IMongoCollection<InitiativeTemplateMetric> _initiativeTemplateMetrics;
    private readonly IMongoCollection<ProjectTemplate> _projectTemplates;
    private readonly IMongoCollection<ProjectTemplateMetric> _projectTemplateMetrics;
    private readonly IMongoCollection<StrategyBlueprintPack> _blueprintPacks;
    private readonly IMongoCollection<StrategyBlueprintPackItem> _blueprintPackItems;
    private readonly IMongoCollection<TemplateVersion> _templateVersions;
    private readonly IMongoCollection<TemplatePublishHistory> _publishHistory;
    private readonly IMongoCollection<InstantiationBatch> _instantiationBatches;
    private readonly IMongoCollection<InstantiationRecord> _instantiationRecords;
    private readonly IMongoCollection<TemplateOverrideLog> _overrideLogs;
    private readonly IMongoCollection<TemplateUsageStat> _usageStats;

    public StrategyLibraryRepository(MongoDbContext context)
    {
        _importBatches = context.GetCollection<TemplateImportBatch>(nameof(TemplateImportBatch));
        _importIssues = context.GetCollection<TemplateImportIssue>(nameof(TemplateImportIssue));
        _goalTemplates = context.GetCollection<GoalTemplate>(nameof(GoalTemplate));
        _goalTemplateMetrics = context.GetCollection<GoalTemplateMetric>(nameof(GoalTemplateMetric));
        _objectiveTemplates = context.GetCollection<ObjectiveTemplate>(nameof(ObjectiveTemplate));
        _objectiveTemplateMetrics = context.GetCollection<ObjectiveTemplateMetric>(nameof(ObjectiveTemplateMetric));
        _initiativeTemplates = context.GetCollection<InitiativeTemplate>(nameof(InitiativeTemplate));
        _initiativeTemplateMetrics = context.GetCollection<InitiativeTemplateMetric>(nameof(InitiativeTemplateMetric));
        _projectTemplates = context.GetCollection<ProjectTemplate>(nameof(ProjectTemplate));
        _projectTemplateMetrics = context.GetCollection<ProjectTemplateMetric>(nameof(ProjectTemplateMetric));
        _blueprintPacks = context.GetCollection<StrategyBlueprintPack>(nameof(StrategyBlueprintPack));
        _blueprintPackItems = context.GetCollection<StrategyBlueprintPackItem>(nameof(StrategyBlueprintPackItem));
        _templateVersions = context.GetCollection<TemplateVersion>(nameof(TemplateVersion));
        _publishHistory = context.GetCollection<TemplatePublishHistory>(nameof(TemplatePublishHistory));
        _instantiationBatches = context.GetCollection<InstantiationBatch>(nameof(InstantiationBatch));
        _instantiationRecords = context.GetCollection<InstantiationRecord>(nameof(InstantiationRecord));
        _overrideLogs = context.GetCollection<TemplateOverrideLog>(nameof(TemplateOverrideLog));
        _usageStats = context.GetCollection<TemplateUsageStat>(nameof(TemplateUsageStat));
    }

    public async Task<TemplateImportBatch?> GetImportBatchAsync(string batchId, CancellationToken cancellationToken = default) =>
        await _importBatches.Find(x => x.Id == batchId).FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TemplateImportIssue>> ListImportIssuesAsync(string batchId, CancellationToken cancellationToken = default) =>
        await _importIssues.Find(x => x.BatchId == batchId).ToListAsync(cancellationToken);

    public async Task UpsertImportBatchAsync(TemplateImportBatch batch, CancellationToken cancellationToken = default) =>
        await _importBatches.ReplaceOneAsync(x => x.Id == batch.Id, batch, new ReplaceOptions { IsUpsert = true }, cancellationToken);

    public async Task UpsertImportIssuesAsync(IReadOnlyList<TemplateImportIssue> issues, CancellationToken cancellationToken = default)
    {
        if (issues.Count == 0) return;
        var batchId = issues[0].BatchId;
        await _importIssues.DeleteManyAsync(x => x.BatchId == batchId, cancellationToken);
        await _importIssues.InsertManyAsync(issues, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<GoalTemplate>> ListGoalTemplatesAsync(CancellationToken cancellationToken = default) =>
        await _goalTemplates.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<GoalTemplate?> GetGoalTemplateAsync(string id, CancellationToken cancellationToken = default) =>
        await _goalTemplates.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertGoalTemplatesAsync(IReadOnlyList<GoalTemplate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
            await _goalTemplates.ReplaceOneAsync(x => x.Id == row.Id, row, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task ReplaceGoalTemplateMetricsAsync(string goalTemplateId, IReadOnlyList<GoalTemplateMetric> rows, CancellationToken cancellationToken = default)
    {
        await _goalTemplateMetrics.DeleteManyAsync(x => x.GoalTemplateId == goalTemplateId, cancellationToken);
        if (rows.Count > 0)
            await _goalTemplateMetrics.InsertManyAsync(rows, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<GoalTemplateMetric>> ListGoalTemplateMetricsAsync(string goalTemplateId, CancellationToken cancellationToken = default) =>
        await _goalTemplateMetrics.Find(x => x.GoalTemplateId == goalTemplateId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ObjectiveTemplate>> ListObjectiveTemplatesAsync(CancellationToken cancellationToken = default) =>
        await _objectiveTemplates.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<ObjectiveTemplate?> GetObjectiveTemplateAsync(string id, CancellationToken cancellationToken = default) =>
        await _objectiveTemplates.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertObjectiveTemplatesAsync(IReadOnlyList<ObjectiveTemplate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
            await _objectiveTemplates.ReplaceOneAsync(x => x.Id == row.Id, row, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task ReplaceObjectiveTemplateMetricsAsync(string objectiveTemplateId, IReadOnlyList<ObjectiveTemplateMetric> rows, CancellationToken cancellationToken = default)
    {
        await _objectiveTemplateMetrics.DeleteManyAsync(x => x.ObjectiveTemplateId == objectiveTemplateId, cancellationToken);
        if (rows.Count > 0)
            await _objectiveTemplateMetrics.InsertManyAsync(rows, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ObjectiveTemplateMetric>> ListObjectiveTemplateMetricsAsync(string objectiveTemplateId, CancellationToken cancellationToken = default) =>
        await _objectiveTemplateMetrics.Find(x => x.ObjectiveTemplateId == objectiveTemplateId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<InitiativeTemplate>> ListInitiativeTemplatesAsync(CancellationToken cancellationToken = default) =>
        await _initiativeTemplates.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<InitiativeTemplate?> GetInitiativeTemplateAsync(string id, CancellationToken cancellationToken = default) =>
        await _initiativeTemplates.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertInitiativeTemplatesAsync(IReadOnlyList<InitiativeTemplate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
            await _initiativeTemplates.ReplaceOneAsync(x => x.Id == row.Id, row, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task ReplaceInitiativeTemplateMetricsAsync(string initiativeTemplateId, IReadOnlyList<InitiativeTemplateMetric> rows, CancellationToken cancellationToken = default)
    {
        await _initiativeTemplateMetrics.DeleteManyAsync(x => x.InitiativeTemplateId == initiativeTemplateId, cancellationToken);
        if (rows.Count > 0)
            await _initiativeTemplateMetrics.InsertManyAsync(rows, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<InitiativeTemplateMetric>> ListInitiativeTemplateMetricsAsync(string initiativeTemplateId, CancellationToken cancellationToken = default) =>
        await _initiativeTemplateMetrics.Find(x => x.InitiativeTemplateId == initiativeTemplateId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectTemplate>> ListProjectTemplatesAsync(CancellationToken cancellationToken = default) =>
        await _projectTemplates.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<ProjectTemplate?> GetProjectTemplateAsync(string id, CancellationToken cancellationToken = default) =>
        await _projectTemplates.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertProjectTemplatesAsync(IReadOnlyList<ProjectTemplate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
            await _projectTemplates.ReplaceOneAsync(x => x.Id == row.Id, row, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task ReplaceProjectTemplateMetricsAsync(string projectTemplateId, IReadOnlyList<ProjectTemplateMetric> rows, CancellationToken cancellationToken = default)
    {
        await _projectTemplateMetrics.DeleteManyAsync(x => x.ProjectTemplateId == projectTemplateId, cancellationToken);
        if (rows.Count > 0)
            await _projectTemplateMetrics.InsertManyAsync(rows, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectTemplateMetric>> ListProjectTemplateMetricsAsync(string projectTemplateId, CancellationToken cancellationToken = default) =>
        await _projectTemplateMetrics.Find(x => x.ProjectTemplateId == projectTemplateId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, int>> CountProjectTemplateMetricsByProjectAsync(CancellationToken cancellationToken = default)
    {
        var all = await _projectTemplateMetrics.Find(_ => true).ToListAsync(cancellationToken);
        return all
            .GroupBy(x => x.ProjectTemplateId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<StrategyBlueprintPack>> ListBlueprintPacksAsync(CancellationToken cancellationToken = default) =>
        await _blueprintPacks.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<StrategyBlueprintPack?> GetBlueprintPackAsync(string id, CancellationToken cancellationToken = default) =>
        await _blueprintPacks.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertBlueprintPacksAsync(IReadOnlyList<StrategyBlueprintPack> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
            await _blueprintPacks.ReplaceOneAsync(x => x.Id == row.Id, row, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task ReplaceBlueprintPackItemsAsync(string packId, IReadOnlyList<StrategyBlueprintPackItem> rows, CancellationToken cancellationToken = default)
    {
        await _blueprintPackItems.DeleteManyAsync(x => x.BlueprintPackId == packId, cancellationToken);
        if (rows.Count > 0)
            await _blueprintPackItems.InsertManyAsync(rows, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<StrategyBlueprintPackItem>> ListBlueprintPackItemsAsync(string packId, CancellationToken cancellationToken = default) =>
        await _blueprintPackItems.Find(x => x.BlueprintPackId == packId).ToListAsync(cancellationToken);

    public async Task AddTemplateVersionAsync(TemplateVersion version, CancellationToken cancellationToken = default) =>
        await _templateVersions.InsertOneAsync(version, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<TemplateVersion>> ListTemplateVersionsAsync(string templateType, string templateId, CancellationToken cancellationToken = default) =>
        await _templateVersions.Find(x => x.TemplateType == templateType && x.TemplateId == templateId).SortByDescending(x => x.VersionNumber).ToListAsync(cancellationToken);

    public async Task AddPublishHistoryAsync(TemplatePublishHistory row, CancellationToken cancellationToken = default) =>
        await _publishHistory.InsertOneAsync(row, cancellationToken: cancellationToken);

    public async Task AddInstantiationBatchAsync(InstantiationBatch batch, CancellationToken cancellationToken = default) =>
        await _instantiationBatches.InsertOneAsync(batch, cancellationToken: cancellationToken);

    public async Task AddInstantiationRecordsAsync(IReadOnlyList<InstantiationRecord> rows, CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0) return;
        await _instantiationRecords.InsertManyAsync(rows, cancellationToken: cancellationToken);
    }

    public async Task AddOverrideLogsAsync(IReadOnlyList<TemplateOverrideLog> rows, CancellationToken cancellationToken = default)
    {
        if (rows.Count == 0) return;
        await _overrideLogs.InsertManyAsync(rows, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<InstantiationBatch>> ListInstantiationBatchesAsync(CancellationToken cancellationToken = default) =>
        await _instantiationBatches.Find(_ => true).SortByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);

    public async Task UpsertUsageStatsAsync(IReadOnlyList<TemplateUsageStat> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
        {
            await _usageStats.ReplaceOneAsync(
                x => x.ItemType == row.ItemType && x.ItemId == row.ItemId,
                row,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<TemplateUsageStat>> ListUsageStatsAsync(CancellationToken cancellationToken = default) =>
        await _usageStats.Find(_ => true).ToListAsync(cancellationToken);
}
