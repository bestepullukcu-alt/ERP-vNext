using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

public sealed class AccountTerritoryAssignmentRepository : IAccountTerritoryAssignmentRepository
{
    private readonly IMongoCollection<AccountTerritoryAssignment> _collection;
    private readonly IMongoDatabase _database;

    public AccountTerritoryAssignmentRepository(IMongoDatabase database)
    {
        _database = database;
        _collection = database.GetCollection<AccountTerritoryAssignment>("account_territory_assignments");
    }

    private static FilterDefinition<AccountTerritoryAssignment> Tenant(Guid tenantId)
        => Builders<AccountTerritoryAssignment>.Filter.Where(a => a.TenantId == tenantId && !a.IsDeleted);

    public Task<AccountTerritoryAssignment?> GetByIdAsync(Guid tenantId, Guid modelId, Guid id, CancellationToken cancellationToken)
        => _collection.Find(Tenant(tenantId)
            & Builders<AccountTerritoryAssignment>.Filter.Eq(a => a.TerritoryModelId, modelId)
            & Builders<AccountTerritoryAssignment>.Filter.Eq(a => a.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AccountTerritoryAssignment>> ListByModelAsync(
        Guid tenantId, Guid modelId, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId)
                & Builders<AccountTerritoryAssignment>.Filter.Eq(a => a.TerritoryModelId, modelId))
            .SortBy(a => a.AccountDisplayName).ThenByDescending(a => a.EffectiveFrom)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AccountTerritoryAssignment>> ListByAccountAsync(
        Guid tenantId, Guid accountId, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId)
                & Builders<AccountTerritoryAssignment>.Filter.Eq(a => a.AccountId, accountId))
            .SortByDescending(a => a.EffectiveFrom)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AccountTerritoryAssignment>> ListActiveByAccountIdsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, CancellationToken cancellationToken)
    {
        if (accountIds is null || accountIds.Count == 0) return [];
        return await _collection.Find(Tenant(tenantId)
                & Builders<AccountTerritoryAssignment>.Filter.In(a => a.AccountId, accountIds)
                & Builders<AccountTerritoryAssignment>.Filter.Eq(a => a.AssignmentStatus, "active"))
            .ToListAsync(cancellationToken);
    }

    public Task InsertManyAsync(IReadOnlyCollection<AccountTerritoryAssignment> assignments, CancellationToken cancellationToken)
        => assignments.Count == 0
            ? Task.CompletedTask
            : _collection.InsertManyAsync(assignments, new InsertManyOptions { IsOrdered = true }, cancellationToken);

    public async Task UpdateManyAsync(IReadOnlyCollection<AccountTerritoryAssignment> assignments, CancellationToken cancellationToken)
    {
        if (assignments.Count == 0) return;
        var writes = assignments.Select(a => new ReplaceOneModel<AccountTerritoryAssignment>(
            Builders<AccountTerritoryAssignment>.Filter.Where(x => x.Id == a.Id && x.TenantId == a.TenantId), a));
        await _collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = true }, cancellationToken);
    }

    public async Task UpdateAsync(AccountTerritoryAssignment assignment, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<AccountTerritoryAssignment>.Filter.Where(a => a.Id == assignment.Id && a.TenantId == assignment.TenantId),
            assignment, cancellationToken: cancellationToken);

    public async Task CommitApplyAsync(
        IReadOnlyCollection<AccountTerritoryAssignment> ended,
        IReadOnlyCollection<AccountTerritoryAssignment> created,
        CancellationToken cancellationToken)
    {
        // Standalone Mongo (dev) has no multi-document transactions; fall back to compensated sequential writes.
        if (!await SupportsTransactionsAsync(cancellationToken))
        {
            await CommitWithCompensationAsync(ended, created, cancellationToken);
            return;
        }

        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            if (ended.Count > 0)
            {
                var writes = ended.Select(a => new ReplaceOneModel<AccountTerritoryAssignment>(
                    Builders<AccountTerritoryAssignment>.Filter.Where(x => x.Id == a.Id && x.TenantId == a.TenantId), a));
                await _collection.BulkWriteAsync(session, writes, new BulkWriteOptions { IsOrdered = true }, cancellationToken);
            }
            if (created.Count > 0)
                await _collection.InsertManyAsync(session, created, new InsertManyOptions { IsOrdered = true }, cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex) when (TransactionUnavailable(ex))
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync(cancellationToken);
            await CommitWithCompensationAsync(ended, created, cancellationToken);
        }
        catch
        {
            if (session.IsInTransaction)
                await session.AbortTransactionAsync(cancellationToken);
            throw;
        }
    }

    // Transaction-free apply for standalone servers: end the conflicts, then insert the new rows. Originals are
    // captured first so a failed insert can be rolled back (delete the created rows, restore the ended rows).
    private async Task CommitWithCompensationAsync(
        IReadOnlyCollection<AccountTerritoryAssignment> ended,
        IReadOnlyCollection<AccountTerritoryAssignment> created,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AccountTerritoryAssignment> originals = [];
        if (ended.Count > 0)
        {
            var endedIds = ended.Select(a => a.Id).ToList();
            originals = await _collection
                .Find(Builders<AccountTerritoryAssignment>.Filter.In(x => x.Id, endedIds))
                .ToListAsync(cancellationToken);
            var writes = ended.Select(a => new ReplaceOneModel<AccountTerritoryAssignment>(
                Builders<AccountTerritoryAssignment>.Filter.Where(x => x.Id == a.Id && x.TenantId == a.TenantId), a));
            await _collection.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = true }, cancellationToken);
        }

        if (created.Count == 0) return;
        try
        {
            await _collection.InsertManyAsync(created, new InsertManyOptions { IsOrdered = true }, cancellationToken);
        }
        catch
        {
            var createdIds = created.Select(a => a.Id).ToList();
            await _collection.DeleteManyAsync(
                Builders<AccountTerritoryAssignment>.Filter.In(x => x.Id, createdIds), cancellationToken);
            if (originals.Count > 0)
            {
                var restore = originals.Select(a => new ReplaceOneModel<AccountTerritoryAssignment>(
                    Builders<AccountTerritoryAssignment>.Filter.Where(x => x.Id == a.Id && x.TenantId == a.TenantId), a));
                await _collection.BulkWriteAsync(restore, new BulkWriteOptions { IsOrdered = true }, cancellationToken);
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
