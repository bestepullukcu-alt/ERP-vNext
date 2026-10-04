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

    // ── world ───────────────────────────────────────────────────────────────────────────────────────────

    private sealed class World
    {
        public World(IReadOnlyList<string> authRoles)
        {
            User = new User("mint.probe@bl521.test", "hash:x", "Mint", "Probe", PlatformTenant);
            User.SetPlatformActorType("platform_admin");
            foreach (var role in authRoles)
            {
                Roles.Grant(User.Id, role);
            }
        }

        public User User { get; }
        public RoleStore Roles { get; } = new();
        public RecordingTokenService Tokens { get; private set; } = new(Guid.Empty);

        public Task<Response<AuthResponse>> LoginAsync(PlatformAdministratorRecordAnswer record)
        {
            Tokens = new RecordingTokenService(User.Id);
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
            Tokens = new RecordingTokenService(User.Id);
            var existing = new RefreshToken(User.Id, "rh:" + RawRefreshToken, DateTime.UtcNow.AddDays(3), "127.0.0.1", PlatformTenant, "platform_admin");
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

    /// <summary>Roles and their assignments, in memory; every assignment / revocation counts as a write.</summary>
    private sealed class RoleStore : IUserRoleRepository
    {
        private readonly List<Role> _roles = [];
        private readonly List<(Guid UserId, Guid RoleId)> _assigned = [];

        public RoleStore() => Definitions = new RoleDefinitions(this);

        public RoleDefinitions Definitions { get; }
        public int Writes { get; private set; }

        public void Grant(Guid userId, string name) => _assigned.Add((userId, Definitions.Ensure(name).Id));

        public List<string> Of(Guid userId) => _assigned.Where(a => a.UserId == userId)
            .Select(a => _roles.Single(r => r.Id == a.RoleId).Name).OrderBy(n => n, StringComparer.Ordinal).ToList();

        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IEnumerable<string>>(Of(userId));

        public Task AssignAsync(UserRole userRole, CancellationToken ct)
        {
            Writes++;
            _assigned.Add((userRole.UserId, userRole.RoleId));
            return Task.CompletedTask;
        }

        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct)
        {
            Writes++;
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
            public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IEnumerable<Role>>(store._roles.ToList());
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

    private sealed class RecordingTokenService(Guid userId) : ITokenService
    {
        public List<string>? IssuedRoles { get; private set; }

        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => new(new ClaimsIdentity(
        [
            new Claim("sub", userId.ToString()),
            new Claim("tenant_id", PlatformTenant.ToString()),
            new Claim("actor_type", "platform_admin")
        ], "Bearer"));

        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange)
        {
            IssuedRoles = roles.OrderBy(r => r, StringComparer.Ordinal).ToList();
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
