using System.Security.Claims;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;
using Diten.AuthService.Application.Tests.Users;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Auth;

/// <summary>
/// BL-521 FIX4 item 2 — EVERY PLATFORM SIGN-IN AND REFRESH CONVERGES AUTH'S ROLES TO THE RECORD BEFORE MINTING. Auth's copy
/// of an administrator's roles is written by Platform's sync, which can be lost (a write refused after the sync, a
/// convergence that fails too, a role change that never syncs). Before, the token carried whatever the copy said — the
/// reported case left SuperAdmin in every new token of an administrator whose record says SupportAdmin. Now the record's
/// roles (sent with Platform's status answer) are written to the copy first and the token is minted from it; an older
/// Platform that sends no roles converges nothing. Measured on the production handlers over an in-memory role store.
///
/// <para>FIX5 item 3 — AT MINT THERE IS NO PROVISIONING DEFAULT: a record whose answer names no platform role (an empty list, or
/// only names Auth does not know) grants none, the account's platform roles are revoked, and the sign-in is refused 403 as it
/// always was for an account without one (FIX4 turned that into a ReadOnly sign-in). Item 4 — the rule is robust: a role is
/// recognised in any letter case; a convergence that stops half way leaves FEWER rights, never more; a role that cannot be
/// revoked stops the door (nothing is minted from a copy that cannot be corrected); the answer of a partner administrator is
/// converged like any other; the refresh converges in the platform tenant whatever tenant a token row carries (item 12).</para>
/// </summary>
public sealed class PlatformRoleConvergenceAtMintTests
{
    private static readonly Guid PlatformTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string RawRefreshToken = "raw-refresh";

    [Fact]
    public async Task A_sign_in_converges_the_roles_to_the_record_and_mints_the_token_from_them()
    {
        var world = new World(authRoles: ["SuperAdmin"]);

        var result = await world.LoginAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", ["SupportAdmin"]));

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Equal(["SupportAdmin"], world.Roles.Of(world.User.Id));
        Assert.Equal(["SupportAdmin"], world.Tokens.IssuedRoles);
    }

    [Fact]
    public async Task A_refresh_converges_the_roles_to_the_record_and_mints_the_token_from_them()
    {
        var world = new World(authRoles: ["SuperAdmin", "BillingAdmin"]);

        var result = await world.RefreshAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", ["ReadOnly"]));

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Equal(["ReadOnly"], world.Roles.Of(world.User.Id));
        Assert.Equal(["ReadOnly"], world.Tokens.IssuedRoles);
    }

    /// <summary>Only the four platform roles follow the record; anything else the account holds stays.</summary>
    [Fact]
    public async Task A_role_outside_the_platform_set_is_left_alone()
    {
        var world = new World(authRoles: ["SuperAdmin", "Admin"]);

        var result = await world.LoginAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", ["SupportAdmin"]));

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Equal(["Admin", "SupportAdmin"], world.Roles.Of(world.User.Id));
    }

    /// <summary>An older Platform sends no roles: nothing is converged and the sign-in still works as before.</summary>
    [Theory]
    [InlineData("login")]
    [InlineData("refresh")]
    public async Task Without_roles_in_the_answer_nothing_is_converged(string door)
    {
        var world = new World(authRoles: ["SuperAdmin"]);
        var record = PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin");

        var result = door == "login" ? await world.LoginAsync(record) : await world.RefreshAsync(record);

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Equal(["SuperAdmin"], world.Roles.Of(world.User.Id));
        Assert.Equal(0, world.Roles.Writes);
    }

    /// <summary>Roles that already match write nothing (no needless write on every sign-in).</summary>
    [Fact]
    public async Task Matching_roles_write_nothing()
    {
        var world = new World(authRoles: ["SupportAdmin"]);

        await world.LoginAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", ["SupportAdmin"]));

        Assert.Equal(0, world.Roles.Writes);
    }

    // ── FIX5 item 3 — no provisioning default at mint ───────────────────────────────────────────────────

