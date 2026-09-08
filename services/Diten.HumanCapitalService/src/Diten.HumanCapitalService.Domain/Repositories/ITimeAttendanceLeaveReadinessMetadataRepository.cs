using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ITimeAttendanceLeaveReadinessMetadataRepository
{
    Task<IReadOnlyList<TimeAttendanceLeaveReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct);
    Task<TimeAttendanceLeaveReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TimeAttendanceLeaveReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TimeAttendanceLeaveReadinessMetadata metadata, CancellationToken ct);
}
