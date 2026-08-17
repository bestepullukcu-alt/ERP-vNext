using Diten.Platform.Domain.Entities.TimeAttendanceProviders;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.TimeAttendanceProviders;

internal sealed class InMemoryTimeAttendanceProviderRepository : ITimeAttendanceProviderRepository
{
    private readonly Guid _tenantId;
    private readonly List<TimeAttendanceExternalProviderProfile> _profiles = [];
    private readonly List<TimeAttendanceContractProfile> _contracts = [];
    private readonly List<TimeAttendanceEmployeeReferenceMap> _employeeMaps = [];
    private readonly List<TimeAttendanceEventReference> _eventReferences = [];
    private readonly List<AttendanceSummaryReference> _summaryReferences = [];
    private readonly List<TimeAttendanceSyncCheckpoint> _syncCheckpoints = [];
    private readonly List<TimeAttendanceProviderHealthSnapshot> _healthSnapshots = [];

    public InMemoryTimeAttendanceProviderRepository(Guid tenantId) => _tenantId = tenantId;

    public IReadOnlyList<TimeAttendanceExternalProviderProfile> Profiles => _profiles;
    public IReadOnlyList<TimeAttendanceEmployeeReferenceMap> EmployeeMaps => _employeeMaps;
    public IReadOnlyList<TimeAttendanceEventReference> EventReferences => _eventReferences;

    public Task<TimeAttendanceExternalProviderProfile> CreateProviderProfileAsync(TimeAttendanceExternalProviderProfile profile, CancellationToken ct = default)
    {
        _profiles.Add(profile);
        return Task.FromResult(profile);
    }

    public Task<TimeAttendanceExternalProviderProfile?> GetProviderProfileByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_profiles.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

    public Task<IReadOnlyList<TimeAttendanceExternalProviderProfile>> GetProviderProfilesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TimeAttendanceExternalProviderProfile> result = _profiles
            .Where(x => x.TenantId == _tenantId && !x.IsDeleted)
            .OrderBy(x => x.Code)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> ExistsActiveCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default) =>
        Task.FromResult(_profiles.Any(x =>
            x.TenantId == _tenantId
            && !x.IsDeleted
            && x.Code == code
            && (!excludeId.HasValue || x.Id != excludeId.Value)));

    public Task UpdateProviderProfileAsync(TimeAttendanceExternalProviderProfile profile, CancellationToken ct = default)
    {
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<bool> ArchiveProviderProfileAsync(Guid id, CancellationToken ct = default)
    {
        var item = _profiles.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted);
        if (item == null)
        {
            return Task.FromResult(false);
        }

        item.IsDeleted = true;
        item.DeletedAt = DateTimeOffset.UtcNow;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.FromResult(true);
    }

    public Task<TimeAttendanceContractProfile?> GetContractProfileAsync(Guid providerProfileId, CancellationToken ct = default) =>
        Task.FromResult(_contracts.FirstOrDefault(x => x.TenantId == _tenantId && x.ProviderProfileId == providerProfileId && !x.IsDeleted));

    public Task UpsertContractProfileAsync(TimeAttendanceContractProfile contractProfile, CancellationToken ct = default)
    {
        var index = _contracts.FindIndex(x => x.Id == contractProfile.Id && x.TenantId == _tenantId && !x.IsDeleted);
        if (index >= 0)
        {
            _contracts[index] = contractProfile;
        }
        else
        {
            _contracts.Add(contractProfile);
        }

        return Task.CompletedTask;
    }

    public Task<TimeAttendanceEmployeeReferenceMap> CreateEmployeeReferenceMapAsync(TimeAttendanceEmployeeReferenceMap referenceMap, CancellationToken ct = default)
    {
        _employeeMaps.Add(referenceMap);
        return Task.FromResult(referenceMap);
    }

    public Task<TimeAttendanceEmployeeReferenceMap?> GetEmployeeReferenceMapByIdAsync(Guid providerProfileId, Guid mapId, CancellationToken ct = default) =>
        Task.FromResult(_employeeMaps.FirstOrDefault(x => x.TenantId == _tenantId && x.ProviderProfileId == providerProfileId && x.Id == mapId && !x.IsDeleted));

    public Task UpdateEmployeeReferenceMapAsync(TimeAttendanceEmployeeReferenceMap referenceMap, CancellationToken ct = default)
    {
        referenceMap.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<TimeAttendanceEmployeeReferenceMap>> GetEmployeeReferenceMapsAsync(Guid providerProfileId, CancellationToken ct = default)
    {
        IReadOnlyList<TimeAttendanceEmployeeReferenceMap> result = _employeeMaps
            .Where(x => x.TenantId == _tenantId && x.ProviderProfileId == providerProfileId && !x.IsDeleted)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<TimeAttendanceEventReference> CreateEventReferenceAsync(TimeAttendanceEventReference eventReference, CancellationToken ct = default)
    {
        _eventReferences.Add(eventReference);
        return Task.FromResult(eventReference);
    }

    public Task<IReadOnlyList<TimeAttendanceEventReference>> GetEventReferencesAsync(Guid providerProfileId, CancellationToken ct = default)
    {
        IReadOnlyList<TimeAttendanceEventReference> result = _eventReferences
            .Where(x => x.TenantId == _tenantId && x.ProviderProfileId == providerProfileId && !x.IsDeleted)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<AttendanceSummaryReference> CreateAttendanceSummaryReferenceAsync(AttendanceSummaryReference summaryReference, CancellationToken ct = default)
    {
        _summaryReferences.Add(summaryReference);
        return Task.FromResult(summaryReference);
    }

    public Task<IReadOnlyList<AttendanceSummaryReference>> GetAttendanceSummaryReferencesAsync(Guid providerProfileId, CancellationToken ct = default)
    {
        IReadOnlyList<AttendanceSummaryReference> result = _summaryReferences
            .Where(x => x.TenantId == _tenantId && x.ProviderProfileId == providerProfileId && !x.IsDeleted)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<TimeAttendanceSyncCheckpoint> CreateSyncCheckpointAsync(TimeAttendanceSyncCheckpoint checkpoint, CancellationToken ct = default)
    {
        _syncCheckpoints.Add(checkpoint);
        return Task.FromResult(checkpoint);
    }

    public Task<TimeAttendanceSyncCheckpoint?> GetLatestSyncCheckpointAsync(Guid providerProfileId, CancellationToken ct = default) =>
        Task.FromResult(_syncCheckpoints
            .Where(x => x.TenantId == _tenantId && x.ProviderProfileId == providerProfileId && !x.IsDeleted)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault());

    public Task<TimeAttendanceProviderHealthSnapshot> CreateHealthSnapshotAsync(TimeAttendanceProviderHealthSnapshot healthSnapshot, CancellationToken ct = default)
    {
        _healthSnapshots.Add(healthSnapshot);
        return Task.FromResult(healthSnapshot);
    }

    public Task<TimeAttendanceProviderHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid providerProfileId, CancellationToken ct = default) =>
        Task.FromResult(_healthSnapshots
            .Where(x => x.TenantId == _tenantId && x.ProviderProfileId == providerProfileId && !x.IsDeleted)
            .OrderByDescending(x => x.CheckedAt)
            .FirstOrDefault());

    public void Add(TimeAttendanceExternalProviderProfile item) => _profiles.Add(item);

    public void Add(TimeAttendanceContractProfile item) => _contracts.Add(item);
}
