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

public sealed record AuditIntent(Guid Id, Guid TenantId, Guid ActorId, string EntityType, Guid EntityId, string Mutation, DateTime OccurredAtUtc);
public interface IAuditIntentRepository { Task AddAsync(AuditIntent intent, CancellationToken cancellationToken); }
public interface IPpmUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
}