    [Theory]
    [InlineData(new string[0], new[] { "SuperAdmin" })]   // the record names none; the account still holds SuperAdmin
    [InlineData(new string[0], new string[0])]            // the record names none; the account holds none
    [InlineData(new[] { "Auditor", "Admin" }, new string[0])] // only names Auth does not know
    public async Task A_record_that_names_no_platform_role_grants_none_and_the_sign_in_is_refused_403(string[] recordRoles, string[] authRoles)
    {
        var world = new World(authRoles);

        var result = await world.LoginAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", recordRoles));

        Assert.False(result.IsSuccessful);
        Assert.Equal(403, result.StatusCode);
        Assert.Empty(world.Roles.Of(world.User.Id)); // no ReadOnly, and the SuperAdmin the record does not name is gone
        Assert.Null(world.Tokens.IssuedRoles);       // nothing was minted
    }

    [Fact]
    public async Task A_refresh_with_a_record_that_names_no_platform_role_mints_no_platform_role_and_grants_none()
    {
        var world = new World(authRoles: ["SupportAdmin"]);

        var result = await world.RefreshAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", []));

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Empty(world.Roles.Of(world.User.Id));
        Assert.Empty(world.Tokens.IssuedRoles!);
    }

    // ── FIX5 item 4 — the convergence itself ────────────────────────────────────────────────────────────

    /// <summary>(a) A role stored in another letter case is a platform role, and is revoked like the canonical spelling.</summary>
    [Fact]
    public async Task A_role_stored_in_another_letter_case_is_recognised_and_revoked()
    {
        var world = new World(authRoles: ["superadmin"]);

        var result = await world.LoginAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", ["ReadOnly"]));

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Equal(["ReadOnly"], world.Roles.Of(world.User.Id));
        Assert.Equal(["ReadOnly"], world.Tokens.IssuedRoles);
    }

    /// <summary>A role held twice (duplicate assignment rows) neither looks missing nor extra when it is the record's own.</summary>
    [Fact]
    public async Task A_role_held_twice_that_the_record_names_is_left_as_it_is()
    {
        var world = new World(authRoles: ["SupportAdmin", "SupportAdmin"]);

        await world.LoginAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", ["SupportAdmin"]));

        Assert.Equal(0, world.Roles.Writes);
    }

    /// <summary>(b) Revoke first, then grant: when the grant fails the account holds FEWER rights than before, never more.</summary>
    [Fact]
    public async Task A_convergence_that_stops_after_the_revoke_leaves_the_account_with_fewer_rights_never_more()
    {
        var world = new World(authRoles: ["SuperAdmin"]);
        world.Roles.FailEveryGrant = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.LoginAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", ["ReadOnly"])));

        Assert.Empty(world.Roles.Of(world.User.Id)); // SuperAdmin was revoked first; ReadOnly was never granted
        Assert.Null(world.Tokens.IssuedRoles);
    }

    /// <summary>A role the account holds that cannot be revoked (no role document explains it) stops the door: nothing is minted
    /// from a copy that cannot be corrected.</summary>
    [Fact]
    public async Task A_role_that_cannot_be_revoked_stops_the_door_and_mints_nothing()
    {
        var world = new World(authRoles: ["SuperAdmin"]);
        world.Roles.HideRoleDocuments = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            world.LoginAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", ["ReadOnly"])));

        Assert.Null(world.Tokens.IssuedRoles);
        Assert.Equal(["SuperAdmin"], world.Roles.Of(world.User.Id)); // unchanged: nothing was granted either
    }

    /// <summary>A partner administrator's answer is converged like any other, and the token is minted as a partner's.</summary>
    [Theory]
    [InlineData("login")]
    [InlineData("refresh")]
    public async Task A_partner_admin_answer_converges_the_roles_and_mints_a_partner_token(string door)
    {
        var world = new World(authRoles: ["SuperAdmin"], actorType: "partner_admin");
        var record = PlatformAdministratorRecordAnswer.ConfirmedAs("partner_admin", ["SupportAdmin"]);

        var result = door == "login" ? await world.LoginAsync(record) : await world.RefreshAsync(record);

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.Equal(["SupportAdmin"], world.Roles.Of(world.User.Id));
        Assert.Equal("partner_admin", world.Tokens.IssuedActorType);
    }

    // ── FIX5 item 12 — the platform tenant by constant ──────────────────────────────────────────────────

