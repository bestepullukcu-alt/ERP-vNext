using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public interface IPvgIntakeDraftStore
{
    ValueTask<string> AddAsync(
        PvgPersistenceTenantScope tenantScope,
        SafetyCaseIntake intake,
        CancellationToken cancellationToken = default);

    ValueTask<bool> ReplaceAsync(
        PvgPersistenceTenantScope tenantScope,
        string intakeDraftId,
        SafetyCaseIntake intake,
        CancellationToken cancellationToken = default);

    ValueTask<PvgPersistedIntakeDraft?> FindByIdAsync(
        PvgPersistenceTenantScope tenantScope,
        string intakeDraftId,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<PvgPersistedIntakeDraft>> ListAsync(
        PvgPersistenceListScope listScope,
        CancellationToken cancellationToken = default);
}

public sealed record PvgPersistenceTenantScope(string TenantId);

public sealed record PvgPersistenceListScope(
    PvgPersistenceTenantScope TenantScope,
    int PageNumber,
    int PageSize,
    PvgIntakeStatus? Status);

public sealed record PvgPersistedIntakeDraft(
    string IntakeDraftId,
    SafetyCaseIntake Intake);
