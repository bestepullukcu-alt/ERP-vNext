using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Diten.AuthService.Application.Tests.Testing;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// A TENANT MUST NOT BE ABLE TO LOCK ITSELF OUT — found in live testing, 2026-09-23.
///
/// <para>The handler was one line: soft-delete, 204. The owner deleted every account in the tenant, including
/// their own and the administrator's, and afterwards nobody could sign in. Nothing refused and nothing warned.</para>
///
/// <para>What is pinned here is the SHAPE of the two refusals, not a role name: the capability
/// <c>auth.users.create</c> is what makes recovery possible, so its last live holder is the one account the
/// tenant may not remove — whatever the role is called.</para>
/// </summary>
public sealed class DeleteUserGuardTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task You_cannot_delete_the_account_you_are_signed_in_with()
    {
        var me = User("me@acme.test");
        var other = User("other@acme.test");
        var repo = new InMemoryUserRepository([me, other]);

        // Both hold the recovery capability, so ONLY the self rule can refuse here.
        var result = await Handler(repo, actor: me.Id, holders: [me.Id, other.Id]).Handle(
            new DeleteUserCommand(me.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains(result.ErrorCodes, e => e.Code == DeleteUserCommandHandler.SelfDeleteCode);
        Assert.False(me.IsDeleted);
    }

    [Fact]
    public async Task You_cannot_delete_the_last_account_that_can_create_users()
    {
        var lastAdmin = User("admin@acme.test");
        var plain = User("plain@acme.test");
        var repo = new InMemoryUserRepository([lastAdmin, plain]);

        var result = await Handler(repo, actor: plain.Id, holders: [lastAdmin.Id]).Handle(
            new DeleteUserCommand(lastAdmin.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains(result.ErrorCodes, e => e.Code == DeleteUserCommandHandler.LastStewardCode);
        Assert.False(lastAdmin.IsDeleted);
    }

    [Fact]
    public async Task But_one_of_two_can_go()
    {
        var first = User("a@acme.test");
        var second = User("b@acme.test");
        var actor = User("c@acme.test");
        var repo = new InMemoryUserRepository([first, second, actor]);

        var result = await Handler(repo, actor: actor.Id, holders: [first.Id, second.Id]).Handle(
            new DeleteUserCommand(first.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(first.IsDeleted);
        Assert.False(second.IsDeleted);
    }

    [Fact]
    public async Task A_role_row_pointing_at_a_deleted_user_does_not_count_as_a_holder()
    {
        /*
         * ⚠ THE ASSIGNMENT IS NOT THE PERSON. Counting rows instead of live users would let the tenant delete its
         * last real administrator while the guard pointed at a ghost — the exact failure this guard exists for.
         */
        var lastAdmin = User("admin@acme.test");
        var ghost = User("ghost@acme.test");
        ghost.IsDeleted = true;
        var repo = new InMemoryUserRepository([lastAdmin, ghost]);

        var result = await Handler(repo, actor: Guid.NewGuid(), holders: [lastAdmin.Id, ghost.Id]).Handle(
            new DeleteUserCommand(lastAdmin.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Contains(result.ErrorCodes, e => e.Code == DeleteUserCommandHandler.LastStewardCode);
    }

    [Fact]
    public async Task A_role_without_the_capability_keeps_nobody_alive()
    {
        // The plain role holds a different permission; its members cannot put an account back.
        var lastAdmin = User("admin@acme.test");
        var plain = User("plain@acme.test");
        var repo = new InMemoryUserRepository([lastAdmin, plain]);

        var result = await Handler(repo, actor: plain.Id, holders: [lastAdmin.Id], plainRoleHolders: [plain.Id]).Handle(
            new DeleteUserCommand(lastAdmin.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Contains(result.ErrorCodes, e => e.Code == DeleteUserCommandHandler.LastStewardCode);
    }

    // ── BL-558 (WP-USERS-ROLES-SMALL-01): the guard protects the last HOLDER; it never blocks someone who holds nothing ──

    [Fact]
    public async Task BL558_a_user_without_the_capability_is_deleted_in_a_tenant_that_has_no_holder_left()
    {
        // The live case (2026-10-05): the tenant's administrator was already gone; deleting a role-less "deneme" user was
        // refused with "this is the last account that can create users" — blaming someone who could create nobody.
        var trial = User("deneme@acme.test");
        var actor = User("actor@acme.test");
        var repo = new InMemoryUserRepository([trial, actor]);

        var result = await Handler(repo, actor: actor.Id, holders: []).Handle(
            new DeleteUserCommand(trial.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(",", result.ErrorCodes.Select(e => e.Code)));
        Assert.Equal(204, result.StatusCode);
        Assert.True(trial.IsDeleted);
    }

    [Fact]
    public async Task BL558_the_only_holder_cannot_delete_themself_the_self_rule_answers_first()
    {
        var onlyAdmin = User("admin@acme.test");
        var repo = new InMemoryUserRepository([onlyAdmin]);

        var result = await Handler(repo, actor: onlyAdmin.Id, holders: [onlyAdmin.Id]).Handle(
            new DeleteUserCommand(onlyAdmin.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains(result.ErrorCodes, e => e.Code == DeleteUserCommandHandler.SelfDeleteCode);
        Assert.False(onlyAdmin.IsDeleted);
    }

    [Fact]
    public async Task BL558_the_only_holder_cannot_be_deleted_by_someone_else()
    {
        // The candidate is excluded from "someone else holds it" — counting them would let them keep themselves alive.
        var onlyAdmin = User("admin@acme.test");
        var plain = User("plain@acme.test");
        var repo = new InMemoryUserRepository([onlyAdmin, plain]);

        var result = await Handler(repo, actor: plain.Id, holders: [onlyAdmin.Id]).Handle(
            new DeleteUserCommand(onlyAdmin.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Contains(result.ErrorCodes, e => e.Code == DeleteUserCommandHandler.LastStewardCode);
        Assert.False(onlyAdmin.IsDeleted);
    }

    [Fact]
    public async Task BL558_one_of_two_holders_is_deleted()
    {
        var first = User("a@acme.test");
        var second = User("b@acme.test");
        var actor = User("c@acme.test");
        var repo = new InMemoryUserRepository([first, second, actor]);

        var result = await Handler(repo, actor: actor.Id, holders: [first.Id, second.Id]).Handle(
            new DeleteUserCommand(second.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(second.IsDeleted);
        Assert.False(first.IsDeleted);
    }

    [Fact]
    public async Task BL558_a_capability_held_only_through_a_deleted_role_does_not_make_the_candidate_a_holder()
    {
        // RoleRepository.GetAllByTenantAsync returns live roles only (IsDeleted == false); the fake filters the same way.
        // The role is PRESENT and soft-deleted — leaving it out would make this test say nothing.
        var candidate = User("former-admin@acme.test");
        var actor = User("actor@acme.test");
        var repo = new InMemoryUserRepository([candidate, actor]);

        var result = await Handler(repo, actor: actor.Id, holders: [candidate.Id], adminRoleDeleted: true).Handle(
            new DeleteUserCommand(candidate.Id), CancellationToken.None);

        Assert.True(result.IsSuccessful, string.Join(",", result.ErrorCodes.Select(e => e.Code)));
        Assert.True(candidate.IsDeleted);
    }

    [Fact]
    public async Task BL558_a_holder_in_another_tenant_does_not_keep_this_tenant_alive()
    {
        // Tenant isolation: the role row names a user of tenant B; the user read is scoped to tenant A and finds nobody.
        var onlyAdmin = User("admin@acme.test");
        var actor = User("actor@acme.test");
        var otherTenantAdmin = new User("admin@other.test", "hash:x", "Ad", "Soyad", TenantB);
        var repo = new InMemoryUserRepository([onlyAdmin, actor, otherTenantAdmin]);

        var result = await Handler(repo, actor: actor.Id, holders: [onlyAdmin.Id, otherTenantAdmin.Id]).Handle(
            new DeleteUserCommand(onlyAdmin.Id), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Contains(result.ErrorCodes, e => e.Code == DeleteUserCommandHandler.LastStewardCode);
        Assert.False(onlyAdmin.IsDeleted);
        Assert.False(otherTenantAdmin.IsDeleted);
    }

    [Fact]
    public async Task Deleting_someone_who_is_not_there_is_not_a_success()
    {
        var repo = new InMemoryUserRepository([]);

        var result = await Handler(repo, actor: Guid.NewGuid(), holders: []).Handle(
            new DeleteUserCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
    }

    // ── helpers ──

    private static User User(string email) => new(email, "hash:x", "Ad", "Soyad", TenantA);

    /// <summary>
    /// Two roles, built here so the test owns their ids: one carries <c>auth.users.create</c>, one does not.
    /// The guard must find the capability through the role, never through a name.
    /// </summary>
    private static DeleteUserCommandHandler Handler(
        IUserRepository users, Guid actor, Guid[] holders, Guid[]? plainRoleHolders = null, bool adminRoleDeleted = false)
    {
        var admin = new Role("Yöneticiler", "Yöneticiler", null, TenantA) { IsDeleted = adminRoleDeleted };
        var plain = new Role("Okuyucular", "Okuyucular", null, TenantA);

        // WP-USERS-ROLES-SMALL-02 — the REAL shared rule over these fakes: the handler no longer counts on its own.
        return new DeleteUserCommandHandler(
            users,
            new RecoveryCapabilityGuard(
                users,
                new FakeUserRoles(admin.Id, holders, plain.Id, plainRoleHolders ?? []),
                new FakeRoles(admin, plain),
                new FakeRolePermissions(admin.Id)),
            TenantContext(),
            new FakeCurrentUser(actor),
            UserAuditForTests.None(),
            NullLogger<DeleteUserCommandHandler>.Instance);
    }

    private static ITenantContext TenantContext()
    {
        var ctx = new FakeTenantContext();
        ctx.SetTenant(TenantA);
        return ctx;
    }

    private sealed class FakeTenantContext : ITenantContext
    {
        private Guid _tenantId;
        public Guid TenantId => IsResolved ? _tenantId : throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved { get; private set; }
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { _tenantId = tenantId; IsResolved = true; }
        public void SetPlatformContext(Guid targetTenantId) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUser(Guid id) : ICurrentUserAccessor
    {
        public Guid? UserId => id;
    }

    private sealed class FakeRoles(Role admin, Role plain) : IRoleRepository
    {
        // As RoleRepository.GetAllByTenantAsync: this tenant's live roles only (TenantId == tenantId, IsDeleted == false).
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IEnumerable<Role>>(new[] { admin, plain }.Where(r => r.TenantId == tenantId && !r.IsDeleted).ToList());

        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.FromResult<Role?>(null);
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => Task.FromResult<Role?>(null);
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => Task.FromResult(role);
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => Task.FromResult(role);
        public Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeRolePermissions(Guid adminRoleId) : IRolePermissionRepository
    {
        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IEnumerable<string>>(roleId == adminRoleId
                ? [DeleteUserCommandHandler.RecoveryPermission, "auth.users.read"]
                : ["auth.users.read"]);

        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IEnumerable<string>>([]);
        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RolePermission>>([]);
        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => Task.FromResult(0L);
    }

    private sealed class FakeUserRoles(Guid adminRoleId, Guid[] adminHolders, Guid plainRoleId, Guid[] plainHolders)
        : IUserRoleRepository
    {
        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IEnumerable<string>>([]);
        public Task AssignAsync(UserRole userRole, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(
                roleId == adminRoleId ? adminHolders : roleId == plainRoleId ? plainHolders : []);
    }
}
