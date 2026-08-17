using Diten.Application.EnterpriseStrategy.Repositories;
using Diten.Domain.Aggregates.EnterpriseStrategy;
using Diten.Persistence.Context;
using MongoDB.Driver;

namespace Diten.Persistence.Repositories;

public sealed class KpiScorecardRepository : IKpiScorecardRepository
{
    private readonly IMongoCollection<KpiTemplateAggregate> _templates;
    private readonly IMongoCollection<KpiThresholdModelAggregate> _thresholds;
    private readonly IMongoCollection<KpiScorecardPackAggregate> _packs;
    private readonly IMongoCollection<KpiScorecardPackItemAggregate> _packItems;
    private readonly IMongoCollection<KpiCatalogItemAggregate> _runtimeKpis;
    private readonly IMongoCollection<KpiGovernanceActionAggregate> _governanceActions;

    public KpiScorecardRepository(MongoDbContext context)
    {
        _templates = context.GetCollection<KpiTemplateAggregate>(nameof(KpiTemplateAggregate));
        _thresholds = context.GetCollection<KpiThresholdModelAggregate>(nameof(KpiThresholdModelAggregate));
        _packs = context.GetCollection<KpiScorecardPackAggregate>(nameof(KpiScorecardPackAggregate));
        _packItems = context.GetCollection<KpiScorecardPackItemAggregate>(nameof(KpiScorecardPackItemAggregate));
        _runtimeKpis = context.GetCollection<KpiCatalogItemAggregate>(nameof(KpiCatalogItemAggregate));
        _governanceActions = context.GetCollection<KpiGovernanceActionAggregate>(nameof(KpiGovernanceActionAggregate));
    }

    public async Task<IReadOnlyList<KpiTemplateAggregate>> ListKpiTemplatesAsync(CancellationToken cancellationToken = default) =>
        await _templates
            .Find(x => x.TemplateCode != null && x.TemplateCode != string.Empty)
            .ToListAsync(cancellationToken);

    public async Task<KpiTemplateAggregate?> GetKpiTemplateAsync(string id, CancellationToken cancellationToken = default) =>
        await _templates.Find(x => x.Id == id || x.TemplateCode == id).FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertKpiTemplatesAsync(IReadOnlyList<KpiTemplateAggregate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
            await _templates.ReplaceOneAsync(x => x.TemplateCode == row.TemplateCode, row, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<IReadOnlyList<KpiThresholdModelAggregate>> ListThresholdModelsAsync(CancellationToken cancellationToken = default) =>
        await _thresholds.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<KpiThresholdModelAggregate?> GetThresholdModelAsync(string idOrCode, CancellationToken cancellationToken = default) =>
        await _thresholds.Find(x => x.Id == idOrCode || x.ModelCode == idOrCode).FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertThresholdModelsAsync(IReadOnlyList<KpiThresholdModelAggregate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
            await _thresholds.ReplaceOneAsync(x => x.ModelCode == row.ModelCode, row, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<IReadOnlyList<KpiScorecardPackAggregate>> ListScorecardPacksAsync(CancellationToken cancellationToken = default) =>
        await _packs.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<KpiScorecardPackAggregate?> GetScorecardPackAsync(string id, CancellationToken cancellationToken = default) =>
        await _packs.Find(x => x.Id == id || x.PackCode == id).FirstOrDefaultAsync(cancellationToken);

    public async Task UpsertScorecardPacksAsync(IReadOnlyList<KpiScorecardPackAggregate> rows, CancellationToken cancellationToken = default)
    {
        foreach (var row in rows)
            await _packs.ReplaceOneAsync(x => x.PackCode == row.PackCode, row, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<IReadOnlyList<KpiScorecardPackItemAggregate>> ListScorecardPackItemsAsync(string packId, CancellationToken cancellationToken = default) =>
        await _packItems.Find(x => x.PackId == packId || x.PackCode == packId).SortBy(x => x.DisplayOrder).ToListAsync(cancellationToken);

    public async Task ReplaceScorecardPackItemsAsync(string packId, IReadOnlyList<KpiScorecardPackItemAggregate> rows, CancellationToken cancellationToken = default)
    {
        var packCode = rows.FirstOrDefault()?.PackCode ?? packId;
        await _packItems.DeleteManyAsync(x => x.PackId == packId || x.PackCode == packCode, cancellationToken);
        foreach (var row in rows)
        {
            await _packItems.ReplaceOneAsync(
                x => x.PackCode == row.PackCode && x.KpiTemplateCode == row.KpiTemplateCode,
                row,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }
    }

    public async Task<IReadOnlyList<KpiCatalogItemAggregate>> ListRuntimeKpisAsync(CancellationToken cancellationToken = default) =>
        await _runtimeKpis.Find(_ => true).ToListAsync(cancellationToken);

    public async Task<KpiCatalogItemAggregate?> GetRuntimeKpiAsync(string id, CancellationToken cancellationToken = default) =>
        await _runtimeKpis.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public Task AddRuntimeKpiAsync(KpiCatalogItemAggregate row, CancellationToken cancellationToken = default) =>
        _runtimeKpis.InsertOneAsync(row, cancellationToken: cancellationToken);

    public Task UpdateRuntimeKpiAsync(KpiCatalogItemAggregate row, CancellationToken cancellationToken = default) =>
        _runtimeKpis.ReplaceOneAsync(x => x.Id == row.Id, row, new ReplaceOptions { IsUpsert = false }, cancellationToken);

    public Task AddGovernanceActionAsync(KpiGovernanceActionAggregate row, CancellationToken cancellationToken = default) =>
        _governanceActions.InsertOneAsync(row, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<KpiGovernanceActionAggregate>> ListGovernanceActionsAsync(CancellationToken cancellationToken = default) =>
        await _governanceActions.Find(_ => true).SortByDescending(x => x.At).ToListAsync(cancellationToken);
}
