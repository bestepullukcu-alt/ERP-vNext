using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface ITenantUserMembershipRepository
{
    Task<IReadOnlyList<TenantUserMembership>> GetByUserIdAsync(Guid userId, CancellationToken ct);
    Task<TenantUserMembership> CreateAsync(TenantUserMembership membership, CancellationToken ct);
}
