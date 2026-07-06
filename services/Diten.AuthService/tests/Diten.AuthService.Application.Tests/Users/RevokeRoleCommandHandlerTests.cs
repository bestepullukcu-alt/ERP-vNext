using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Users;

// AG-STEP-010 / MOD-0018-FU13 Group B — user-role removal must revoke the user's refresh tokens (OD-FU13-01).
public sealed class RevokeRoleCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid RoleId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task Successful_revoke_calls_role_revoke_then_refresh_token_revoke()
    {
        var callLog = new List<string>();
        var userRoles = new FakeUserRoleRepository(callLog);
        var refreshTokens = new FakeRefreshTokenRepository(callLog);
        var handler = CreateHandler(role: null, userRoles, refreshTokens);

        var result = await handler.Handle(new RevokeRoleCommand(UserId, RoleId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal((UserId, RoleId, TenantId), userRoles.RevokedCall);
        Assert.Equal((UserId, TenantId), refreshTokens.RevokeAllCall);
        // Ordering: role revoke happens before the refresh-token revoke (shared call log).
        Assert.Equal(new[] { "user-role-revoke", "refresh-token-revoke-all" }, callLog);
    }

    [Fact]
    public async Task Tenant_id_comes_from_context_not_request_for_both_calls()
    {
        var userRoles = new FakeUserRoleRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, userRoles, refreshTokens);

        await handler.Handle(new RevokeRoleCommand(UserId, RoleId), CancellationToken.None);

        Assert.Equal(TenantId, userRoles.RevokedCall!.Value.tenantId);
        Assert.Equal(TenantId, refreshTokens.RevokeAllCall!.Value.tenantId);
    }

    [Fact]
    public async Task Refresh_token_revoke_targets_only_the_requested_user()
    {
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, new FakeUserRoleRepository(), refreshTokens);

        await handler.Handle(new RevokeRoleCommand(UserId, RoleId), CancellationToken.None);

        Assert.Equal(UserId, refreshTokens.RevokeAllCall!.Value.userId);
        Assert.NotEqual(Guid.NewGuid(), refreshTokens.RevokeAllCall!.Value.userId);
    }

    // FIX-USERROLE-REVOKE-SYSTEMROLE — revoking a user↔role assignment on a SYSTEM role now succeeds (symmetric
    // with the unguarded assign): it only narrows the user's permissions, so no 403 block. Full flow still runs.
    [Fact]
    public async Task System_role_revoke_succeeds_and_runs_full_flow()
    {
        var systemRole = new Role("admin", "Admin", null, TenantId);
        systemRole.MarkAsSystem();
        var userRoles = new FakeUserRoleRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(systemRole, userRoles, refreshTokens);

        var result = await handler.Handle(new RevokeRoleCommand(UserId, RoleId), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(204, result.StatusCode);
        Assert.Equal((UserId, RoleId, TenantId), userRoles.RevokedCall);        // role revoke reached
        Assert.Equal((UserId, TenantId), refreshTokens.RevokeAllCall);          // refresh-token revoke reached
    }

    [Fact]
    public async Task Refresh_token_revoke_failure_is_not_swallowed_and_handler_does_not_succeed()
    {
        var userRoles = new FakeUserRoleRepository();
        var refreshTokens = new FakeRefreshTokenRepository { ThrowOnRevokeAll = true };
        var handler = CreateHandler(role: null, userRoles, refreshTokens);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new RevokeRoleCommand(UserId, RoleId), CancellationToken.None));

        // Role removal stands (was reached) and is not rolled back; only the refresh-token revoke failed.
        Assert.Equal((UserId, RoleId, TenantId), userRoles.RevokedCall);
    }

    [Fact]
    public async Task Cancellation_token_is_propagated_to_both_repositories()
    {
        using var cts = new CancellationTokenSource();
        var userRoles = new FakeUserRoleRepository();
        var refreshTokens = new FakeRefreshTokenRepository();
        var handler = CreateHandler(role: null, userRoles, refreshTokens);

        await handler.Handle(new RevokeRoleCommand(UserId, RoleId), cts.Token);

        Assert.Equal(cts.Token, userRoles.RevokedToken);
        Assert.Equal(cts.Token, refreshTokens.RevokeAllToken);
    }

    [Fact]
    public async Task Successful_revoke_bumps_role_assignment_version()
    {
        var version = new FakeRoleAssignmentVersionService();
        var handler = CreateHandler(role: null, new FakeUserRoleRepository(), new FakeRefreshTokenRepository(), version);

        await handler.Handle(new RevokeRoleCommand(UserId, RoleId), CancellationToken.None);

        Assert.Equal(1, version.IncrementCount);
        Assert.Contains(TenantId, version.IncrementedTenants);
    }

    // FIX-USERROLE-REVOKE-SYSTEMROLE — system-role revoke is no longer blocked, so it bumps the version like any
    // other revoke (cached authorization snapshots must invalidate).
    [Fact]
    public async Task System_role_revoke_bumps_version()
    {
        var systemRole = new Role("admin", "Admin", null, TenantId);
        systemRole.MarkAsSystem();
        var version = new FakeRoleAssignmentVersionService();
        var handler = CreateHandler(systemRole, new FakeUserRoleRepository(), new FakeRefreshTokenRepository(), version);

        await handler.Handle(new RevokeRoleCommand(UserId, RoleId), CancellationToken.None);

        Assert.Equal(1, version.IncrementCount);
        Assert.Contains(TenantId, version.IncrementedTenants);
    }

    private static RevokeRoleCommandHandler CreateHandler(
        Role? role,
        FakeUserRoleRepository userRoles,
        FakeRefreshTokenRepository refreshTokens,
        FakeRoleAssignmentVersionService? version = null)
    {
        var tenantContext = new TestTenantContext();
        tenantContext.SetTenant(TenantId);
        return new RevokeRoleCommandHandler(
            new FakeRoleRepository(role),
            userRoles,
            refreshTokens,
            version ?? new FakeRoleAssignmentVersionService(),
            tenantContext,
            new NoOpRbacAuditRecorder(),
            NullLogger<RevokeRoleCommandHandler>.Instance);
    }

    // FEAT-AUDIT-RBAC — audit is instrumented but not asserted here (see RbacAuditRecorderTests); no-op keeps focus.
    private sealed class NoOpRbacAuditRecorder : Diten.AuthService.Application.Common.Interfaces.IRbacAuditRecorder
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

    private sealed class FakeUserRoleRepository(List<string>? callLog = null) : IUserRoleRepository
    {
        public (Guid userId, Guid roleId, Guid tenantId)? RevokedCall { get; private set; }
        public CancellationToken RevokedToken { get; private set; }

        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct)
        {
            RevokedCall = (userId, roleId, tenantId);
            RevokedToken = ct;
            callLog?.Add("user-role-revoke");
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task AssignAsync(UserRole userRole, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRefreshTokenRepository(List<string>? callLog = null) : IRefreshTokenRepository
    {
        public bool ThrowOnRevokeAll { get; init; }
        public (Guid userId, Guid tenantId)? RevokeAllCall { get; private set; }
        public CancellationToken RevokeAllToken { get; private set; }

        public Task RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
        {
            if (ThrowOnRevokeAll)
                throw new InvalidOperationException("refresh-token revoke failed");
            RevokeAllCall = (userId, tenantId);
            RevokeAllToken = ct;
            callLog?.Add("refresh-token-revoke-all");
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
