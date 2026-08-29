using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class TerritoryDraftCloneUnitOfWork(IMongoDatabase database) : ITerritoryDraftCloneUnitOfWork
{
    public async Task CommitAsync(TerritoryModel model, IReadOnlyCollection<TerritoryNode> nodes,
        IReadOnlyCollection<TerritoryAssignmentRule> rules, CancellationToken cancellationToken)
    {
        var models = database.GetCollection<TerritoryModel>("territory_models");
        var nodeCollection = database.GetCollection<TerritoryNode>("territory_nodes");
        var ruleCollection = database.GetCollection<TerritoryAssignmentRule>("territory_assignment_rules");

        if (!await SupportsTransactionsAsync(cancellationToken))
        {
            try
            {
                await models.InsertOneAsync(model, cancellationToken: cancellationToken);
                if (nodes.Count > 0) await nodeCollection.InsertManyAsync(nodes, cancellationToken: cancellationToken);
                if (rules.Count > 0) await ruleCollection.InsertManyAsync(rules, cancellationToken: cancellationToken);
            }
            catch
            {
                await ruleCollection.DeleteManyAsync(x => x.TenantId == model.TenantId && x.ModelId == model.Id, cancellationToken);
                await nodeCollection.DeleteManyAsync(x => x.TenantId == model.TenantId && x.ModelId == model.Id, cancellationToken);
                await models.DeleteOneAsync(x => x.TenantId == model.TenantId && x.Id == model.Id, cancellationToken);
                throw;
            }
            return;
        }

        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            await models.InsertOneAsync(session, model, cancellationToken: cancellationToken);
            if (nodes.Count > 0) await nodeCollection.InsertManyAsync(session, nodes, cancellationToken: cancellationToken);
            if (rules.Count > 0) await ruleCollection.InsertManyAsync(session, rules, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            if (session.IsInTransaction) await session.AbortTransactionAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> SupportsTransactionsAsync(CancellationToken cancellationToken)
    {
        var hello = await database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: cancellationToken);
        return hello.Contains("setName") || string.Equals(hello.GetValue("msg", "").AsString, "isdbgrid", StringComparison.OrdinalIgnoreCase);
    }
}
