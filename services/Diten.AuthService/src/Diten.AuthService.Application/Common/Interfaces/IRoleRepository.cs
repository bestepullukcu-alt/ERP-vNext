using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct);
    Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct);
    Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct);
    Task<Role> CreateAsync(Role role, CancellationToken ct);
    Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct);

    /// <summary>
    /// WP-0018-ROLE1 — inserts the system role when the tenant has no active role of that name and says whether THIS call
    /// inserted it (atomic against <c>uq_roles_tenant_name_active</c>: of concurrent callers exactly one gets
    /// <see cref="SystemRoleProvisioning.Created"/> = true). An existing role is returned unchanged. The decision built on it is
    /// <c>DefaultRoleProvisioningRule</c>. Implemented by the Mongo repository; test doubles that never provision do not need it.
    /// </summary>
    Task<SystemRoleProvisioning> CreateSystemRoleIfMissingAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct)
        => throw new NotSupportedException("CreateSystemRoleIfMissingAsync is implemented by the Mongo role repository.");
    Task<Role> UpdateAsync(Role role, CancellationToken ct);
    /// <summary>Soft delete (IsDeleted = true). BL-412 F1 — <paramref name="deletedBy"/> is stamped as UpdatedBy in the same update.</summary>
    Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct);
}

/// <summary>The tenant's system role and whether the call that returned it created it (WP-0018-ROLE1).</summary>
public sealed record SystemRoleProvisioning(Role Role, bool Created);
