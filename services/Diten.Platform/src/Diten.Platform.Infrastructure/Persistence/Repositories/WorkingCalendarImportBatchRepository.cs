using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Entities.WorkingCalendar;
using Diten.Platform.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Repositories;

public sealed class WorkingCalendarImportBatchRepository : IWorkingCalendarImportBatchRepository
{
    private readonly IMongoCollection<WorkingCalendarImportBatch> _collection;

    public WorkingCalendarImportBatchRepository(IPlatformDbContext dbContext)
        => _collection = dbContext.Database.GetCollection<WorkingCalendarImportBatch>("working_calendar_import_batches");

    private static FilterDefinition<WorkingCalendarImportBatch> Live =>
        Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.IsDeleted, false);

    public async Task<WorkingCalendarImportBatch> CreateAsync(WorkingCalendarImportBatch batch, CancellationToken ct = default)
    {
        await _collection.InsertOneAsync(batch, cancellationToken: ct);
        return batch;
    }

    public Task<WorkingCalendarImportBatch?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _collection.Find(Builders<WorkingCalendarImportBatch>.Filter.And(
            Live, Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.Id, id))).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<WorkingCalendarImportBatch>> ListAsync(string? status, string? countryCode, int? year,
        Guid? targetCalendarId, string? triggerSource, CancellationToken ct = default)
    {
        var filters = new List<FilterDefinition<WorkingCalendarImportBatch>> { Live };
        if (!string.IsNullOrWhiteSpace(status)) filters.Add(Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.ImportStatus, status));
        if (!string.IsNullOrWhiteSpace(countryCode)) filters.Add(Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.CountryCode, countryCode.ToUpperInvariant()));
        if (year.HasValue) filters.Add(Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.CalendarYear, year.Value));
        if (targetCalendarId.HasValue) filters.Add(Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.TargetCalendarId, targetCalendarId.Value));
        if (!string.IsNullOrWhiteSpace(triggerSource)) filters.Add(Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.TriggerSource, triggerSource));
        return await _collection.Find(Builders<WorkingCalendarImportBatch>.Filter.And(filters))
            .SortByDescending(x => x.RequestedAt).ToListAsync(ct);
    }

    public Task<bool> HasOpenBatchAsync(Guid targetCalendarId, CancellationToken ct = default)
        => _collection.Find(Builders<WorkingCalendarImportBatch>.Filter.And(Live,
            Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.TargetCalendarId, targetCalendarId),
            Builders<WorkingCalendarImportBatch>.Filter.In(x => x.ImportStatus, WorkingCalendarImportStatus.Open)))
            .AnyAsync(ct);

    public Task<WorkingCalendarImportBatch?> GetByScheduledRunKeyAsync(string scheduledRunKey, CancellationToken ct = default)
        => _collection.Find(Builders<WorkingCalendarImportBatch>.Filter.And(Live,
            Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.ScheduledRunKey, scheduledRunKey)))
            .SortByDescending(x => x.RequestedAt).FirstOrDefaultAsync(ct)!;

    public async Task<bool> ReplaceAsync(WorkingCalendarImportBatch batch, int expectedVersion, CancellationToken ct = default)
    {
        batch.UpdatedAt = DateTimeOffset.UtcNow;
        batch.Version = expectedVersion + 1;
        var result = await _collection.ReplaceOneAsync(Builders<WorkingCalendarImportBatch>.Filter.And(
            Live,
            Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.Id, batch.Id),
            Builders<WorkingCalendarImportBatch>.Filter.Eq(x => x.Version, expectedVersion)), batch, cancellationToken: ct);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }
}
