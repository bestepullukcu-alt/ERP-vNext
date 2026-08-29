using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class TerritoryResourceAssignmentRepository : ITerritoryResourceAssignmentRepository
{
    private readonly IMongoCollection<TerritoryResourceAssignment> _collection;
    private readonly IMongoDatabase _database;

    public TerritoryResourceAssignmentRepository(IMongoDatabase database)
    {
        _database = database;
        _collection = database.GetCollection<TerritoryResourceAssignment>("territory_resource_assignments");
    }

    private static FilterDefinition<TerritoryResourceAssignment> ActiveModel(Guid tenantId, Guid modelId)
        => Builders<TerritoryResourceAssignment>.Filter.Where(a => a.TenantId == tenantId && a.ModelId == modelId && !a.IsDeleted);

    public async Task<TerritoryResourceAssignment?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken cancellationToken)
    {
        var filter = ActiveModel(tenantId, modelId) & Builders<TerritoryResourceAssignment>.Filter.Eq(a => a.Id, id);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TerritoryResourceAssignment>> ListByModelAsync(Guid tenantId, Guid modelId, CancellationToken cancellationToken)
        => await _collection.Find(ActiveModel(tenantId, modelId))
            .SortBy(a => a.Position.PositionCode).ThenBy(a => a.ValidFrom)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TerritoryResourceAssignment>> ListByResourceAsync(
        Guid tenantId, string resourceId, CancellationToken cancellationToken)
        => await _collection.Find(a => a.TenantId == tenantId
                                      && !a.IsDeleted
                                      && a.Resource.ResourceId == resourceId)
            .SortByDescending(a => a.ValidFrom)
            .ToListAsync(cancellationToken);

    public async Task InsertAsync(TerritoryResourceAssignment assignment, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(assignment, cancellationToken: cancellationToken);

    public async Task UpdateAsync(TerritoryResourceAssignment assignment, CancellationToken cancellationToken)
    {
        var filter = Builders<TerritoryResourceAssignment>.Filter.Where(a => a.Id == assignment.Id && a.TenantId == assignment.TenantId);
        await _collection.ReplaceOneAsync(filter, assignment, cancellationToken: cancellationToken);
    }

    public async Task CommitLifecycleTransitionAsync(
        TerritoryResourceAssignment ended,
        TerritoryResourceAssignment created,
        CancellationToken cancellationToken)
    {
        if (!await SupportsTransactionsAsync(cancellationToken))
        {
            await CommitWithCompensationAsync(ended, created, cancellationToken);
            return;
        }

        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var sourceFilter = Builders<TerritoryResourceAssignment>.Filter.Where(
                a => a.Id == ended.Id && a.TenantId == ended.TenantId && !a.IsDeleted);
            var result = await _collection.ReplaceOneAsync(session, sourceFilter, ended, cancellationToken: cancellationToken);
            if (result.MatchedCount != 1)
            {
                throw new InvalidOperationException("The source resource assignment changed before the lifecycle operation committed.");
            }

            await _collection.InsertOneAsync(session, created, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex) when (TransactionUnavailable(ex))
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellationToken);
            }

            await CommitWithCompensationAsync(ended, created, cancellationToken);
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

    private async Task CommitWithCompensationAsync(
        TerritoryResourceAssignment ended,
        TerritoryResourceAssignment created,
        CancellationToken cancellationToken)
    {
        var collection = _database.GetCollection<TerritoryResourceAssignment>("territory_resource_assignments");
        var original = await collection.Find(
                x => x.Id == ended.Id && x.TenantId == ended.TenantId && !x.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (original is null)
        {
            throw new InvalidOperationException("The source assignment changed before the lifecycle transition committed.");
        }

        var result = await collection.ReplaceOneAsync(
            x => x.Id == ended.Id && x.TenantId == ended.TenantId && !x.IsDeleted,
            ended,
            cancellationToken: cancellationToken);
        if (result.MatchedCount != 1)
        {
            throw new InvalidOperationException("The source assignment changed before the lifecycle transition committed.");
        }

        try
        {
            await collection.InsertOneAsync(created, cancellationToken: cancellationToken);
        }
        catch
        {
            await collection.ReplaceOneAsync(
                x => x.Id == original.Id && x.TenantId == original.TenantId,
                original,
                cancellationToken: cancellationToken);
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
