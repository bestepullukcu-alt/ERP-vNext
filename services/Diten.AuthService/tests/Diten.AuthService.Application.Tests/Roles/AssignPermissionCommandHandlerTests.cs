using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// FEAT-ROLEPERMS-TENANT-SCOPE — manual assignment to a tenant role always requires a tenant-assignable
// permission, including requests initiated from platform context. The guard is assign-only; revoke stays
// unguarded so an invalid historical grant can still be removed (covered by RevokePermissionCommandHandlerTests).
public sealed class AssignPermissionCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RoleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid PermissionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static Permission TenantPermission() => new("auth", "users", "read", "Read User", null);
    private static Permission PlatformPermission() => new("platform", "tenants", "read", "Read Tenant", null);

    [Fact]
    public async Task Tenant_context_rejects_platform_permission_with_403_and_assigns_nothing()
    {
        var rolePerms = new FakeRolePermissionRepository();
        var version = new FakeRoleAssignmentVersionService();
        var handler = CreateHandler(Role(), PlatformPermission(), rolePerms, version, platformContext: false);

        var result = await handler.Handle(new AssignPermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Null(rolePerms.AssignedCall);
        Assert.Equal(0, version.IncrementCount);
    }

    [Fact]
    public async Task Tenant_context_allows_tenant_assignable_permission_and_bumps_version()
    {
        var rolePerms = new FakeRolePermissionRepository();
        var version = new FakeRoleAssignmentVersionService();
        var handler = CreateHandler(Role(), TenantPermission(), rolePerms, version, platformContext: false);

        var result = await handler.Handle(new AssignPermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal((RoleId, PermissionId, TenantId), rolePerms.AssignedCall);
        Assert.Equal(1, version.IncrementCount);
    }

    [Fact]
    public async Task Platform_context_cannot_assign_non_tenant_permission_to_tenant_role()
    {
        var rolePerms = new FakeRolePermissionRepository();
        var version = new FakeRoleAssignmentVersionService();
        var handler = CreateHandler(Role(), PlatformPermission(), rolePerms, version, platformContext: true);

        var result = await handler.Handle(new AssignPermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Null(rolePerms.AssignedCall);
    }

    [Fact]
    public async Task Tenant_context_rejects_unknown_permission_with_403()
    {
        var rolePerms = new FakeRolePermissionRepository();
        var handler = CreateHandler(Role(), permission: null, rolePerms, new FakeRoleAssignmentVersionService(), platformContext: false);

        var result = await handler.Handle(new AssignPermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Null(rolePerms.AssignedCall);
    }

    [Fact]
    public async Task Missing_role_returns_404_and_assigns_nothing()
    {
        var rolePerms = new FakeRolePermissionRepository();
        var handler = CreateHandler(role: null, TenantPermission(), rolePerms, new FakeRoleAssignmentVersionService(), platformContext: false);

        var result = await handler.Handle(new AssignPermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Null(rolePerms.AssignedCall);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Missing_or_empty_authenticated_actor_fails_closed(string? actor)
    {
        var rolePerms = new FakeRolePermissionRepository();
        var handler = CreateHandler(Role(), TenantPermission(), rolePerms,
            new FakeRoleAssignmentVersionService(), false,
            actor is null ? null : Guid.Parse(actor),
            authenticated: actor is not null);

        var result = await handler.Handle(new AssignPermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(401, result.StatusCode);
        Assert.Null(rolePerms.AssignedCall);
    }

    [Fact]
    public async Task First_grant_attributes_actor_and_changes_version_and_audit_once()
    {
        var rolePerms = new FakeRolePermissionRepository();
        var version = new FakeRoleAssignmentVersionService();
        var audit = new CountingRbacAuditRecorder();
        var handler = CreateHandler(Role(), TenantPermission(), rolePerms, version, false, ActorId, audit);

        var result = await handler.Handle(new AssignPermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(ActorId.ToString("D"), rolePerms.AssignedRolePermission!.AssignedBy);
        Assert.Equal(1, version.IncrementCount);
        Assert.Equal(1, audit.Count);
    }

    [Fact]
    public async Task Exact_duplicate_is_stable_no_op_without_version_or_audit()
    {
        var rolePerms = new FakeRolePermissionRepository { Exists = true };
        var version = new FakeRoleAssignmentVersionService();
        var audit = new CountingRbacAuditRecorder();
        var handler = CreateHandler(Role(), TenantPermission(), rolePerms, version, false, ActorId, audit);

        var result = await handler.Handle(new AssignPermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Null(rolePerms.AssignedCall);
        Assert.Equal(0, version.IncrementCount);
        Assert.Equal(0, audit.Count);
    }

    [Fact]
    public async Task Unique_index_race_for_exact_tuple_is_stable_no_op()
    {
        var rolePerms = new FakeRolePermissionRepository { RaceDuplicate = true };
        var version = new FakeRoleAssignmentVersionService();
        var audit = new CountingRbacAuditRecorder();
        var handler = CreateHandler(Role(), TenantPermission(), rolePerms, version, false, ActorId, audit);

        var result = await handler.Handle(new AssignPermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(0, version.IncrementCount);
        Assert.Equal(0, audit.Count);
    }

    private static Role Role() => new("admin", "Admin", null, TenantId);

    private static AssignPermissionCommandHandler CreateHandler(
        Role? role,
        Permission? permission,
        FakeRolePermissionRepository rolePerms,
        FakeRoleAssignmentVersionService version,
        bool platformContext,
        Guid? actorId = null,
        IRbacAuditRecorder? audit = null,
        bool authenticated = true)
    {
        var tenantContext = new TestTenantContext();
        if (platformContext) tenantContext.SetPlatformContext(TenantId);
        else tenantContext.SetTenant(TenantId);

        return new AssignPermissionCommandHandler(
            new FakeRoleRepository(role),
            new FakePermissionRepository(permission),
            rolePerms,
            version,
            tenantContext,
            audit ?? new NoOpRbacAuditRecorder(),
            new FakeCurrentUserAccessor(authenticated ? actorId ?? ActorId : null));
    }

    // ── Minimal inline fakes (codebase convention: hand-written, no Moq) ──

    private sealed class FakeRoleRepository(Role? role) : IRoleRepository
    {
        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.FromResult(role);
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakePermissionRepository(Permission? permission) : IPermissionRepository
    {
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult(permission);
        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRolePermissionRepository : IRolePermissionRepository
    {
        public (Guid roleId, Guid permissionId, Guid tenantId)? AssignedCall { get; private set; }
        public RolePermission? AssignedRolePermission { get; private set; }
        public bool Exists { get; init; }
        public bool RaceDuplicate { get; init; }

        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct)
        {
            AssignedCall = (rolePermission.RoleId, rolePermission.PermissionId, rolePermission.TenantId);
            AssignedRolePermission = rolePermission;
            return Task.CompletedTask;
        }

        public Task<bool> TryAssignAsync(RolePermission rolePermission, CancellationToken ct)
        {
            if (Exists || RaceDuplicate) return Task.FromResult(false);
            AssignedCall = (rolePermission.RoleId, rolePermission.PermissionId, rolePermission.TenantId);
            AssignedRolePermission = rolePermission;
            return Task.FromResult(true);
        }

        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class NoOpRbacAuditRecorder : IRbacAuditRecorder
    {
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class CountingRbacAuditRecorder : IRbacAuditRecorder
    {
        public int Count { get; private set; }
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default)
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCurrentUserAccessor(Guid? userId) : ICurrentUserAccessor
    {
        public Guid? UserId { get; } = userId;
    }

    private sealed class TestTenantContext : ITenantContext
    {
        private Guid _tenantId;
        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext { get; private set; }
        public Guid? TargetTenantId { get; private set; }
        public void SetTenant(Guid tenantId) { _tenantId = tenantId; IsResolved = true; IsPlatformContext = false; TargetTenantId = null; }
        public void SetPlatformContext(Guid targetTenantId) { _tenantId = targetTenantId; IsResolved = true; IsPlatformContext = true; TargetTenantId = targetTenantId; }
    }
}
