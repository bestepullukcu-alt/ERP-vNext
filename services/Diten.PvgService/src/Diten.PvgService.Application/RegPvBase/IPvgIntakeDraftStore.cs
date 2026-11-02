using Diten.PvgService.Domain.RegPvBase;

namespace Diten.PvgService.Application.RegPvBase;

public interface IPvgIntakeDraftStore
{
    // actorId is the validated subject (PvgActorContext.ActorId, OD-PVG-2); a store that keeps actor fields writes
    // CreatedBy / UpdatedBy from it and never from a request body or header.
    ValueTask<string> AddAsync(
        PvgPersistenceTenantScope tenantScope,
        string actorId,
        SafetyCaseIntake intake,
        CancellationToken cancellationToken = default);

    ValueTask<PvgIntakeDraftReplaceResult> ReplaceAsync(
        PvgPersistenceTenantScope tenantScope,
        string intakeDraftId,
        long? expectedVersion,
        string actorId,
        SafetyCaseIntake intake,
        CancellationToken cancellationToken = default);

    ValueTask<PvgPersistedIntakeDraft?> FindByIdAsync(
        PvgPersistenceTenantScope tenantScope,
        string intakeDraftId,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<PvgPersistedIntakeDraft>> ListAsync(
        PvgPersistenceListScope listScope,
        CancellationToken cancellationToken = default);

    ValueTask<int> CountAsync(
        PvgPersistenceTenantScope tenantScope,
        PvgIntakeStatus? status,
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
    SafetyCaseIntake Intake,
    long Version);

public sealed record PvgIntakeDraftReplaceResult(
    PvgIntakeDraftReplaceOutcome Outcome,
    long? Version)
{
    public static PvgIntakeDraftReplaceResult Replaced(long version) => new(PvgIntakeDraftReplaceOutcome.Replaced, version);

    public static PvgIntakeDraftReplaceResult NotFound() => new(PvgIntakeDraftReplaceOutcome.NotFound, null);

    public static PvgIntakeDraftReplaceResult VersionConflict() => new(PvgIntakeDraftReplaceOutcome.VersionConflict, null);
}

public enum PvgIntakeDraftReplaceOutcome
{
    Replaced,
    NotFound,
    VersionConflict
}
