using Diten.Domain.Aggregates.EnterpriseStrategy;
using Diten.Application.EnterpriseStrategy.Shared;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Reflection;

namespace Diten.Persistence.Context;

public class MongoDbContext
{
    private readonly IMongoClient _client;
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        EnsureClassMaps();
        _client = new MongoClient(configuration.GetConnectionString("MongoDb"));
        _database = _client.GetDatabase(configuration["DatabaseName"]);
    }

    private static void EnsureClassMaps()
    {
        RegisterIgnoreExtraElementsClassMap<KpiTemplateAggregate>();
        RegisterIgnoreExtraElementsClassMap<StrategyBlueprintPack>();
        RegisterIgnoreExtraElementsClassMap<StrategyBlueprintPackItem>();
        RegisterGoalTemplateClassMap();
        RegisterIgnoreExtraElementsClassMap<GoalTemplateMetric>();
        RegisterIgnoreExtraElementsClassMap<ObjectiveTemplate>();
        RegisterIgnoreExtraElementsClassMap<ObjectiveTemplateMetric>();
        RegisterIgnoreExtraElementsClassMap<InitiativeTemplate>();
        RegisterIgnoreExtraElementsClassMap<InitiativeTemplateMetric>();
        RegisterIgnoreExtraElementsClassMap<ProjectTemplate>();
        RegisterIgnoreExtraElementsClassMap<ProjectTemplateMetric>();
        RegisterIgnoreExtraElementsClassMap<TemplateImportBatch>();
        RegisterIgnoreExtraElementsClassMap<TemplateImportIssue>();
        RegisterIgnoreExtraElementsClassMap<TemplateVersion>();
        RegisterIgnoreExtraElementsClassMap<TemplatePublishHistory>();
        RegisterIgnoreExtraElementsClassMap<InstantiationBatch>();
        RegisterIgnoreExtraElementsClassMap<InstantiationRecord>();
        RegisterIgnoreExtraElementsClassMap<TemplateOverrideLog>();
        RegisterIgnoreExtraElementsClassMap<TemplateUsageStat>();
        RegisterIgnoreExtraElementsClassMap<AuditEvent>();
        RegisterIgnoreExtraElementsClassMap<GoalAggregate>();
        RegisterIgnoreExtraElementsClassMap<GoalMetric>();
        RegisterIgnoreExtraElementsClassMap<GoalMetricYearValue>();
        RegisterIgnoreExtraElementsClassMap<GoalYearlyBudgetEnvelope>();
        RegisterIgnoreExtraElementsClassMap<PlanningCycleAggregate>();
        RegisterIgnoreExtraElementsClassMap<StrategyPeriodAggregate>();
        RegisterIgnoreExtraElementsClassMap<ObjectiveAggregate>();
        RegisterIgnoreExtraElementsClassMap<StrategyConnectionAggregate>();
        RegisterIgnoreExtraElementsClassMap<InitiativeStrategyLinkAggregate>();
        RegisterIgnoreExtraElementsClassMap<ProjectStrategyLinkAggregate>();
        RegisterIgnoreExtraElementsClassMap<PpmInitiativeReadModelAggregate>();
        RegisterIgnoreExtraElementsClassMap<PpmProjectReadModelAggregate>();
    }

    private static void RegisterIgnoreExtraElementsClassMap<T>()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(T))) return;
        BsonClassMap.RegisterClassMap<T>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
        });
    }

    private static void RegisterGoalTemplateClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(GoalTemplate))) return;
        BsonClassMap.RegisterClassMap<GoalTemplate>(cm =>
        {
            cm.AutoMap();
            var legacyCategoryProperty = typeof(GoalTemplate).GetProperty(nameof(GoalTemplate.Category), BindingFlags.Instance | BindingFlags.Public);
            if (legacyCategoryProperty is not null)
            {
                cm.UnmapMember(legacyCategoryProperty);
            }
            cm.SetIgnoreExtraElements(true);
        });
    }

    public IMongoCollection<T> GetCollection<T>(string name)
    {
        return _database.GetCollection<T>(name);
    }

    public IMongoClient GetClient() => _client;
    public IMongoDatabase GetDatabase() => _database;

    /// <summary>Runs a lightweight server ping against the configured database (for health checks).</summary>
    public Task PingAsync(CancellationToken cancellationToken = default) =>
        _database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
}
