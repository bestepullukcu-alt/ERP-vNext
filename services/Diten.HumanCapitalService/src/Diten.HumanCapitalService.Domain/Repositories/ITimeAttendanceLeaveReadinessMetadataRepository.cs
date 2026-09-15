using Diten.HumanCapitalService.Domain.Entities;

namespace Diten.HumanCapitalService.Domain.Repositories;

public interface ITimeAttendanceLeaveReadinessMetadataRepository
{
    Task<IReadOnlyList<TimeAttendanceLeaveReadinessMetadata>> ListAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, CancellationToken ct);
    Task<TimeAttendanceLeaveReadinessMetadata?> GetByIdAsync(Guid tenantId, IReadOnlyCollection<Guid> legalEntityIds, Guid id, CancellationToken ct);
    Task<bool> ExistsActiveCodeAsync(Guid tenantId, Guid legalEntityId, string code, Guid? excludingId, CancellationToken ct);
    Task CreateAsync(TimeAttendanceLeaveReadinessMetadata metadata, CancellationToken ct);
    Task UpdateAsync(TimeAttendanceLeaveReadinessMetadata metadata, CancellationToken ct);
}
