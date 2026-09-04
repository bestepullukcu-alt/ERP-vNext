using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0155 FU05 atomic apply / re-plan (D-APPLY-ATOMICITY = C, LOCKED). Writing the FU01 <see cref="PlannedVisit"/>
/// atoms into <c>planned_visits</c> and flipping the <see cref="PlanningSession"/> to <c>committed</c> in
/// <c>planning_sessions</c> is ALL-OR-NOTHING: on a replica set it runs in one multi-document transaction; on dev
/// STANDALONE Mongo (no transactions) it falls back to compensated sequential writes that capture the originals first
/// and roll back manually on failure. Mirrors the shipped <c>AccountTerritoryAssignmentRepository.CommitApplyAsync</c>
/// pattern so a half-applied plan can never be left behind and the session is never flipped without its atoms.
/// <para>It reuses FU01's own <c>planned_visits</c> collection + aggregate — it does NOT duplicate FU01 storage and does
/// NOT change the aggregate shape; the engine hands it fully-formed atoms.</para>
/// </summary>
public sealed class PlanningSessionApplyUnitOfWork : IPlanningSessionApplyUnitOfWork
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<PlanningSession> _sessions;
    private readonly IMongoCollection<PlannedVisit> _plannedVisits;

    public PlanningSessionApplyUnitOfWork(IMongoDatabase database)
    {
        _database = database;
        _sessions = database.GetCollection<PlanningSession>(PlanningSessionRepository.CollectionName);
        _plannedVisits = database.GetCollection<PlannedVisit>(PlannedVisitRepository.CollectionName);
    }

    public async Task<bool> ApplyAsync(
        PlanningSession session, int expectedVersion, IReadOnlyList<PlannedVisit> atoms, CancellationToken cancellationToken)
    {
        session.Version = expectedVersion + 1;
        var sessionFilter = Builders<PlanningSession>.Filter.Where(
            x => x.Id == session.Id && x.TenantId == session.TenantId && x.Version == expectedVersion);

        if (!await SupportsTransactionsAsync(cancellationToken))
        {
            return await ApplyWithCompensationAsync(session, sessionFilter, atoms, cancellationToken);
        }

        using var mongoSession = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        mongoSession.StartTransaction();
        try
        {
            var replace = await _sessions.ReplaceOneAsync(
                mongoSession, sessionFilter, session, cancellationToken: cancellationToken);
            if (!replace.IsAcknowledged || replace.MatchedCount != 1)
            {
                await mongoSession.AbortTransactionAsync(cancellationToken);
                return false; // concurrency mismatch — nothing written
            }

            if (atoms.Count > 0)
            {
                await _plannedVisits.InsertManyAsync(
                    mongoSession, atoms, new InsertManyOptions { IsOrdered = true }, cancellationToken);
            }

            await mongoSession.CommitTransactionAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (TransactionUnavailable(ex))
        {
            if (mongoSession.IsInTransaction)
            {
                await mongoSession.AbortTransactionAsync(cancellationToken);
            }

            return await ApplyWithCompensationAsync(session, sessionFilter, atoms, cancellationToken);
        }
        catch
        {
            if (mongoSession.IsInTransaction)
            {
                await mongoSession.AbortTransactionAsync(cancellationToken);
            }

            throw;
        }
    }

    // Transaction-free apply for standalone servers: flip the session (version-checked) FIRST so a concurrency mismatch
    // writes nothing, then insert the atoms; on a failed insert, delete the created atoms and restore the original
    // session, so no half-plan and no premature commit survives.
    private async Task<bool> ApplyWithCompensationAsync(
        PlanningSession session, FilterDefinition<PlanningSession> sessionFilter,
        IReadOnlyList<PlannedVisit> atoms, CancellationToken cancellationToken)
    {
        var original = await _sessions
            .Find(Builders<PlanningSession>.Filter.Where(x => x.Id == session.Id && x.TenantId == session.TenantId))
            .FirstOrDefaultAsync(cancellationToken);

        var replace = await _sessions.ReplaceOneAsync(sessionFilter, session, cancellationToken: cancellationToken);
        if (!replace.IsAcknowledged || replace.MatchedCount != 1)
        {
            return false; // concurrency mismatch — nothing written
        }

        if (atoms.Count == 0)
        {
            return true;
        }

        try
        {
            await _plannedVisits.InsertManyAsync(
                atoms, new InsertManyOptions { IsOrdered = true }, cancellationToken);
            return true;
        }
        catch
        {
            var createdIds = atoms.Select(a => a.Id).ToList();
            await _plannedVisits.DeleteManyAsync(
                Builders<PlannedVisit>.Filter.In(x => x.Id, createdIds), cancellationToken);
            if (original is not null)
            {
                await _sessions.ReplaceOneAsync(
                    Builders<PlanningSession>.Filter.Where(x => x.Id == original.Id && x.TenantId == original.TenantId),
                    original, cancellationToken: cancellationToken);
            }

            throw;
        }
    }

    public async Task ReplanAsync(IReadOnlyList<PlannedVisit> atoms, CancellationToken cancellationToken)
    {
        if (atoms is null || atoms.Count == 0)
        {
            return;
        }

        // Bump each atom's optimistic token so a concurrent writer is not silently overwritten.
        foreach (var atom in atoms)
        {
            atom.Version += 1;
        }

        var writes = atoms.Select(a => new ReplaceOneModel<PlannedVisit>(
            Builders<PlannedVisit>.Filter.Where(
                x => x.Id == a.Id && x.TenantId == a.TenantId && x.Version == a.Version - 1),
            a));

        if (!await SupportsTransactionsAsync(cancellationToken))
        {
            await _plannedVisits.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = true }, cancellationToken);
            return;
        }

        using var mongoSession = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        mongoSession.StartTransaction();
        try
        {
            await _plannedVisits.BulkWriteAsync(
                mongoSession, writes, new BulkWriteOptions { IsOrdered = true }, cancellationToken);
            await mongoSession.CommitTransactionAsync(cancellationToken);
        }
        catch (Exception ex) when (TransactionUnavailable(ex))
        {
            if (mongoSession.IsInTransaction)
            {
                await mongoSession.AbortTransactionAsync(cancellationToken);
            }

            await _plannedVisits.BulkWriteAsync(writes, new BulkWriteOptions { IsOrdered = true }, cancellationToken);
        }
        catch
        {
            if (mongoSession.IsInTransaction)
            {
                await mongoSession.AbortTransactionAsync(cancellationToken);
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