    /// <summary>A refresh token row (and the token it belongs to) of ANOTHER tenant, for a platform actor: the convergence still
    /// writes in the platform tenant — it never takes the tenant from the row.</summary>
    [Fact]
    public async Task A_refresh_converges_in_the_platform_tenant_whatever_tenant_the_token_row_carries()
    {
        var otherTenant = Guid.NewGuid();
        var world = new World(authRoles: [], userTenant: otherTenant);

        var result = await world.RefreshAsync(PlatformAdministratorRecordAnswer.ConfirmedAs("platform_admin", ["ReadOnly"]));

        Assert.True(result.IsSuccessful, string.Join("; ", result.Errors));
        Assert.NotEmpty(world.Roles.WriteTenants);
        Assert.All(world.Roles.WriteTenants, tenant => Assert.Equal(PlatformTenant, tenant));
    }

    // ── world ───────────────────────────────────────────────────────────────────────────────────────────

    private sealed class World
    {
        private readonly Guid _userTenant;
        private readonly string _actorType;

        public World(IReadOnlyList<string> authRoles, string actorType = "platform_admin", Guid? userTenant = null)
        {
            _userTenant = userTenant ?? PlatformTenant;
            _actorType = actorType;
            User = new User("mint.probe@bl521.test", "hash:x", "Mint", "Probe", _userTenant);
            User.SetPlatformActorType(actorType);
            foreach (var role in authRoles)
            {
                Roles.Grant(User.Id, role);
            }
        }

        public User User { get; }
        public RoleStore Roles { get; } = new();
        public RecordingTokenService Tokens { get; private set; } = new(Guid.Empty, PlatformTenant, "platform_admin");

        public Task<Response<AuthResponse>> LoginAsync(PlatformAdministratorRecordAnswer record)
        {
            Tokens = new RecordingTokenService(User.Id, _userTenant, _actorType);
            return new PlatformLoginCommandHandler(
                    new InMemoryUserRepository([User]),
                    Roles,
                    Roles.Definitions,
                    new NoPermissions(),
                    new AcceptingHasher(),
                    new PrefixHasher(),
                    new RefreshRows(null),
                    Tokens,
                    new RecordStatus(record),
                    NullLogger<PlatformLoginCommandHandler>.Instance)
                .Handle(new PlatformLoginCommand(User.Email, "any", "127.0.0.1", "test-agent"), CancellationToken.None);
        }

        public Task<Response<AuthResponse>> RefreshAsync(PlatformAdministratorRecordAnswer record)
        {
            Tokens = new RecordingTokenService(User.Id, _userTenant, _actorType);
            var existing = new RefreshToken(User.Id, "rh:" + RawRefreshToken, DateTime.UtcNow.AddDays(3), "127.0.0.1", _userTenant, _actorType);
            return new RefreshTokenCommandHandler(
                    new InMemoryUserRepository([User]),
                    Roles,
                    Roles.Definitions,
                    new NoPermissions(),
                    new PassthroughPermissions(),
                    Tokens,
                    new NoTenantSettings(),
                    new PrefixHasher(),
                    new RefreshRows(existing),
                    new UnresolvedTenantContext(),
                    new RecordStatus(record),
                    NullLogger<RefreshTokenCommandHandler>.Instance)
                .Handle(new RefreshTokenCommand("old-access-token", RawRefreshToken, "127.0.0.1", "test-agent"), CancellationToken.None);
        }
    }

    /// <summary>Roles and their assignments, in memory; every assignment / revocation counts as a write. A grant is idempotent and
    /// a revoke removes every matching row, as the production repository does.</summary>
    private sealed class RoleStore : IUserRoleRepository
    {
        private readonly List<Role> _roles = [];
        private readonly List<(Guid UserId, Guid RoleId)> _assigned = [];

        public RoleStore() => Definitions = new RoleDefinitions(this);

        public RoleDefinitions Definitions { get; }
        public int Writes { get; private set; }

        /// <summary>The tenant every write was addressed to.</summary>
        public List<Guid> WriteTenants { get; } = [];

        /// <summary>Every grant fails (the store went away after the revoke).</summary>
        public bool FailEveryGrant { get; set; }

        /// <summary>The tenant's role list is empty: what an account holds cannot be matched to a role document.</summary>
        public bool HideRoleDocuments { get; set; }

        public void Grant(Guid userId, string name) => _assigned.Add((userId, Definitions.Ensure(name).Id));

