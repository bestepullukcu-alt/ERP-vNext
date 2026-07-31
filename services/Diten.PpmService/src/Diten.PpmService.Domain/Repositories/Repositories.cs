using Diten.PpmService.Domain.Entities;

namespace Diten.PpmService.Domain.Repositories;

public interface IRepository<T> where T : EntityBase
{
    Task<T?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<T>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, Guid? excludingId, CancellationToken cancellationToken);
    Task AddAsync(T entity, CancellationToken cancellationToken);
    Task ReplaceAsync(T entity, int expectedVersion, CancellationToken cancellationToken);
}
public interface IPortfolioRepository : IRepository<Portfolio> { }
public interface IInitiativeRepository : IRepository<Initiative> { }
public interface IProgramRepository : IRepository<Program> { }
public interface IProjectRepository : IRepository<Project> { }

public sealed record AuditIntent(
    Guid Id,
    Guid TenantId,
    Guid ActorId,
    Guid CorrelationId,
    string EntityType,
    Guid EntityId,
    string Mutation,
    DateTime OccurredAtUtc);

public sealed record AuditIntentDispatchCandidate(
    Guid Id,
    Guid TenantId,
    Guid ActorId,
    Guid CorrelationId,
    string EntityType,
    Guid EntityId,
    string Mutation,
    DateTime OccurredAtUtc,
    AuditIntentDispatchMetadata? DispatchMetadata);

public sealed record AuditIntentDispatchMetadata(
    string SignatureScheme,
    string KeyId,
    string Signature);

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
public interface IPpmUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
