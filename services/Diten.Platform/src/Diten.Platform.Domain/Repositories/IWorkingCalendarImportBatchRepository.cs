using Diten.Platform.Domain.Entities.WorkingCalendar;

namespace Diten.Platform.Domain.Repositories;

public interface IWorkingCalendarImportBatchRepository
{
    Task<WorkingCalendarImportBatch> CreateAsync(WorkingCalendarImportBatch batch, CancellationToken ct = default);
    Task<WorkingCalendarImportBatch?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<WorkingCalendarImportBatch>> ListAsync(string? status, string? countryCode, int? year,
        Guid? targetCalendarId, string? triggerSource, CancellationToken ct = default);
    Task<bool> HasOpenBatchAsync(Guid targetCalendarId, CancellationToken ct = default);
    Task<WorkingCalendarImportBatch?> GetByScheduledRunKeyAsync(string scheduledRunKey, CancellationToken ct = default);
    Task<bool> ReplaceAsync(WorkingCalendarImportBatch batch, int expectedVersion, CancellationToken ct = default);
}