        public List<string> Of(Guid userId) => _assigned.Where(a => a.UserId == userId)
            .Select(a => _roles.Single(r => r.Id == a.RoleId).Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IEnumerable<string>>(Of(userId));

        public Task AssignAsync(UserRole userRole, CancellationToken ct)
        {
            if (FailEveryGrant)
            {
                throw new InvalidOperationException("the role store went away");
            }

            Writes++;
            WriteTenants.Add(userRole.TenantId);
            if (!_assigned.Contains((userRole.UserId, userRole.RoleId)))
            {
                _assigned.Add((userRole.UserId, userRole.RoleId));
            }

            return Task.CompletedTask;
        }

        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct)
        {
            Writes++;
            WriteTenants.Add(tenantId);
            _assigned.RemoveAll(a => a.UserId == userId && a.RoleId == roleId);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult(_assigned.Contains((userId, roleId)));

        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(_assigned.Where(a => a.RoleId == roleId).Select(a => a.UserId).ToList());

        public sealed class RoleDefinitions(RoleStore store) : IRoleRepository
        {
            public Role Ensure(string name)
            {
                var role = store._roles.FirstOrDefault(r => r.Name == name);
                if (role is null)
                {
                    role = new Role(name, name, "test", PlatformTenant);
                    store._roles.Add(role);
                }

                return role;
            }

            public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.FromResult(store._roles.FirstOrDefault(r => r.Id == id));
            public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct) => Task.FromResult(store._roles.FirstOrDefault(r => r.Name == name));
            public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) =>
                Task.FromResult<IEnumerable<Role>>(store.HideRoleDocuments ? [] : store._roles.ToList());
            public Task<Role> CreateAsync(Role role, CancellationToken ct) { store._roles.Add(role); return Task.FromResult(role); }
            public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => Task.FromResult(Ensure(name));
            public Task<Role> UpdateAsync(Role role, CancellationToken ct) => Task.FromResult(role);
            public Task DeleteAsync(Guid id, Guid tenantId, string deletedBy, CancellationToken ct) => Task.CompletedTask;
        }
    }

    private sealed class RecordStatus(PlatformAdministratorRecordAnswer record) : IPlatformAdministratorStatusClient
    {
        public Task<bool> IsActiveAsync(string email, CancellationToken ct) => Task.FromResult(record.Verdict == PlatformAdministratorRecordVerdict.Confirmed);
        public Task MarkLoginAcceptedAsync(string email, CancellationToken ct) => Task.CompletedTask;
        public Task<PlatformAdministratorRecordAnswer> GetActiveActorTypeAsync(string email, CancellationToken ct) => Task.FromResult(record);
    }

    private sealed class RecordingTokenService(Guid userId, Guid tenantId, string actorType) : ITokenService
    {
        public List<string>? IssuedRoles { get; private set; }
        public string? IssuedActorType { get; private set; }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => new(new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("actor_type", actorType)
        ], "Bearer"));

        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange)
        {
            IssuedRoles = roles.OrderBy(r => r, StringComparer.Ordinal).ToList();
            IssuedActorType = actorType;
            return "new-access-token";
        }

        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions) => throw new NotSupportedException();
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw new NotSupportedException();
        public string GenerateRefreshToken() => "new-refresh";
    }

    private sealed class RefreshRows(RefreshToken? existing) : IRefreshTokenRepository
    {
        public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct) =>
            Task.FromResult(token == RawRefreshToken ? existing : null);
        public Task CreateAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(RefreshToken refreshToken, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(string token, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAllByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class AcceptingHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string hash) => true;
    }

    private sealed class PrefixHasher : IRefreshTokenHasher
    {
        public string Hash(string refreshToken) => "rh:" + refreshToken;
    }

    private sealed class PassthroughPermissions : ITenantEffectivePermissionResolver
    {
        public Task<IReadOnlyList<string>> ResolveAsync(Guid tenantId, IEnumerable<string>? rolePermissions, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<string>>(rolePermissions?.ToArray() ?? []);
    }

    private sealed class NoTenantSettings : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct) =>
            throw new NotSupportedException("A platform refresh reads no tenant login settings.");
    }

    private sealed class UnresolvedTenantContext : ITenantContext
    {
        public Guid TenantId => throw new InvalidOperationException("Tenant not resolved.");
        public bool IsResolved => false;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { }
        public void SetPlatformContext(Guid targetTenantId) { }
    }

    private sealed class NoPermissions : IRolePermissionRepository
    {
        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<string>>([]);
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<string>>([]);
        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct) => Task.CompletedTask;
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<RolePermission>>([]);
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => Task.CompletedTask;
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => Task.FromResult(0L);
    }
}
