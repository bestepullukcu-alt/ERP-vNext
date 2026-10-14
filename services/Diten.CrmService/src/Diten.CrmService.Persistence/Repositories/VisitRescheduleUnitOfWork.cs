using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// WP-VW-W2 (K-W1 = A) — the rescheduled report and the new planned visit it creates, written ALL-OR-NOTHING. On a
/// replica set: one multi-document transaction. On standalone Mongo (dev): the report is written first (version-checked,
/// so a concurrency mismatch writes nothing), then the visit; a failed visit insert restores the original report (or
/// deletes the inserted one) and rethrows. Mirrors <see cref="PlanningSessionApplyUnitOfWork"/>.
/// </summary>
public sealed class VisitRescheduleUnitOfWork : IVisitRescheduleUnitOfWork
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<VisitReport> _reports;
    private readonly IMongoCollection<PlannedVisit> _plannedVisits;

    public VisitRescheduleUnitOfWork(IMongoDatabase database)
    {
        _database = database;
        _reports = database.GetCollection<VisitReport>(VisitReportRepository.CollectionName);
        _plannedVisits = database.GetCollection<PlannedVisit>(PlannedVisitRepository.CollectionName);
    }

    public async Task<bool> SubmitWithNewVisitAsync(
        VisitReport report, int? expectedVersion, PlannedVisit newVisit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(newVisit);

        if (expectedVersion is { } version)
        {
            report.Version = version + 1;
        }

        if (!await SupportsTransactionsAsync(cancellationToken))
        {
            return await WriteWithCompensationAsync(report, expectedVersion, newVisit, cancellationToken);
        }

        using var session = await _database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            if (!await WriteReportAsync(session, report, expectedVersion, cancellationToken))
            {
                await session.AbortTransactionAsync(cancellationToken);
                return false;
            }

            await _plannedVisits.InsertOneAsync(session, newVisit, cancellationToken: cancellationToken);
            await session.CommitTransactionAsync(cancellationToken);
            return true;
        }
        catch (Exception ex) when (TransactionUnavailable(ex))
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(cancellationToken);
            }

            return await WriteWithCompensationAsync(report, expectedVersion, newVisit, cancellationToken);
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

    private async Task<bool> WriteReportAsync(
        IClientSessionHandle session, VisitReport report, int? expectedVersion, CancellationToken cancellationToken)
    {
        if (expectedVersion is not { } version)
        {
            await _reports.InsertOneAsync(session, report, cancellationToken: cancellationToken);
            return true;
        }

        var result = await _reports.ReplaceOneAsync(
            session,
            Builders<VisitReport>.Filter.Where(x => x.Id == report.Id && x.TenantId == report.TenantId && x.Version == version),
            report, cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.MatchedCount == 1;
    }

    private async Task<bool> WriteWithCompensationAsync(
        VisitReport report, int? expectedVersion, PlannedVisit newVisit, CancellationToken cancellationToken)
    {
        var byId = Builders<VisitReport>.Filter.Where(x => x.Id == report.Id && x.TenantId == report.TenantId);
        VisitReport? original = null;

        if (expectedVersion is { } version)
        {
            original = await _reports.Find(byId).FirstOrDefaultAsync(cancellationToken);
            var result = await _reports.ReplaceOneAsync(
                Builders<VisitReport>.Filter.Where(x => x.Id == report.Id && x.TenantId == report.TenantId && x.Version == version),
                report, cancellationToken: cancellationToken);
            if (!result.IsAcknowledged || result.MatchedCount != 1)
            {
                return false; // concurrency mismatch — nothing written
            }
        }
        else
        {
            await _reports.InsertOneAsync(report, cancellationToken: cancellationToken);
        }

        try
        {
            await _plannedVisits.InsertOneAsync(newVisit, cancellationToken: cancellationToken);
            return true;
        }
        catch
        {
            if (original is not null)
            {
                await _reports.ReplaceOneAsync(byId, original, cancellationToken: cancellationToken);
            }
            else
            {
                await _reports.DeleteOneAsync(byId, cancellationToken);
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
