using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public interface IAuditIntentTemporalMigrationRepository
{
    Task<AuditIntentTemporalMigrationState?> GetValidatedStateAsync(
        CancellationToken cancellationToken = default);

    Task<AuditIntentTemporalMigrationResult> RunAsync(
        AuditIntentTemporalMigrationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record AuditIntentTemporalMigrationRequest(
    string LeaseOwner,
    int BatchSize,
    TimeSpan LeaseDuration,
    bool VerifyCompletion,
    bool ActivateCutover,
    string SelectedIndexEvidenceFingerprint);

public sealed record AuditIntentTemporalMigrationResult(
    string Phase,
    long ScannedCount,
    long MigratedCount,
    long AlreadyCurrentCount,
    int CollectionOrdinal,
    Guid? LastAggregateId,
    Guid? LastIntentId);
