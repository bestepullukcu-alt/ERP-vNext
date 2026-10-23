using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ROLES-SMALL-02 — the last-steward rule as it leaves the building: HTTP round trips through the REAL Api
/// (routing, [HasPermission], DI of the rule, the pipeline, JSON) on a test-owned mongod. A disposable tenant per test:
/// the caller holds the write keys in the token but NO role; the one role with auth.users.create belongs to a second,
/// active account — the tenant's last ACTIVE steward. Each path answers 409 with its own code and leaves the data as it
/// was; with a second active steward the same request goes through (non-vacuity).
/// </summary>
public sealed class LastStewardEndpointTests : IClassFixture<PlatformEdgeTestHost>
{
    private readonly PlatformEdgeTestHost _host;
    private readonly Guid _tenantId = Guid.NewGuid();

    public LastStewardEndpointTests(PlatformEdgeTestHost host) => _host = host;

    [Fact]
    public async Task Disabling_the_last_active_steward_is_409_USER_DEACTIVATE_LAST_STEWARD_on_the_kebab_and_on_the_form()
    {
        var seed = await SeedAsync(secondSteward: false);
        using var client = _host.Client(seed.Token, _tenantId);

        await AssertRefusedAsync(await client.PostAsync($"api/users/{seed.StewardId}/disable", null),
            UserErrorCodes.DeactivateLastSteward);
        await AssertRefusedAsync(
            await client.PutAsJsonAsync($"api/users/{seed.StewardId}", new { firstName = "Renamed", lastName = "Steward", isActive = false }),
            UserErrorCodes.DeactivateLastSteward);

        var steward = await StoredUserAsync(seed.StewardId);
        Assert.True(steward.IsActive);
        Assert.Equal("Last", steward.FirstName); // the refused form wrote no field at all
    }

    [Fact]
    public async Task Revoking_auth_users_create_from_the_last_stewards_role_is_409_ROLE_PERMISSION_REVOKE_LAST_STEWARD()
    {
        var seed = await SeedAsync(secondSteward: false);
        using var client = _host.Client(seed.Token, _tenantId);

        await AssertRefusedAsync(await client.DeleteAsync($"api/roles/{seed.RoleId}/permissions/{seed.CreatePermissionId}"),
            RoleErrorCodes.PermissionRevokeLastSteward);

        Assert.Equal(1, await GrantCountAsync(seed.RoleId, seed.CreatePermissionId));
    }

    [Fact]
    public async Task Revoking_the_role_and_deleting_the_role_are_409_with_their_own_codes()
    {
        var seed = await SeedAsync(secondSteward: false);
        using var client = _host.Client(seed.Token, _tenantId);

        await AssertRefusedAsync(await client.DeleteAsync($"api/users/{seed.StewardId}/roles/{seed.RoleId}"),
            RoleErrorCodes.UserRoleRevokeLastSteward);
        await AssertRefusedAsync(await client.DeleteAsync($"api/roles/{seed.RoleId}"), RoleErrorCodes.DeleteLastSteward);
    }

