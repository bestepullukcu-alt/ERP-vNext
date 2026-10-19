using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ROLES-SMALL-02 — one tenant's people, roles, grants and assignments, behind fakes that answer the way the
/// Mongo repositories do: users through <see cref="InMemoryUserRepository"/> (tenant + IsDeleted filtered; IsActive is
/// CARRIED, never filtered — filtering is the rule's job), roles tenant + IsDeleted filtered, every read tenant-scoped.
/// The write calls of the five paths are counted, so a refusal can be shown to have written nothing.
/// </summary>
internal sealed class StewardWorld
{
    public static readonly Guid TenantA = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000558");
    public static readonly Guid TenantB = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000558");

    private readonly List<User> _users = [];
    private readonly List<Role> _roles = [];
    private readonly List<(Guid RoleId, Permission Permission, GrantSource Source)> _grants = [];
    private readonly List<(Guid UserId, Guid RoleId, Guid TenantId)> _assignments = [];
    private InMemoryUserRepository? _userRepository;

    public static readonly Permission CreateUsers = new("auth", "users", "create", "Create users", null);
    public static readonly Permission ReadUsers = new("auth", "users", "read", "Read users", null);

    public InMemoryUserRepository UserRepository => _userRepository ??= new InMemoryUserRepository(_users);
    public Roles RoleRepository => new(this);
    public RolePermissions RolePermissionRepository { get; }
    public UserRoles UserRoleRepository { get; }
    public RefreshTokens Tokens { get; } = new();
    public FakeRoleAssignmentVersionService Version { get; } = new();
    public int RoleDeletes { get; private set; }

    public StewardWorld()
    {
        RolePermissionRepository = new RolePermissions(this);
        UserRoleRepository = new UserRoles(this);
    }

    public IRecoveryCapabilityGuard Guard() =>
        new RecoveryCapabilityGuard(UserRepository, UserRoleRepository, RoleRepository, RolePermissionRepository);

    public User Person(string name, bool active = true, Guid? tenantId = null)
    {
        if (_userRepository is not null) throw new InvalidOperationException("Add people before the repository is read.");
        var user = new User($"{name}.{Guid.NewGuid():N}@steward.test", "hash:x", "Ad", name, tenantId ?? TenantA);
        if (!active) user.Deactivate();
        _users.Add(user);
        return user;
    }

    /// <summary>A custom role carrying auth.users.create (a MANUAL grant, removable by hand) and auth.users.read.</summary>
    public Role StewardRole(string name = "Admins") => RoleWith(name, (CreateUsers, GrantSource.Manual), (ReadUsers, GrantSource.Manual));

    /// <summary>A custom role without the recovery capability.</summary>
    public Role PlainRole(string name = "Readers") => RoleWith(name, (ReadUsers, GrantSource.Manual));

    public Role RoleWith(string name, params (Permission Permission, GrantSource Source)[] grants)
    {
        var role = new Role(name, name, null, TenantA);
        _roles.Add(role);
        foreach (var (permission, source) in grants) _grants.Add((role.Id, permission, source));
        return role;
    }

    /// <summary>An assignment row of tenant A — also for a user of another tenant (the cross-tenant case).</summary>
    public void Assign(User user, Role role) => _assignments.Add((user.Id, role.Id, TenantA));

    public bool HasAssignment(User user, Role role) => _assignments.Any(a => a.UserId == user.Id && a.RoleId == role.Id);
    public bool HasGrant(Role role, Permission permission) => _grants.Any(g => g.RoleId == role.Id && g.Permission.Id == permission.Id);

    internal sealed class Roles(StewardWorld world) : IRoleRepository
    {
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IEnumerable<Role>>(world._roles.Where(r => r.TenantId == tenantId && !r.IsDeleted).ToList());

        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) =>
            Task.FromResult(world._roles.FirstOrDefault(r => r.Id == id && r.TenantId == tenantId && !r.IsDeleted));

        public Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct)
        {
            world.RoleDeletes++;
            var role = world._roles.FirstOrDefault(r => r.Id == id && r.TenantId == tenantId);
            if (role is not null) role.IsDeleted = true;
            return Task.CompletedTask;
        }

        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
    }

    internal sealed class RolePermissions(StewardWorld world) : IRolePermissionRepository
    {
        public int Revokes { get; private set; }

        private bool RoleInTenant(Guid roleId, Guid tenantId) => world._roles.Any(r => r.Id == roleId && r.TenantId == tenantId);

        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IEnumerable<string>>(RoleInTenant(roleId, tenantId)
                ? world._grants.Where(g => g.RoleId == roleId).Select(g => g.Permission.Key).ToList()
                : []);

        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RolePermission>>(RoleInTenant(roleId, tenantId)
                ? world._grants.Where(g => g.RoleId == roleId)
                    .Select(g => new RolePermission(roleId, g.Permission.Id, tenantId, "steward-world", g.Source, null)).ToList()
                : []);

        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct)
        {
            Revokes++;
            world._grants.RemoveAll(g => g.RoleId == roleId && g.Permission.Id == permissionId && RoleInTenant(roleId, tenantId));
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => throw new NotSupportedException();
    }

    internal sealed class UserRoles(StewardWorld world) : IUserRoleRepository
    {
        public int Revokes { get; private set; }

        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(world._assignments
                .Where(a => a.RoleId == roleId && a.TenantId == tenantId).Select(a => a.UserId).ToList());

        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IEnumerable<string>>(world._assignments
                .Where(a => a.UserId == userId && a.TenantId == tenantId)
                .Select(a => world._roles.First(r => r.Id == a.RoleId).Name).ToList());

        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct)
        {
            Revokes++;
            world._assignments.RemoveAll(a => a.UserId == userId && a.RoleId == roleId && a.TenantId == tenantId);
            return Task.CompletedTask;
        }

        public Task AssignAsync(UserRole userRole, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    internal sealed class Catalog : IPermissionRepository
    {
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(new[] { CreateUsers, ReadUsers }.FirstOrDefault(p => p.Id == id));

        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct) => throw new NotSupportedException();
    }

    internal sealed class RefreshTokens : IRefreshTokenRepository
    {
        public int Revocations { get; private set; }

        public Task<long> RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            Revocations++;
            return Task.FromResult(0L);
        }

        public Task<long> RevokeLiveSessionsAsync(Guid userId, Guid tenantId, string reason, CancellationToken ct)
        {
            Revocations++;
            return Task.FromResult(0L);
        }

        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> RevokeIfLiveAsync(string token, string reason, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> TryRotateAsync(Guid tokenId, string replacedByTokenHash, string? revokedByIp, CancellationToken ct) => throw new NotSupportedException();
    }
}
