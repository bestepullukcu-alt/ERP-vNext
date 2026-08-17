using Diten.Platform.Domain.Entities.PayrollSources;

namespace Diten.Platform.Domain.Repositories;

public interface IPayrollSourceRepository
{
    Task<PayrollExternalSystemProfile> CreateExternalSystemProfileAsync(PayrollExternalSystemProfile profile, CancellationToken ct = default);
    Task<PayrollExternalSystemProfile?> GetExternalSystemProfileByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollExternalSystemProfile>> GetExternalSystemProfilesAsync(CancellationToken ct = default);
    Task<bool> ExistsActiveCodeAsync(string code, Guid? excludeId = null, CancellationToken ct = default);
    Task UpdateExternalSystemProfileAsync(PayrollExternalSystemProfile profile, CancellationToken ct = default);
    Task<bool> ArchiveExternalSystemProfileAsync(Guid id, CancellationToken ct = default);
    Task<PayrollContractProfile?> GetContractProfileAsync(Guid sourceProfileId, CancellationToken ct = default);
    Task UpsertContractProfileAsync(PayrollContractProfile contractProfile, CancellationToken ct = default);
    Task<PayrollEmployeeReferenceMap> CreateEmployeeReferenceMapAsync(PayrollEmployeeReferenceMap referenceMap, CancellationToken ct = default);
    Task<PayrollEmployeeReferenceMap?> GetEmployeeReferenceMapByIdAsync(Guid sourceProfileId, Guid mapId, CancellationToken ct = default);
    Task UpdateEmployeeReferenceMapAsync(PayrollEmployeeReferenceMap referenceMap, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollEmployeeReferenceMap>> GetEmployeeReferenceMapsAsync(Guid sourceProfileId, CancellationToken ct = default);
    Task<PayrollCycleReference> CreateCycleReferenceAsync(PayrollCycleReference cycleReference, CancellationToken ct = default);
    Task<PayrollCycleReference?> GetCycleReferenceByIdAsync(Guid sourceProfileId, Guid cycleReferenceId, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollCycleReference>> GetCycleReferencesAsync(Guid sourceProfileId, CancellationToken ct = default);
    Task<PayrollResultReference> CreateResultReferenceAsync(PayrollResultReference resultReference, CancellationToken ct = default);
    Task<IReadOnlyList<PayrollResultReference>> GetResultReferencesAsync(Guid sourceProfileId, CancellationToken ct = default);
    Task<PayrollSourceHealthSnapshot> CreateHealthSnapshotAsync(PayrollSourceHealthSnapshot healthSnapshot, CancellationToken ct = default);
    Task<PayrollSourceHealthSnapshot?> GetLatestHealthSnapshotAsync(Guid sourceProfileId, CancellationToken ct = default);
}
