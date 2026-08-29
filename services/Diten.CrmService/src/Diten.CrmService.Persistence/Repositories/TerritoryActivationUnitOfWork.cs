using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class TerritoryActivationUnitOfWork : ITerritoryActivationUnitOfWork
{
    private readonly IMongoDatabase _database;

    public TerritoryActivationUnitOfWork(IMongoDatabase database) => _database = database;

    public async Task CommitAsync(
        TerritoryModel model,
        IReadOnlyCollection<TerritoryNode> nodes,
        IReadOnlyCollection<TerritoryResourceAssignment> resourceAssignments,
        TerritoryResourceAssignmentPlanSnapshot? planSnapshot,
        CancellationToken cancellationToken)
    {
        if (!await SupportsTransactionsAsync(cancellationToken))
        {
            await CommitWithCompensationAsync(model, nodes, resourceAssignments, planSnapshot, cancellationToken);
            return;
        }

        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var models = _database.GetCollection<TerritoryModel>("territory_models");
            var nodeCollection = _database.GetCollection<TerritoryNode>("territory_nodes");
            var assignments = _database.GetCollection<TerritoryResourceAssignment>("territory_resource_assignments");

            var modelResult = await models.ReplaceOneAsync(
                session,
                x => x.Id == model.Id && x.TenantId == model.TenantId && !x.IsDeleted,
                model,
                cancellationToken: cancellationToken);
            if (modelResult.MatchedCount != 1)
            {
                throw new InvalidOperationException("The territory model changed before activation committed.");
            }

            if (nodes.Count > 0)
            {
                var writes = nodes.Select(node => new ReplaceOneModel<TerritoryNode>(
                    Builders<TerritoryNode>.Filter.Where(x => x.Id == node.Id && x.TenantId == node.TenantId && !x.IsDeleted),
                    node));
                await nodeCollection.BulkWriteAsync(session, writes, new BulkWriteOptions { IsOrdered = true }, cancellationToken);
            }

            if (resourceAssignments.Count > 0)
            {
                var writes = resourceAssignments.Select(assignment => new ReplaceOneModel<TerritoryResourceAssignment>(
                    Builders<TerritoryResourceAssignment>.Filter.Where(
                        x => x.Id == assignment.Id && x.TenantId == assignment.TenantId && !x.IsDeleted),
                    assignment));
                await assignments.BulkWriteAsync(session, writes, new BulkWriteOptions { IsOrdered = true }, cancellationToken);
            }

            // FU04B: the plan baseline joins the SAME boundary, so it can never outlive a failed activation.
            if (planSnapshot is not null)
            {
                await Snapshots.InsertOneAsync(session, planSnapshot, cancellationToken: cancellationToken);
            }

            await session.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex) when (TransactionUnavailable(ex))
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellationToken);
            }

            await CommitWithCompensationAsync(model, nodes, resourceAssignments, planSnapshot, cancellationToken);
        }
        catch
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellationToken);
            }
            throw;
        }
    }

    public async Task CommitVersionCutoverAsync(
        TerritoryModel targetModel,
        IReadOnlyCollection<TerritoryNode> targetNodes,
        IReadOnlyCollection<TerritoryResourceAssignment> targetResourceAssignments,
        TerritoryResourceAssignmentPlanSnapshot? planSnapshot,
        TerritoryModel sourceModel,
        IReadOnlyCollection<TerritoryNode> sourceNodes,
        IReadOnlyCollection<AccountTerritoryAssignment> endedSourceAssignments,
        IReadOnlyCollection<AccountTerritoryAssignment> createdTargetAssignments,
        CancellationToken cancellationToken)
    {
        if (!await SupportsTransactionsAsync(cancellationToken))
        {
            await CommitVersionCutoverWithCompensationAsync(targetModel, targetNodes, targetResourceAssignments,
                planSnapshot, sourceModel, sourceNodes, endedSourceAssignments, createdTargetAssignments, cancellationToken);
            return;
        }

        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var models = _database.GetCollection<TerritoryModel>("territory_models");
            var nodes = _database.GetCollection<TerritoryNode>("territory_nodes");
            var resources = _database.GetCollection<TerritoryResourceAssignment>("territory_resource_assignments");
            var accounts = _database.GetCollection<AccountTerritoryAssignment>("account_territory_assignments");

            await ReplaceRequiredAsync(session, models, targetModel, cancellationToken);
            await ReplaceRequiredAsync(session, models, sourceModel, cancellationToken);
            await ReplaceManyAsync(session, nodes, targetNodes, cancellationToken);
            await ReplaceManyAsync(session, nodes, sourceNodes, cancellationToken);
            await ReplaceManyAsync(session, resources, targetResourceAssignments, cancellationToken);
            await ReplaceManyAsync(session, accounts, endedSourceAssignments, cancellationToken);
            if (createdTargetAssignments.Count > 0)
                await accounts.InsertManyAsync(session, createdTargetAssignments, cancellationToken: cancellationToken);
            if (planSnapshot is not null)
                await Snapshots.InsertOneAsync(session, planSnapshot, cancellationToken: cancellationToken);

            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task CommitVersionCutoverWithCompensationAsync(
        TerritoryModel targetModel,
        IReadOnlyCollection<TerritoryNode> targetNodes,
        IReadOnlyCollection<TerritoryResourceAssignment> targetResourceAssignments,
        TerritoryResourceAssignmentPlanSnapshot? planSnapshot,
        TerritoryModel sourceModel,
        IReadOnlyCollection<TerritoryNode> sourceNodes,
        IReadOnlyCollection<AccountTerritoryAssignment> endedSourceAssignments,
        IReadOnlyCollection<AccountTerritoryAssignment> createdTargetAssignments,
        CancellationToken cancellationToken)
    {
        var models = _database.GetCollection<TerritoryModel>("territory_models");
        var nodes = _database.GetCollection<TerritoryNode>("territory_nodes");
        var resources = _database.GetCollection<TerritoryResourceAssignment>("territory_resource_assignments");
        var accounts = _database.GetCollection<AccountTerritoryAssignment>("account_territory_assignments");

        var modelIds = new[] { targetModel.Id, sourceModel.Id };
        var originalsModels = await models.Find(x => x.TenantId == targetModel.TenantId && modelIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var allNodes = targetNodes.Concat(sourceNodes).ToList();
        var nodeIds = allNodes.Select(x => x.Id).ToList();
        var resourceIds = targetResourceAssignments.Select(x => x.Id).ToList();
        var endedIds = endedSourceAssignments.Select(x => x.Id).ToList();
        var originalNodes = nodeIds.Count == 0 ? [] : await nodes.Find(x => x.TenantId == targetModel.TenantId && nodeIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var originalResources = resourceIds.Count == 0 ? [] : await resources.Find(x => x.TenantId == targetModel.TenantId && resourceIds.Contains(x.Id)).ToListAsync(cancellationToken);
        var originalAccounts = endedIds.Count == 0 ? [] : await accounts.Find(x => x.TenantId == targetModel.TenantId && endedIds.Contains(x.Id)).ToListAsync(cancellationToken);

        try
        {
            await ReplaceRequiredAsync(models, targetModel, cancellationToken);
            await ReplaceRequiredAsync(models, sourceModel, cancellationToken);
            await ReplaceManyAsync(nodes, targetNodes, cancellationToken);
            await ReplaceManyAsync(nodes, sourceNodes, cancellationToken);
            await ReplaceManyAsync(resources, targetResourceAssignments, cancellationToken);
            await ReplaceManyAsync(accounts, endedSourceAssignments, cancellationToken);
            if (createdTargetAssignments.Count > 0)
                await accounts.InsertManyAsync(createdTargetAssignments, cancellationToken: cancellationToken);
            if (planSnapshot is not null)
                await Snapshots.InsertOneAsync(planSnapshot, cancellationToken: cancellationToken);
        }
        catch
        {
            foreach (var original in originalsModels) await models.ReplaceOneAsync(x => x.Id == original.Id && x.TenantId == original.TenantId, original, cancellationToken: cancellationToken);
            foreach (var original in originalNodes) await nodes.ReplaceOneAsync(x => x.Id == original.Id && x.TenantId == original.TenantId, original, cancellationToken: cancellationToken);
            foreach (var original in originalResources) await resources.ReplaceOneAsync(x => x.Id == original.Id && x.TenantId == original.TenantId, original, cancellationToken: cancellationToken);
            foreach (var original in originalAccounts) await accounts.ReplaceOneAsync(x => x.Id == original.Id && x.TenantId == original.TenantId, original, cancellationToken: cancellationToken);
            var createdIds = createdTargetAssignments.Select(x => x.Id).ToList();
            if (createdIds.Count > 0) await accounts.DeleteManyAsync(x => x.TenantId == targetModel.TenantId && createdIds.Contains(x.Id), cancellationToken);
            if (planSnapshot is not null) await Snapshots.DeleteOneAsync(x => x.Id == planSnapshot.Id && x.TenantId == planSnapshot.TenantId, cancellationToken);
            throw;
        }
    }

    private static async Task ReplaceRequiredAsync<T>(IClientSessionHandle session, IMongoCollection<T> collection, T entity,
        CancellationToken cancellationToken) where T : EntityBase
    {
        var result = await collection.ReplaceOneAsync(session, x => x.Id == entity.Id && x.TenantId == entity.TenantId && !x.IsDeleted,
            entity, cancellationToken: cancellationToken);
        if (result.MatchedCount != 1) throw new InvalidOperationException($"{typeof(T).Name} changed before version cutover committed.");
    }

    private static async Task ReplaceRequiredAsync<T>(IMongoCollection<T> collection, T entity,
        CancellationToken cancellationToken) where T : EntityBase
    {
        var result = await collection.ReplaceOneAsync(x => x.Id == entity.Id && x.TenantId == entity.TenantId && !x.IsDeleted,
            entity, cancellationToken: cancellationToken);
        if (result.MatchedCount != 1) throw new InvalidOperationException($"{typeof(T).Name} changed before version cutover committed.");
    }

    private static Task ReplaceManyAsync<T>(IClientSessionHandle session, IMongoCollection<T> collection,
        IReadOnlyCollection<T> entities, CancellationToken cancellationToken) where T : EntityBase
        => entities.Count == 0 ? Task.CompletedTask : collection.BulkWriteAsync(session,
            entities.Select(entity => new ReplaceOneModel<T>(Builders<T>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId && !x.IsDeleted), entity)),
            new BulkWriteOptions { IsOrdered = true }, cancellationToken);

    private static async Task ReplaceManyAsync<T>(IMongoCollection<T> collection, IReadOnlyCollection<T> entities,
        CancellationToken cancellationToken) where T : EntityBase
    {
        foreach (var entity in entities)
            await ReplaceRequiredAsync(collection, entity, cancellationToken);
    }

    private IMongoCollection<TerritoryResourceAssignmentPlanSnapshot> Snapshots
        => _database.GetCollection<TerritoryResourceAssignmentPlanSnapshot>(
            TerritoryResourceAssignmentPlanSnapshotRepository.CollectionName);

    private async Task CommitWithCompensationAsync(
        TerritoryModel model,
        IReadOnlyCollection<TerritoryNode> nodes,
        IReadOnlyCollection<TerritoryResourceAssignment> resourceAssignments,
        TerritoryResourceAssignmentPlanSnapshot? planSnapshot,
        CancellationToken cancellationToken)
    {
        var models = _database.GetCollection<TerritoryModel>("territory_models");
        var nodeCollection = _database.GetCollection<TerritoryNode>("territory_nodes");
        var assignments = _database.GetCollection<TerritoryResourceAssignment>("territory_resource_assignments");

        var originalModel = await models.Find(x => x.Id == model.Id && x.TenantId == model.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("The territory model changed before activation committed.");
        var nodeIds = nodes.Select(x => x.Id).ToList();
        var assignmentIds = resourceAssignments.Select(x => x.Id).ToList();
        var originalNodes = nodeIds.Count == 0
            ? []
            : await nodeCollection.Find(x => x.TenantId == model.TenantId && nodeIds.Contains(x.Id))
                .ToListAsync(cancellationToken);
        var originalAssignments = assignmentIds.Count == 0
            ? []
            : await assignments.Find(x => x.TenantId == model.TenantId && assignmentIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

        try
        {
            await models.ReplaceOneAsync(x => x.Id == model.Id && x.TenantId == model.TenantId, model,
                cancellationToken: cancellationToken);
            foreach (var node in nodes)
            {
                await nodeCollection.ReplaceOneAsync(
                    x => x.Id == node.Id && x.TenantId == node.TenantId, node,
                    cancellationToken: cancellationToken);
            }
            foreach (var assignment in resourceAssignments)
            {
                await assignments.ReplaceOneAsync(
                    x => x.Id == assignment.Id && x.TenantId == assignment.TenantId, assignment,
                    cancellationToken: cancellationToken);
            }

            // Written LAST on purpose: if it throws, the catch below restores the previous state and no baseline
            // exists — so the snapshot never needs a compensating DELETE (immutability holds even on this path).
            if (planSnapshot is not null)
            {
                await Snapshots.InsertOneAsync(planSnapshot, cancellationToken: cancellationToken);
            }
        }
        catch
        {
            await models.ReplaceOneAsync(x => x.Id == originalModel.Id && x.TenantId == originalModel.TenantId,
                originalModel, cancellationToken: cancellationToken);
            foreach (var node in originalNodes)
            {
                await nodeCollection.ReplaceOneAsync(x => x.Id == node.Id && x.TenantId == node.TenantId, node,
                    cancellationToken: cancellationToken);
            }
            foreach (var assignment in originalAssignments)
            {
                await assignments.ReplaceOneAsync(
                    x => x.Id == assignment.Id && x.TenantId == assignment.TenantId, assignment,
                    cancellationToken: cancellationToken);
            }
            throw;
        }
    }

    private static bool TransactionUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is MongoCommandException { Code: 20 }
                || current.Message.Contains("Transaction numbers are only allowed", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("replica set member or mongos", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("Standalone servers do not support transactions", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private async Task<bool> SupportsTransactionsAsync(CancellationToken cancellationToken)
    {
        var hello = await _database.RunCommandAsync<BsonDocument>(
            new BsonDocument("hello", 1), cancellationToken: cancellationToken);
        return hello.Contains("setName")
               || string.Equals(hello.GetValue("msg", "").AsString, "isdbgrid", StringComparison.OrdinalIgnoreCase);
    }
}
