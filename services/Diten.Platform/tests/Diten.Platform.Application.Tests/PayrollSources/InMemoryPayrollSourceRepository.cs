using Diten.Platform.Domain.Entities.PayrollSources;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Tests.PayrollSources;

internal sealed class InMemoryPayrollSourceRepository : IPayrollSourceRepository
{
    private readonly Guid _tenantId;
    private readonly List<PayrollExternalSystemProfile> _profiles = [];
    private readonly List<PayrollContractProfile> _contracts = [];
    private readonly List<PayrollEmployeeReferenceMap> _employeeMaps = [];
    private readonly List<PayrollCycleReference> _cycleReferences = [];
    private readonly List<PayrollResultReference> _resultReferences = [];
    private readonly List<PayrollSourceHealthSnapshot> _healthSnapshots = [];

    public InMemoryPayrollSourceRepository(Guid tenantId) => _tenantId = tenantId;

    public IReadOnlyList<PayrollExternalSystemProfile> Profiles => _profiles;
    public IReadOnlyList<PayrollEmployeeReferenceMap> EmployeeMaps => _employeeMaps;

    public Task<PayrollExternalSystemProfile> CreateExternalSystemProfileAsync(PayrollExternalSystemProfile profile, CancellationToken ct = default)
    {
        _profiles.Add(profile);
        return Task.FromResult(profile);
    }

    public Task<PayrollExternalSystemProfile?> GetExternalSystemProfileByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_profiles.FirstOrDefault(x => x.Id == id && x.TenantId == _tenantId && !x.IsDeleted));

    public Task<IReadOnlyList<PayrollExternalSystemProfile>> GetExternalSystemProfilesAsync(CancellationToken ct = default)
    {
        IReadOnlyList<PayrollExternalSystemProfile> result = _profiles
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

    public Task UpdateExternalSystemProfileAsync(PayrollExternalSystemProfile profile, CancellationToken ct = default)
    {
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<bool> ArchiveExternalSystemProfileAsync(Guid id, CancellationToken ct = default)
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

    public Task<PayrollContractProfile?> GetContractProfileAsync(Guid sourceProfileId, CancellationToken ct = default) =>
        Task.FromResult(_contracts.FirstOrDefault(x => x.TenantId == _tenantId && x.PayrollExternalSystemProfileId == sourceProfileId && !x.IsDeleted));

    public Task UpsertContractProfileAsync(PayrollContractProfile contractProfile, CancellationToken ct = default)
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

    public Task<PayrollEmployeeReferenceMap> CreateEmployeeReferenceMapAsync(PayrollEmployeeReferenceMap referenceMap, CancellationToken ct = default)
    {
        _employeeMaps.Add(referenceMap);
        return Task.FromResult(referenceMap);
    }

    public Task<PayrollEmployeeReferenceMap?> GetEmployeeReferenceMapByIdAsync(Guid sourceProfileId, Guid mapId, CancellationToken ct = default) =>
        Task.FromResult(_employeeMaps.FirstOrDefault(x => x.TenantId == _tenantId && x.PayrollExternalSystemProfileId == sourceProfileId && x.Id == mapId && !x.IsDeleted));

    public Task UpdateEmployeeReferenceMapAsync(PayrollEmployeeReferenceMap referenceMap, CancellationToken ct = default)
    {
        referenceMap.UpdatedAt = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PayrollEmployeeReferenceMap>> GetEmployeeReferenceMapsAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        IReadOnlyList<PayrollEmployeeReferenceMap> result = _employeeMaps
            .Where(x => x.TenantId == _tenantId && x.PayrollExternalSystemProfileId == sourceProfileId && !x.IsDeleted)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<PayrollCycleReference> CreateCycleReferenceAsync(PayrollCycleReference cycleReference, CancellationToken ct = default)
    {
        _cycleReferences.Add(cycleReference);
        return Task.FromResult(cycleReference);
    }

    public Task<PayrollCycleReference?> GetCycleReferenceByIdAsync(Guid sourceProfileId, Guid cycleReferenceId, CancellationToken ct = default) =>
        Task.FromResult(_cycleReferences.FirstOrDefault(x => x.TenantId == _tenantId && x.PayrollExternalSystemProfileId == sourceProfileId && x.Id == cycleReferenceId && !x.IsDeleted));

    public Task<IReadOnlyList<PayrollCycleReference>> GetCycleReferencesAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        IReadOnlyList<PayrollCycleReference> result = _cycleReferences
            .Where(x => x.TenantId == _tenantId && x.PayrollExternalSystemProfileId == sourceProfileId && !x.IsDeleted)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<PayrollResultReference> CreateResultReferenceAsync(PayrollResultReference resultReference, CancellationToken ct = default)
    {
        _resultReferences.Add(resultReference);
        return Task.FromResult(resultReference);
    }

    public Task<IReadOnlyList<PayrollResultReference>> GetResultReferencesAsync(Guid sourceProfileId, CancellationToken ct = default)
    {
        IReadOnlyList<PayrollResultReference> result = _resultReferences
            .Where(x => x.TenantId == _tenantId && x.PayrollExternalSystemProfileId == sourceProfileId && !x.IsDeleted)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<PayrollSourceHealthSnapshot> CreateHealthSnapshotAsync(PayrollSourceHealthSnapshot healthSnapshot, CancellationToken ct = default)
    {
        _healthSnapshots.Add(healthSnapshot);
        return Task.FromResult(healthSnapshot);
    }

    public Task<PayrollSourceHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid sourceProfileId, CancellationToken ct = default) =>
        Task.FromResult(_healthSnapshots
            .Where(x => x.TenantId == _tenantId && x.PayrollExternalSystemProfileId == sourceProfileId && !x.IsDeleted)
            .OrderByDescending(x => x.CheckedAt)
            .FirstOrDefault());

    public void Add(PayrollExternalSystemProfile item) => _profiles.Add(item);

    public void Add(PayrollContractProfile item) => _contracts.Add(item);
}
