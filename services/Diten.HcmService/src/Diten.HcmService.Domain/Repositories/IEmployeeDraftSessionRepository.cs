using Diten.HcmService.Domain.Entities;

namespace Diten.HcmService.Domain.Repositories;

public interface IEmployeeDraftSessionRepository
{
    Task<EmployeeDraftSession?> GetByIdAsync(Guid tenantId, Guid draftSessionId, CancellationToken cancellationToken);
    Task<EmployeeDraftSession?> GetByCreateIdempotencyKeyAsync(Guid tenantId, string idempotencyKeyHash, CancellationToken cancellationToken);
    Task AddAsync(EmployeeDraftSession draftSession, CancellationToken cancellationToken);
    Task<bool> ReplaceAsync(EmployeeDraftSession draftSession, int expectedVersion, CancellationToken cancellationToken);
}
