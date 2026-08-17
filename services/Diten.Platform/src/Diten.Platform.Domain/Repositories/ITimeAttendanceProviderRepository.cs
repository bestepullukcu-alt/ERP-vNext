using Diten.Platform.Domain.Entities.TimeAttendanceProviders;

namespace Diten.Platform.Domain.Repositories;

public interface ITimeAttendanceProviderRepository
{
    Task<TimeAttendanceExternalProviderProfile> CreateProviderProfileAsync(TimeAttendanceExternalProviderProfile profile, CancellationToken ct = default);
    Task<TimeAttendanceExternalProviderProfile?> GetProviderProfileByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TimeAttendanceExternalProviderProfile>> GetProviderProfilesAsync(CancellationToken ct = default);
    Task<bool> ExistsActiveCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateProviderProfileAsync(TimeAttendanceExternalProviderProfile profile, CancellationToken ct = default);
    Task<bool> ArchiveProviderProfileAsync(Guid id, CancellationToken ct = default);
    Task<TimeAttendanceContractProfile?> GetContractProfileAsync(Guid providerProfileId, CancellationToken ct = default);
    Task UpsertContractProfileAsync(TimeAttendanceContractProfile contractProfile, CancellationToken ct = default);
    Task<TimeAttendanceEmployeeReferenceMap> CreateEmployeeReferenceMapAsync(TimeAttendanceEmployeeReferenceMap referenceMap, CancellationToken ct = default);
    Task<TimeAttendanceEmployeeReferenceMap?> GetEmployeeReferenceMapByIdAsync(Guid providerProfileId, Guid mapId, CancellationToken ct = default);
    Task UpdateEmployeeReferenceMapAsync(TimeAttendanceEmployeeReferenceMap referenceMap, CancellationToken ct = default);
    Task<IReadOnlyList<TimeAttendanceEmployeeReferenceMap>> GetEmployeeReferenceMapsAsync(Guid providerProfileId, CancellationToken ct = default);
    Task<TimeAttendanceEventReference> CreateEventReferenceAsync(TimeAttendanceEventReference eventReference, CancellationToken ct = default);
    Task<IReadOnlyList<TimeAttendanceEventReference>> GetEventReferencesAsync(Guid providerProfileId, CancellationToken ct = default);
    Task<AttendanceSummaryReference> CreateAttendanceSummaryReferenceAsync(AttendanceSummaryReference summaryReference, CancellationToken ct = default);
    Task<IReadOnlyList<AttendanceSummaryReference>> GetAttendanceSummaryReferencesAsync(Guid providerProfileId, CancellationToken ct = default);
    Task<TimeAttendanceSyncCheckpoint> CreateSyncCheckpointAsync(TimeAttendanceSyncCheckpoint checkpoint, CancellationToken ct = default);
    Task<TimeAttendanceSyncCheckpoint?> GetLatestSyncCheckpointAsync(Guid providerProfileId, CancellationToken ct = default);
    Task<TimeAttendanceProviderHealthSnapshot> CreateHealthSnapshotAsync(TimeAttendanceProviderHealthSnapshot healthSnapshot, CancellationToken ct = default);
    Task<TimeAttendanceProviderHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid providerProfileId, CancellationToken ct = default);
}
