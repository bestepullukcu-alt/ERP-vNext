using Diten.Platform.Infrastructure.Persistence.Schema;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class JobExecutionLogRepository : IJobExecutionLogRepository
{
    private readonly IMongoCollection<JobExecutionLog> _collection;

    public JobExecutionLogRepository(IPlatformDbContext dbContext)
    {
        _collection = dbContext.GetCollection<JobExecutionLog>(PlatformCollections.JobExecutionLogs);
    }

    public async Task<JobExecutionLog> CreateAsync(JobExecutionLog log, CancellationToken cancellationToken = default)
    {
        await _collection.InsertOneAsync(log, cancellationToken: cancellationToken);
        return log;
    }

    public Task<JobExecutionLog?> MarkSucceededAsync(
        Guid id,
        DateTimeOffset finishedAt,
        long durationMs,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<JobExecutionLog>.Update
            .Set(x => x.Status, JobExecutionStatus.Succeeded)
            .Set(x => x.FinishedAt, finishedAt)
            .Set(x => x.DurationMs, durationMs)
            .Set(x => x.UpdatedAt, finishedAt);

        return UpdateByIdAsync(id, update, cancellationToken);
    }

    public Task<JobExecutionLog?> MarkFailedAsync(
        Guid id,
        DateTimeOffset finishedAt,
        long durationMs,
        string error,
        int retryCount,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<JobExecutionLog>.Update
            .Set(x => x.Status, JobExecutionStatus.Failed)
            .Set(x => x.FinishedAt, finishedAt)
            .Set(x => x.DurationMs, durationMs)
            .Set(x => x.Error, error)
            .Set(x => x.RetryCount, retryCount)
            .Set(x => x.UpdatedAt, finishedAt);

        return UpdateByIdAsync(id, update, cancellationToken);
    }

    public async Task<IReadOnlyList<JobExecutionLog>> GetByServiceNameAsync(
        string serviceName,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 500);
        var filter = Builders<JobExecutionLog>.Filter.And(
            Builders<JobExecutionLog>.Filter.Eq(x => x.ServiceName, serviceName),
            Builders<JobExecutionLog>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter)
            .SortByDescending(x => x.StartedAt)
            .Limit(normalizedLimit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobExecutionLog>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<JobExecutionLog>.Filter.And(
            Builders<JobExecutionLog>.Filter.Eq(x => x.CorrelationId, correlationId),
            Builders<JobExecutionLog>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.Find(filter)
            .SortByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);
    }

    private async Task<JobExecutionLog?> UpdateByIdAsync(
        Guid id,
        UpdateDefinition<JobExecutionLog> update,
        CancellationToken cancellationToken)
    {
        var filter = Builders<JobExecutionLog>.Filter.And(
            Builders<JobExecutionLog>.Filter.Eq(x => x.Id, id),
            Builders<JobExecutionLog>.Filter.Eq(x => x.IsDeleted, false));

        return await _collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<JobExecutionLog> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }
}