    [Fact]
    public async Task With_a_second_active_steward_the_same_requests_go_through()
    {
        var seed = await SeedAsync(secondSteward: true);
        using var client = _host.Client(seed.Token, _tenantId);

        var revoke = await client.DeleteAsync($"api/roles/{seed.RoleId}/permissions/{seed.CreatePermissionId}");
        Assert.True(revoke.StatusCode == HttpStatusCode.NoContent, await revoke.Content.ReadAsStringAsync());
        var disable = await client.PostAsync($"api/users/{seed.StewardId}/disable", null);
        Assert.True(disable.StatusCode == HttpStatusCode.NoContent, await disable.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// CT K1 — a platform account is not a holder for a TENANT actor. The tenant's only other active holder of
    /// auth.users.create is a platform account (the platform tenant is also a business tenant); read through the
    /// production <see cref="PlatformAccountScopedUserRepository"/> that hides it, the rule refuses; through the same
    /// repository that does not hide it (the platform door), the same change goes through — so the refusal is the
    /// hiding's, not the world's.
    /// </summary>
    [Fact]
    public async Task For_a_tenant_actor_a_platform_account_does_not_keep_the_tenant_alive()
    {
        var ct = CancellationToken.None;
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var inner = sp.GetRequiredService<UserRepository>();
        var roles = sp.GetRequiredService<IRoleRepository>();
        var userRoles = sp.GetRequiredService<IUserRoleRepository>();
        var rolePermissions = sp.GetRequiredService<IRolePermissionRepository>();
        var create = await _host.Database.GetCollection<Permission>("permissions").Find(p => p.Key == "auth.users.create").SingleAsync();

        async Task StewardOfOwnRoleAsync(User member, string roleName)
        {
            var role = await roles.CreateAsync(new Role(roleName, roleName, "last-steward K1 fixture", _tenantId), ct);
            await _host.Database.GetCollection<RolePermission>("rolePermissions")
                .InsertOneAsync(RolePermission.ManualGrant(role.Id, create.Id, _tenantId, "last-steward-k1"));
            await userRoles.AssignAsync(new UserRole(member.Id, role.Id, _tenantId, "last-steward-k1"), ct);
        }

        var steward = await inner.CreateAsync(new User($"k1.steward.{Guid.NewGuid():N}@steward.test", "hash:x", "Tenant", "Steward", _tenantId), ct);
        var platformUser = new User($"k1.platform.{Guid.NewGuid():N}@steward.test", "hash:x", "Platform", "Admin", _tenantId);
        platformUser.SetPlatformActorType("platform_admin");
        platformUser = await inner.CreateAsync(platformUser, ct);
        Assert.True(platformUser.IsActive && platformUser.IsPlatformAccount);
        await StewardOfOwnRoleAsync(steward, "TenantStewards");
        await StewardOfOwnRoleAsync(platformUser, "PlatformStewards");

        RecoveryCapabilityGuard Rule(IPlatformAccountVisibility visibility) =>
            new(new PlatformAccountScopedUserRepository(inner, visibility), userRoles, roles, rolePermissions);
        var change = new RecoveryCapabilityChange.DeactivateUser(steward.Id);

        Assert.True(await Rule(new HideAccountsFromCaller()).WouldLeaveNoActiveHolderAsync(_tenantId, change, ct));
        Assert.False(await Rule(new ShowEveryAccount()).WouldLeaveNoActiveHolderAsync(_tenantId, change, ct));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    private static async Task AssertRefusedAsync(HttpResponseMessage response, string code)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, $"expected 409, got {(int)response.StatusCode}: {body}");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.False(root.GetProperty("isSuccessful").GetBoolean());
        Assert.True(root.TryGetProperty("errorCodes", out var codes) && codes.ValueKind == JsonValueKind.Array && codes.GetArrayLength() > 0,
            $"the refusal left without a code: {body}");
        Assert.Equal(code, codes[0].GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("errors")[0].GetString()));
    }

    private async Task<User> StoredUserAsync(Guid id)
    {
        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        return (await scope.ServiceProvider.GetRequiredService<IUserRepository>().GetByIdAndTenantAsync(id, _tenantId, CancellationToken.None))!;
    }

    private Task<long> GrantCountAsync(Guid roleId, Guid permissionId) =>
        _host.Database.GetCollection<RolePermission>("rolePermissions")
            .CountDocumentsAsync(g => g.RoleId == roleId && g.PermissionId == permissionId && g.TenantId == _tenantId);

    private sealed record Seed(Guid StewardId, Guid RoleId, Guid CreatePermissionId, string Token);

    /// <summary>
    /// The caller: active, no role, every write key in the token. The steward: active, the only member of the one custom
    /// role carrying auth.users.create (a MANUAL grant). With <paramref name="secondSteward"/>, a second active account
    /// holds the capability through ANOTHER role.
    /// </summary>
    private async Task<Seed> SeedAsync(bool secondSteward)
    {
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(_tenantId);
        var users = sp.GetRequiredService<IUserRepository>();
        var roles = sp.GetRequiredService<IRoleRepository>();
        var userRoles = sp.GetRequiredService<IUserRoleRepository>();
        var tokens = sp.GetRequiredService<ITokenService>();
        var create = await _host.Database.GetCollection<Permission>("permissions").Find(p => p.Key == "auth.users.create").SingleAsync();

        async Task<User> PersonAsync(string first)
        {
            var user = new User($"{first.ToLowerInvariant()}.{Guid.NewGuid():N}@steward.test", "hash:x", first, "Steward", _tenantId);
            user.ConfirmEmail();
            return await users.CreateAsync(user, CancellationToken.None);
        }

        async Task<Role> StewardRoleAsync(string name, User member)
        {
            var role = await roles.CreateAsync(new Role(name, name, "last-steward fixture", _tenantId), CancellationToken.None);
            await _host.Database.GetCollection<RolePermission>("rolePermissions")
                .InsertOneAsync(RolePermission.ManualGrant(role.Id, create.Id, _tenantId, "last-steward-fixture"));
            await userRoles.AssignAsync(new UserRole(member.Id, role.Id, _tenantId, "last-steward-fixture"), CancellationToken.None);
            return role;
        }

        var actor = await PersonAsync("Caller");
        var steward = await PersonAsync("Last");
        var stewards = await StewardRoleAsync("Stewards", steward);
        if (secondSteward)
        {
            await StewardRoleAsync("Owners", await PersonAsync("Second"));
        }

        var token = tokens.GenerateAccessToken(actor, [],
            ["auth.users.read", "auth.users.update", "auth.users.assign-role", "auth.roles.read", "auth.roles.delete",
             "auth.roles.assign-permission"], expiresInMinutes: 60);
        return new Seed(steward.Id, stewards.Id, create.Id, token);
    }
}
