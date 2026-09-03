using System.Security.Cryptography;
using System.Text;
using Diten.AuthService.Api.Configuration;
using Diten.AuthService.Api.Services.ServiceIdentityTokens;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Services;
using Diten.AuthService.Infrastructure.Settings;
using Diten.BuildingBlocks.Security.Secrets;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Diten.AuthService.Application.Tests.ServiceIdentityTokens;

public sealed class ServiceClientOperationalActorAuthorizationTests
{
    private static readonly Guid PlatformTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string Secret = "fu02e-operator-jwt-secret-that-is-long-enough-for-hs256-tests";
    private const string Marker = "opaque-one-shot-marker";

    [Fact]
    public async Task Fresh_platform_admin_with_live_permission_and_marker_is_authorized()
    {
        var fixture = Build();

        var actor = await fixture.Authorizer.AuthorizeAsync(fixture.Token, Marker, default);

        Assert.Equal(fixture.User.Id, actor.UserId);
        Assert.Equal(PlatformTenant, actor.TenantId);
        Assert.Equal("platform_admin", actor.ActorType);
    }

    [Fact]
    public async Task Real_shaped_operator_jwt_larger_than_16KiB_and_within_32KiB_is_authorized()
    {
        var fixture = Build(largeToken: true);

        Assert.InRange(fixture.Token.Length, (16 * 1024) + 1, 32 * 1024);
        var actor = await fixture.Authorizer.AuthorizeAsync(fixture.Token, Marker, default);

        Assert.Equal(fixture.User.Id, actor.UserId);
    }

    [Fact]
    public async Task Jwt_claim_without_live_permission_fails_closed()
    {
        var fixture = Build(hasPersistedPermission: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Authorizer.AuthorizeAsync(fixture.Token, Marker, default));
    }

    [Fact]
    public async Task Wrong_marker_or_non_system_tenant_fails_closed()
    {
        var fixture = Build();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Authorizer.AuthorizeAsync(fixture.Token, "wrong-marker", default));

        var tenantFixture = Build(tokenTenant: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            tenantFixture.Authorizer.AuthorizeAsync(tenantFixture.Token, Marker, default));
    }

    [Fact]
    public async Task Inactive_or_password_change_required_user_fails_closed()
    {
        var inactive = Build(active: false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            inactive.Authorizer.AuthorizeAsync(inactive.Token, Marker, default));

        var passwordChange = Build(mustChangePassword: true);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            passwordChange.Authorizer.AuthorizeAsync(passwordChange.Token, Marker, default));
    }

    private static Fixture Build(
        bool hasPersistedPermission = true,
        bool active = true,
        bool mustChangePassword = false,
        Guid? tokenTenant = null,
        bool largeToken = false)
    {
        var user = new User("operator@example.test", "hash", "Platform", "Operator", PlatformTenant);
        user.SetPlatformActorType("platform_admin");
        if (!active) user.Deactivate();
        if (mustChangePassword) user.RequirePasswordChange(DateTime.UtcNow.AddMinutes(10));
        var role = new Role("SuperAdmin", "Super Admin", null, PlatformTenant);
        role.MarkAsSystem();
        var resolver = new FakeRotationResolver(Secret);
        var jwtSettings = new JwtSettings
        {
            Secret = Secret,
            Issuer = "Diten.Auth.Tests",
            Audience = "Diten.Web.Tests",
            AccessTokenExpirationMinutes = 15
        };
        var tokenService = new TokenService(Options.Create(jwtSettings), resolver);
        var permissions = largeToken
            ? Enumerable.Range(0, 400)
                .Select(index => $"test.large-permission-{index:D4}-{new string('x', 24)}")
                .Append(ServiceClientOperationalActorAuthorizer.RequiredPermission)
                .ToArray()
            : [ServiceClientOperationalActorAuthorizer.RequiredPermission];
        var token = tokenService.GeneratePlatformAccessToken(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            tokenTenant ?? PlatformTenant,
            "platform_admin",
            [role.Name],
            permissions);

        var authorizer = new ServiceClientOperationalActorAuthorizer(
            Options.Create(jwtSettings),
            Options.Create(new ServiceClientOperationalProvisioningOptions
            {
                OperationalMarkerSha256 = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(Marker))).ToLowerInvariant(),
                MaximumOperatorTokenAgeSeconds = 300
            }),
            resolver,
            new FakeUserRepository(user),
            new FakeUserRoleRepository(role.Name),
            new FakeRoleRepository(role),
            new FakeRolePermissionRepository(hasPersistedPermission),
            new FakePermissionRepository(),
            TimeProvider.System);
        return new Fixture(authorizer, token, user);
    }

    private sealed record Fixture(ServiceClientOperationalActorAuthorizer Authorizer, string Token, User User);

    private sealed class FakeRotationResolver(string secret) : ISecretRotationResolver
    {
        private readonly SecurityKey _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        public SecurityKey GetCurrentSigningKey() => _key;
        public IReadOnlyList<SecurityKey> GetValidationKeys() => [_key];
    }

    private sealed class FakeUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
            => Task.FromResult<User?>(id == user.Id && tenantId == user.TenantId ? user : null);
        public Task<User?> GetByEmailAndTenantAsync(string email, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByUserNameAndTenantAsync(string normalizedUserName, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByPasswordResetTokenHashAsync(string tokenHash, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<User>> GetAllByTenantAsync(Guid tenantId, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> GetCountByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<User> CreateAsync(User value, CancellationToken ct) => throw new NotSupportedException();
        public Task<User> UpdateAsync(User value, CancellationToken ct) => throw new NotSupportedException();
        public Task<User> UpdateForTenantAsync(User value, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task SoftDeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeUserRoleRepository(string roleName) : IUserRoleRepository
    {
        public Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
            => Task.FromResult<IEnumerable<string>>([roleName]);
        public Task AssignAsync(UserRole userRole, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRoleRepository(Role role) : IRoleRepository
    {
        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct)
            => Task.FromResult<Role?>(name == role.Name && tenantId == role.TenantId ? role : null);
        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role value, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role value, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRolePermissionRepository(bool hasPermission) : IRolePermissionRepository
    {
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct)
            => Task.FromResult<IEnumerable<string>>(hasPermission
                ? [ServiceClientOperationalActorAuthorizer.RequiredPermission]
                : []);
        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakePermissionRepository : IPermissionRepository
    {
        private readonly Permission _permission = new(
            "auth", "service-clients", "provision", "Provision Service Clients", null,
            moduleOverride: "platform", scope: PermissionScope.PlatformAdmin);

        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct)
            => Task.FromResult<Permission?>(key == _permission.Key ? _permission : null);
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct) => throw new NotSupportedException();
    }
}
