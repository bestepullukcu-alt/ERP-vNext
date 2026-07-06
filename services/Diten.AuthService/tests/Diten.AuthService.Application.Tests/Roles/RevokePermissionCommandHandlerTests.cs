using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// AG-STEP-010 / MOD-0018-FU13 Group C — role-permission removal must revoke refresh tokens for every tenant-scoped
// holder of the role (OD-FU13-01, B-Option 1).
public sealed class RevokePermissionCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RoleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid PermissionId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid User1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid User2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Successful_revoke_does_permission_revoke_then_holder_lookup_then_per_holder_token_revoke()
    {
        var callLog = new List<string>();
        var rolePerms = new FakeRolePermissionRepository(callLog);
        var userRoles = new FakeUserRoleRepository(callLog) { Holders = [User1, User2] };
        var refreshTokens = new FakeRefreshTokenRepository(callLog);
        var handler = CreateHandler(role: null, rolePerms, userRoles, refreshTokens);

        var result = await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal((RoleId, PermissionId, TenantId), rolePerms.RevokedCall);
        Assert.Equal((RoleId, TenantId), userRoles.LookupCall);
        Assert.Equal(new[] { User1, User2 }, refreshTokens.RevokedUsers);
        // Ordering: permission revoke → holder lookup → token revokes.
        Assert.Equal(
            new[] { "perm-revoke", "holder-lookup", "token-revoke", "token-revoke" },
            callLog);
    }

    [Fact]
    public async Task Tenant_id_comes_from_context_for_all_calls()
    {
        var rolePerms = new FakeRolePermissionRepository();
        var userRoles = new FakeUserRoleRepository { Holders = [User1] };
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, rolePerms, userRoles, refreshTokens);

        await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.Equal(TenantId, rolePerms.RevokedCall!.Value.tenantId);
        Assert.Equal(TenantId, userRoles.LookupCall!.Value.tenantId);
        Assert.All(refreshTokens.RevokeCalls, c => Assert.Equal(TenantId, c.tenantId));
    }

    [Fact]
    public async Task Duplicate_holder_ids_revoke_each_user_only_once()
    {
        var refreshTokens = new FakeRefreshTokenRepository();
        var userRoles = new FakeUserRoleRepository { Holders = [User1, User1, User2, User2] };
        var handler = CreateHandler(role: null, new FakeRolePermissionRepository(), userRoles, refreshTokens);

        await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.Equal(new[] { User1, User2 }, refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Empty_holder_list_is_a_successful_no_op()
    {
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, new FakeRolePermissionRepository(), new FakeUserRoleRepository { Holders = [] }, refreshTokens);

        var result = await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Empty(refreshTokens.RevokedUsers);
    }

    // FIX-ROLEPERM-REVOKE-SYSTEMROLE — a MANUAL grant on a SYSTEM role is removable (the blanket 403 is gone); the
    // S-GUARD still protects System/Module baseline grants on any role.
    [Fact]
    public async Task System_role_manual_grant_revoke_succeeds_204_and_revokes_holder_tokens()
    {
        var systemRole = new Role("admin", "Admin", null, TenantId);
        systemRole.MarkAsSystem();
        var rolePerms = new FakeRolePermissionRepository { TargetGrantSource = GrantSource.Manual };
        var userRoles = new FakeUserRoleRepository { Holders = [User1] };
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(systemRole, rolePerms, userRoles, refreshTokens);

        var result = await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal((RoleId, PermissionId, TenantId), rolePerms.RevokedCall);   // removal happened
        Assert.Equal((RoleId, TenantId), userRoles.LookupCall);                  // holder lookup reached
        Assert.Equal(new[] { User1 }, refreshTokens.RevokedUsers);              // token revoke reached
    }

    [Fact]
    public async Task System_role_system_grant_revoke_is_rejected_409_and_removes_nothing()
    {
        var systemRole = new Role("admin", "Admin", null, TenantId);
        systemRole.MarkAsSystem();
        var rolePerms = new FakeRolePermissionRepository { TargetGrantSource = GrantSource.System };
        var userRoles = new FakeUserRoleRepository { Holders = [User1] };
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(systemRole, rolePerms, userRoles, refreshTokens);

        var result = await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);                 // baseline still protected on a system role
        Assert.Null(rolePerms.RevokedCall);
        Assert.Null(userRoles.LookupCall);
        Assert.Empty(refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Permission_revoke_failure_skips_holder_lookup_and_token_revoke()
    {
        var rolePerms = new FakeRolePermissionRepository { ThrowOnRevoke = true };
        var userRoles = new FakeUserRoleRepository { Holders = [User1] };
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, rolePerms, userRoles, refreshTokens);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None));

        Assert.Null(userRoles.LookupCall);          // holder lookup not reached
        Assert.Empty(refreshTokens.RevokedUsers);   // token revoke not reached
    }

    [Fact]
    public async Task Holder_lookup_failure_is_not_swallowed_and_handler_does_not_succeed()
    {
        var rolePerms = new FakeRolePermissionRepository();
        var userRoles = new FakeUserRoleRepository { ThrowOnLookup = true };
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, rolePerms, userRoles, refreshTokens);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None));

        // Permission removal stands (reached); only the holder lookup failed.
        Assert.Equal((RoleId, PermissionId, TenantId), rolePerms.RevokedCall);
        Assert.Empty(refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Refresh_token_revoke_failure_is_not_swallowed_and_does_not_rollback_permission_removal()
    {
        var rolePerms = new FakeRolePermissionRepository();
        var userRoles = new FakeUserRoleRepository { Holders = [User1, User2] };
        var refreshTokens = new FakeRefreshTokenRepository { ThrowForUser = User2 };
        var handler = CreateHandler(role: null, rolePerms, userRoles, refreshTokens);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None));

        // Permission removal stands; the first user's successful revoke is not rolled back; fail-fast on User2.
        Assert.Equal((RoleId, PermissionId, TenantId), rolePerms.RevokedCall);
        Assert.Equal(new[] { User1 }, refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Cancellation_token_is_propagated_to_all_repositories()
    {
        using var cts = new CancellationTokenSource();
        var rolePerms = new FakeRolePermissionRepository();
        var userRoles = new FakeUserRoleRepository { Holders = [User1] };
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, rolePerms, userRoles, refreshTokens);

        await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), cts.Token);

        Assert.Equal(cts.Token, rolePerms.RevokeToken);
        Assert.Equal(cts.Token, userRoles.LookupToken);
        Assert.All(refreshTokens.RevokeTokens, t => Assert.Equal(cts.Token, t));
    }

    // ── S-GUARD: only Manual grants may be removed via the API ──

    [Fact]
    public async Task System_grant_revoke_is_rejected_409_and_removes_nothing()
    {
        var rolePerms = new FakeRolePermissionRepository { TargetGrantSource = GrantSource.System };
        var userRoles = new FakeUserRoleRepository { Holders = [User1] };
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, rolePerms, userRoles, refreshTokens);

        var result = await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Null(rolePerms.RevokedCall);     // baseline grant remains
        Assert.Null(userRoles.LookupCall);      // no holder token churn
        Assert.Empty(refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Module_grant_revoke_is_rejected_409_and_removes_nothing()
    {
        var rolePerms = new FakeRolePermissionRepository { TargetGrantSource = GrantSource.Module };
        var handler = CreateHandler(role: null, rolePerms, new FakeUserRoleRepository { Holders = [User1] }, new FakeRefreshTokenRepository());

        var result = await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Null(rolePerms.RevokedCall);     // module-sourced grant remains (only the consumer removes it)
    }

    [Fact]
    public async Task Manual_grant_revoke_succeeds_and_revokes_holder_tokens()
    {
        var rolePerms = new FakeRolePermissionRepository { TargetGrantSource = GrantSource.Manual };
        var userRoles = new FakeUserRoleRepository { Holders = [User1] };
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, rolePerms, userRoles, refreshTokens);

        var result = await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal((RoleId, PermissionId, TenantId), rolePerms.RevokedCall);
        Assert.Equal(new[] { User1 }, refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Missing_grant_is_idempotent_204_and_removes_nothing()
    {
        var rolePerms = new FakeRolePermissionRepository { GrantExists = false };
        var userRoles = new FakeUserRoleRepository { Holders = [User1] };
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, rolePerms, userRoles, refreshTokens);

        var result = await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Null(rolePerms.RevokedCall);
        Assert.Null(userRoles.LookupCall);
        Assert.Empty(refreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Manual_grant_revoke_bumps_role_assignment_version()
    {
        var version = new FakeRoleAssignmentVersionService();
        var handler = CreateHandler(role: null, new FakeRolePermissionRepository(), new FakeUserRoleRepository { Holders = [User1] }, new FakeRefreshTokenRepository(), version);

        await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.Equal(1, version.IncrementCount);
        Assert.Contains(TenantId, version.IncrementedTenants);
    }

    [Fact]
    public async Task Idempotent_missing_grant_does_not_bump_version()
    {
        var version = new FakeRoleAssignmentVersionService();
        var rolePerms = new FakeRolePermissionRepository { GrantExists = false };
        var handler = CreateHandler(role: null, rolePerms, new FakeUserRoleRepository(), new FakeRefreshTokenRepository(), version);

        var result = await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.True(result.IsSuccessful); // 204 idempotent no-op
        Assert.Equal(0, version.IncrementCount);
    }

    [Fact]
    public async Task System_role_manual_grant_revoke_bumps_version()
    {
        // FIX-ROLEPERM-REVOKE-SYSTEMROLE — a Manual grant removal on a system role is a real change → version bumps.
        var systemRole = new Role("admin", "Admin", null, TenantId);
        systemRole.MarkAsSystem();
        var version = new FakeRoleAssignmentVersionService();
        var rolePerms = new FakeRolePermissionRepository { TargetGrantSource = GrantSource.Manual };
        var handler = CreateHandler(systemRole, rolePerms, new FakeUserRoleRepository { Holders = [User1] }, new FakeRefreshTokenRepository(), version);

        await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.Equal(1, version.IncrementCount);
        Assert.Contains(TenantId, version.IncrementedTenants);
    }

    [Fact]
    public async Task System_role_system_grant_revoke_does_not_bump_version()
    {
        var systemRole = new Role("admin", "Admin", null, TenantId);
        systemRole.MarkAsSystem();
        var version = new FakeRoleAssignmentVersionService();
        var rolePerms = new FakeRolePermissionRepository { TargetGrantSource = GrantSource.System };
        var handler = CreateHandler(systemRole, rolePerms, new FakeUserRoleRepository(), new FakeRefreshTokenRepository(), version);

        await handler.Handle(new RevokePermissionCommand(RoleId, PermissionId), CancellationToken.None);

        Assert.Equal(0, version.IncrementCount); // 409 baseline-protected → no change
    }

    private static RevokePermissionCommandHandler CreateHandler(
        Role? role,
        FakeRolePermissionRepository rolePerms,
        FakeUserRoleRepository userRoles,
        FakeRefreshTokenRepository refreshTokens,
        FakeRoleAssignmentVersionService? version = null)
    {
        var tenantContext = new TestTenantContext();
        tenantContext.SetTenant(TenantId);
        return new RevokePermissionCommandHandler(
            new FakeRoleRepository(role),
            rolePerms,
            userRoles,
            refreshTokens,
            version ?? new FakeRoleAssignmentVersionService(),
            tenantContext,
            new FakePermissionRepository(),
            new NoOpRbacAuditRecorder());
    }

    // FEAT-AUDIT-RBAC — permission lookup is best-effort (for the audit key); audit itself asserted in
    // RbacAuditRecorderTests. Minimal no-op fakes keep this handler's tests focused on the revoke behaviour.
    private sealed class FakePermissionRepository : IPermissionRepository
    {
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct) => Task.FromResult<Permission?>(null);
        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class NoOpRbacAuditRecorder : IRbacAuditRecorder
    {
        public Task RecordAsync(string eventName, Guid tenantId, object metadata, CancellationToken ct = default) => Task.CompletedTask;
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

    private sealed class FakeRolePermissionRepository(List<string>? callLog = null) : IRolePermissionRepository
    {
        public bool ThrowOnRevoke { get; init; }
        // S-GUARD: the grant the handler inspects via GetByRoleAsync. Default Manual so the existing
        // revoke flow proceeds; set GrantExists=false to simulate "nothing to remove".
        public GrantSource TargetGrantSource { get; init; } = GrantSource.Manual;
        public bool GrantExists { get; init; } = true;
        public (Guid roleId, Guid permissionId, Guid tenantId)? RevokedCall { get; private set; }
        public CancellationToken RevokeToken { get; private set; }

        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct)
        {
            if (ThrowOnRevoke) throw new InvalidOperationException("permission revoke failed");
            RevokedCall = (roleId, permissionId, tenantId);
            RevokeToken = ct;
            callLog?.Add("perm-revoke");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
        {
            IReadOnlyList<RolePermission> rows = GrantExists
                ? [new RolePermission(roleId, PermissionId, tenantId, "system", TargetGrantSource, sourceModuleCode: null)]
                : [];
            return Task.FromResult(rows);
        }

        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => Task.FromResult(0L);
    }

    private sealed class FakeUserRoleRepository(List<string>? callLog = null) : IUserRoleRepository
    {
        public IReadOnlyCollection<Guid> Holders { get; init; } = [];
        public bool ThrowOnLookup { get; init; }
        public (Guid roleId, Guid tenantId)? LookupCall { get; private set; }
        public CancellationToken LookupToken { get; private set; }

        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
        {
            if (ThrowOnLookup) throw new InvalidOperationException("holder lookup failed");
            LookupCall = (roleId, tenantId);
            LookupToken = ct;
            callLog?.Add("holder-lookup");
            return Task.FromResult(Holders);
        }

        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task AssignAsync(UserRole userRole, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRefreshTokenRepository(List<string>? callLog = null) : IRefreshTokenRepository
    {
        public Guid? ThrowForUser { get; init; }
        public List<(Guid userId, Guid tenantId)> RevokeCalls { get; } = [];
        public List<CancellationToken> RevokeTokens { get; } = [];
        public IReadOnlyList<Guid> RevokedUsers => RevokeCalls.Select(c => c.userId).ToList();

        public Task RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            if (ThrowForUser == userId) throw new InvalidOperationException("refresh-token revoke failed");
            RevokeCalls.Add((userId, tenantId));
            RevokeTokens.Add(ct);
            callLog?.Add("token-revoke");
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(string token, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class TestTenantContext : ITenantContext
    {
        private Guid _tenantId;
        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { _tenantId = tenantId; IsResolved = true; }
        public void SetPlatformContext(Guid targetTenantId) => SetTenant(targetTenantId);
    }
}
