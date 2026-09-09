using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IUserLegalEntityAssignmentRepository
{
    Task<IReadOnlyList<UserLegalEntityAssignment>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid userId, Guid legalEntityId, Guid tenantId, CancellationToken ct);
    Task<UserLegalEntityAssignment> CreateAsync(UserLegalEntityAssignment assignment, CancellationToken ct);
}
