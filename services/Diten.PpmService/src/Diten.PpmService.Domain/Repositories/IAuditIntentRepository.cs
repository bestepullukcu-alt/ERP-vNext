namespace Diten.PpmService.Domain.Repositories;

public interface IAuditIntentRepository
{
    Task AddAsync(AuditIntent intent, CancellationToken cancellationToken);

    Task<IReadOnlyList<AuditIntentDispatchCandidate>> GetDispatchCandidatesAsync(
        int batchSize,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Audit intent dispatch is not implemented by this repository.");

    Task<AuditIntentDispatchMetadata> EnsureDispatchMetadataAsync(
        Guid intentId,
        AuditIntentDispatchMetadata proposed,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Audit intent signing metadata persistence is not implemented by this repository.");

    Task<bool> MarkOutboxEnqueuedAsync(
        Guid intentId,
        DateTime enqueuedAtUtc,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Audit intent dispatch marker persistence is not implemented by this repository.");

    Task<bool> MarkDispatchQuarantinedAsync(
        Guid intentId,
        string failureCode,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Audit intent quarantine persistence is not implemented by this repository.");
}
