using Diten.Domain.Aggregates.EnterpriseStrategy;
using Diten.Persistence.Context;
using Diten.Application.EnterpriseStrategy.Shared;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace Diten.Persistence.EnterpriseStrategy;

internal static class StrategyLibraryMongoMigration
{
    public static async Task EnsureAppliedAsync(MongoDbContext context, CancellationToken cancellationToken = default)
    {
        var database = context.GetDatabase();
        var existingCollections = await (await database.ListCollectionNamesAsync(cancellationToken: cancellationToken)).ToListAsync(cancellationToken);
        var existingSet = new HashSet<string>(existingCollections, StringComparer.OrdinalIgnoreCase);

        var requiredCollections = new[]
        {
            nameof(GoalTemplate),
            nameof(GoalTemplateMetric),
            nameof(ObjectiveTemplate),
            nameof(ObjectiveTemplateMetric),
            nameof(InitiativeTemplate),
            nameof(InitiativeTemplateMetric),
            nameof(ProjectTemplate),
            nameof(ProjectTemplateMetric),
            nameof(TemplateImportBatch),
            nameof(TemplateImportIssue),
            nameof(TemplateUsageStat),
            nameof(KpiTemplateAggregate),
            nameof(StrategyBlueprintPack),
            nameof(StrategyBlueprintPackItem),
            nameof(TemplateVersion),
            nameof(TemplatePublishHistory),
            nameof(InstantiationBatch),
            nameof(InstantiationRecord),
            nameof(TemplateOverrideLog)
        };

        foreach (var collectionName in requiredCollections)
        {
            if (existingSet.Contains(collectionName)) continue;
            try
            {
                await database.CreateCollectionAsync(collectionName, cancellationToken: cancellationToken);
            }
            catch (MongoCommandException ex) when (string.Equals(ex.CodeName, "NamespaceExists", StringComparison.OrdinalIgnoreCase))
            {
                // Another node/process may have created it first; treat as success.
            }
        }

        var goalTemplateDocuments = database.GetCollection<BsonDocument>(nameof(GoalTemplate));
        var legacyCategoryWithoutTypeFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Exists("Category", true),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Exists("Type", false),
                Builders<BsonDocument>.Filter.Eq("Type", BsonNull.Value),
                Builders<BsonDocument>.Filter.Eq("Type", string.Empty)));
        var renameCategoryToTypePipeline = PipelineDefinition<BsonDocument, BsonDocument>.Create(new[]
        {
            new BsonDocument("$set", new BsonDocument("Type", "$Category")),
            new BsonDocument("$unset", "Category")
        });
        await goalTemplateDocuments.UpdateManyAsync(
            legacyCategoryWithoutTypeFilter,
            renameCategoryToTypePipeline,
            cancellationToken: cancellationToken);

        var removeLegacyCategoryFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Exists("Category", true),
            Builders<BsonDocument>.Filter.Exists("Type", true));
        await goalTemplateDocuments.UpdateManyAsync(
            removeLegacyCategoryFilter,
            Builders<BsonDocument>.Update.Unset("Category"),
            cancellationToken: cancellationToken);

        foreach (var legacyAlias in GoalTemplateTypeCatalog.LegacyAliases)
        {
            var legacyTypeFilter = Builders<BsonDocument>.Filter.Regex(
                "Type",
                new BsonRegularExpression($"^{Regex.Escape(legacyAlias.Key)}$", "i"));
            await goalTemplateDocuments.UpdateManyAsync(
                legacyTypeFilter,
                Builders<BsonDocument>.Update.Set("Type", legacyAlias.Value),
                cancellationToken: cancellationToken);
        }

        await context.GetCollection<GoalTemplateMetric>(nameof(GoalTemplateMetric)).Indexes.CreateOneAsync(
            new CreateIndexModel<GoalTemplateMetric>(
                Builders<GoalTemplateMetric>.IndexKeys.Ascending(x => x.GoalTemplateId),
                new CreateIndexOptions { Name = "ix_goal_template_metric_goal_template_id" }),
            cancellationToken: cancellationToken);

        await context.GetCollection<ObjectiveTemplateMetric>(nameof(ObjectiveTemplateMetric)).Indexes.CreateOneAsync(
            new CreateIndexModel<ObjectiveTemplateMetric>(
                Builders<ObjectiveTemplateMetric>.IndexKeys.Ascending(x => x.ObjectiveTemplateId),
                new CreateIndexOptions { Name = "ix_objective_template_metric_objective_template_id" }),
            cancellationToken: cancellationToken);

        await context.GetCollection<InitiativeTemplateMetric>(nameof(InitiativeTemplateMetric)).Indexes.CreateOneAsync(
            new CreateIndexModel<InitiativeTemplateMetric>(
                Builders<InitiativeTemplateMetric>.IndexKeys.Ascending(x => x.InitiativeTemplateId),
                new CreateIndexOptions { Name = "ix_initiative_template_metric_initiative_template_id" }),
            cancellationToken: cancellationToken);

        await context.GetCollection<ProjectTemplateMetric>(nameof(ProjectTemplateMetric)).Indexes.CreateOneAsync(
            new CreateIndexModel<ProjectTemplateMetric>(
                Builders<ProjectTemplateMetric>.IndexKeys.Ascending(x => x.ProjectTemplateId),
                new CreateIndexOptions { Name = "ix_project_template_metric_project_template_id" }),
            cancellationToken: cancellationToken);

        await context.GetCollection<TemplateImportIssue>(nameof(TemplateImportIssue)).Indexes.CreateOneAsync(
            new CreateIndexModel<TemplateImportIssue>(
                Builders<TemplateImportIssue>.IndexKeys.Ascending(x => x.BatchId),
                new CreateIndexOptions { Name = "ix_template_import_issue_batch_id" }),
            cancellationToken: cancellationToken);

        await context.GetCollection<StrategyBlueprintPackItem>(nameof(StrategyBlueprintPackItem)).Indexes.CreateOneAsync(
            new CreateIndexModel<StrategyBlueprintPackItem>(
                Builders<StrategyBlueprintPackItem>.IndexKeys.Ascending(x => x.BlueprintPackId),
                new CreateIndexOptions { Name = "ix_blueprint_pack_item_blueprint_pack_id" }),
            cancellationToken: cancellationToken);

        await context.GetCollection<TemplateVersion>(nameof(TemplateVersion)).Indexes.CreateOneAsync(
            new CreateIndexModel<TemplateVersion>(
                Builders<TemplateVersion>.IndexKeys
                    .Ascending(x => x.TemplateType)
                    .Ascending(x => x.TemplateId),
                new CreateIndexOptions { Name = "ix_template_version_template_key" }),
            cancellationToken: cancellationToken);

        await context.GetCollection<TemplateUsageStat>(nameof(TemplateUsageStat)).Indexes.CreateOneAsync(
            new CreateIndexModel<TemplateUsageStat>(
                Builders<TemplateUsageStat>.IndexKeys
                    .Ascending(x => x.ItemType)
                    .Ascending(x => x.ItemId),
                new CreateIndexOptions { Name = "ix_template_usage_stat_item_key" }),
            cancellationToken: cancellationToken);

        await context.GetCollection<InstantiationRecord>(nameof(InstantiationRecord)).Indexes.CreateOneAsync(
            new CreateIndexModel<InstantiationRecord>(
                Builders<InstantiationRecord>.IndexKeys.Ascending(x => x.InstantiationBatchId),
                new CreateIndexOptions { Name = "ix_instantiation_record_batch_id" }),
            cancellationToken: cancellationToken);
    }
}
