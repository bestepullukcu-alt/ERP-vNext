namespace Diten.PpmService.Domain.Entities;

public interface IInitiativeV2Repository
{
    Task<Initiative?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Initiative>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, Guid? excludingId, CancellationToken cancellationToken);
    Task AddAsync(Initiative initiative, CancellationToken cancellationToken);
    Task ReplaceAsync(Initiative initiative, int expectedVersion, CancellationToken cancellationToken);
    Task<Initiative?> GetActiveSuccessorAsync(Guid tenantId, Guid terminalId, CancellationToken cancellationToken);
    Task ClaimTerminalForSuccessorAsync(Guid tenantId, Guid terminalId, Guid successorId, int expectedVersion,
        CancellationToken cancellationToken);
    Task AddClosureAsync(InitiativeClosure closure, CancellationToken cancellationToken);
}
