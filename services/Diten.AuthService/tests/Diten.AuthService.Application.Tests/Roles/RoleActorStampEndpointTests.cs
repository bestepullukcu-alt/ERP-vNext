using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// BL-412 / WP-INFRA-AUTH-GRANT-ACTOR-01 — HTTP round trips through the real Api (WebApplicationFactory, JWT auth,
/// tenant resolution, [HasPermission], MediatR pipeline, Mongo repositories) against the shared
/// <see cref="AccountKindAcceptance.AuthTestHost"/>. One additive disposable actor is seeded into the host's
/// ALREADY-seeded tenant: an ordinary tenant user whose role holds auth.roles.create/read/update/assign-permission —
/// the tenant administrator of roles. Every assertion reads what production wrote back from the test-owned mongod,
/// and checks that the person stamped on the document is the same id the RBAC audit row carries.
/// Nothing here writes to the DefaultTenant; the seeder's own rows are only READ (E5).
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class RoleActorStampEndpointTests : IClassFixture<AccountKindAcceptance.AuthTestHost>, IAsyncLifetime
{
    private readonly AccountKindAcceptance.AuthTestHost _host;
    private RolesAdmin _admin = null!;

    public RoleActorStampEndpointTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    public async Task InitializeAsync() => _admin = await SeedRolesAdminAsync(_host);

    public Task DisposeAsync() => Task.CompletedTask;

    // E1 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Creating_a_role_over_http_records_the_acting_user_as_CreatedBy()
    {
        using var client = _host.Client(_admin.Token, _admin.TenantId);

        var role = await CreateRoleAsync(client, "create");

        Assert.Equal(_admin.UserId.ToString(), role.CreatedBy);
        Assert.Equal(_admin.UserId, await AuditActorAsync("role_created", role.Id));
    }

    // E2 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Granting_a_permission_over_http_records_the_acting_user_as_AssignedBy_same_as_the_audit_actor()
    {
        using var client = _host.Client(_admin.Token, _admin.TenantId);
        var role = await CreateRoleAsync(client, "grant");
        var permissionId = await PermissionIdAsync("auth.users.read");

        var response = await client.PostAsJsonAsync($"api/roles/{role.Id}/permissions", new { permissionId });

        Assert.True(response.StatusCode == HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var grant = Assert.Single(await ReadGrantsAsync(role.Id), g => g.PermissionId == permissionId);
        Assert.Equal(_admin.UserId.ToString(), grant.AssignedBy);
        Assert.Equal(_admin.UserId.ToString(), grant.CreatedBy);
        Assert.NotEqual("System", grant.AssignedBy, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(GrantSource.Manual, grant.GrantSource);
        Assert.Null(grant.SourceModuleCode);
        Assert.Equal(_admin.UserId, await AuditActorAsync("role_permission_granted", role.Id));
    }

    // E3 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task A_grant_made_by_a_person_is_still_a_manual_grant_the_person_can_revoke()
    {
        using var client = _host.Client(_admin.Token, _admin.TenantId);
        var role = await CreateRoleAsync(client, "revoke");
        var permissionId = await PermissionIdAsync("auth.users.read");
        var granted = await client.PostAsJsonAsync($"api/roles/{role.Id}/permissions", new { permissionId });
        Assert.True(granted.StatusCode == HttpStatusCode.NoContent, await granted.Content.ReadAsStringAsync());

        var revoked = await client.DeleteAsync($"api/roles/{role.Id}/permissions/{permissionId}");

        Assert.True(revoked.StatusCode == HttpStatusCode.NoContent, await revoked.Content.ReadAsStringAsync());
        Assert.DoesNotContain(await ReadGrantsAsync(role.Id), g => g.PermissionId == permissionId);
    }

    // E4 ────────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Updating_a_role_over_http_records_the_acting_user_as_UpdatedBy()
    {
        using var client = _host.Client(_admin.Token, _admin.TenantId);
        var role = await CreateRoleAsync(client, "update");

        var response = await client.PutAsJsonAsync($"api/roles/{role.Id}", new { displayName = "BL-412 updated", description = "edited" });

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var updated = await ReadRoleByIdAsync(role.Id);
        Assert.Equal("BL-412 updated", updated.DisplayName);
        Assert.Equal(_admin.UserId.ToString(), updated.UpdatedBy);
        Assert.Equal(_admin.UserId.ToString(), updated.CreatedBy);
        Assert.Equal(_admin.UserId, await AuditActorAsync("role_updated", role.Id));
    }

    // E5 ────────────────────────────────────────────────────────────────────────────────────────────────
    // The production DataSeeder (including the TenantAdminSelfServiceReconciler backfill) ran at host start. Every
    // grant row it wrote — i.e. every row outside the two disposable acceptance tenants — still names the system.
    [Fact]
    public async Task Rows_the_production_seeder_wrote_at_host_start_still_carry_the_system_actor()
    {
        using var scope = _host.Factory.Services.CreateScope();
        var grants = scope.ServiceProvider.GetRequiredService<IMongoDatabase>().GetCollection<RolePermission>("rolePermissions");
        var disposableTenants = new[] { _host.Seeded.TenantId, _host.Seeded.ForeignTenantId };

        var seeded = await grants.Find(Builders<RolePermission>.Filter.Nin(rp => rp.TenantId, disposableTenants)).ToListAsync();

        Assert.NotEmpty(seeded);
        Assert.Contains(seeded, rp => rp.GrantSource == GrantSource.System);
        Assert.All(seeded, rp => Assert.Equal("system", rp.AssignedBy));
        Assert.All(seeded, rp => Assert.Equal("system", rp.CreatedBy));
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────

    private static string Stamp() => Guid.NewGuid().ToString("N")[..8];

    private async Task<Role> CreateRoleAsync(HttpClient client, string slug)
    {
        var name = $"bl412-{slug}-{Stamp()}";
        var response = await client.PostAsJsonAsync("api/roles", new { name, displayName = $"BL-412 {slug}", description = "acceptance" });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        using var scope = TenantScope();
        return await scope.ServiceProvider.GetRequiredService<IRoleRepository>().GetByNameAndTenantAsync(name, _admin.TenantId, CancellationToken.None)
            ?? throw new InvalidOperationException($"POST api/roles answered 201 but role '{name}' was not persisted.");
    }

    private async Task<Role> ReadRoleByIdAsync(Guid roleId)
    {
        using var scope = TenantScope();
        return await scope.ServiceProvider.GetRequiredService<IRoleRepository>().GetByIdAndTenantAsync(roleId, _admin.TenantId, CancellationToken.None)
            ?? throw new InvalidOperationException($"role {roleId} vanished");
    }

    private async Task<IReadOnlyList<RolePermission>> ReadGrantsAsync(Guid roleId)
    {
        using var scope = TenantScope();
        return await scope.ServiceProvider.GetRequiredService<IRolePermissionRepository>().GetByRoleAsync(roleId, _admin.TenantId, CancellationToken.None);
    }

    private async Task<Guid> PermissionIdAsync(string key)
    {
        using var scope = TenantScope();
        var permission = await scope.ServiceProvider.GetRequiredService<IPermissionRepository>().GetByKeyAsync(key, CancellationToken.None)
            ?? throw new InvalidOperationException($"{key} is not in the seeded catalog.");
        return permission.Id;
    }

    private async Task<Guid> AuditActorAsync(string eventName, Guid roleId)
    {
        using var scope = TenantScope();
        var audit = scope.ServiceProvider.GetRequiredService<IMongoDatabase>().GetCollection<AuthAuditLog>("authAuditLogs");
        var rows = await audit.Find(a => a.EventName == eventName && a.TenantId == _admin.TenantId).ToListAsync();
        var row = Assert.Single(rows, a => a.Metadata.Contains(roleId.ToString(), StringComparison.OrdinalIgnoreCase));
        return row.UserId;
    }

    private IServiceScope TenantScope()
    {
        var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(_admin.TenantId);
        return scope;
    }

    // ── additive fixture: one tenant administrator of roles ──────────────────────────────────────────────

    private sealed record RolesAdmin(Guid TenantId, Guid UserId, string Token);

    private static readonly string[] RolesAdminPermissionKeys =
        ["auth.roles.create", "auth.roles.read", "auth.roles.update", "auth.roles.assign-permission"];

    // xUnit builds a new test-class instance per test, but the host (IClassFixture) is shared — seed exactly once per host.
    private static readonly ConditionalWeakTable<AccountKindAcceptance.AuthTestHost, Task<RolesAdmin>> RolesAdminCache = new();

    private static Task<RolesAdmin> SeedRolesAdminAsync(AccountKindAcceptance.AuthTestHost host) =>
        RolesAdminCache.GetValue(host, static h => SeedRolesAdminCoreAsync(h));

    private static async Task<RolesAdmin> SeedRolesAdminCoreAsync(AccountKindAcceptance.AuthTestHost host)
    {
        var tenantId = host.Seeded.TenantId;
        var stamp = Stamp();

        using var scope = host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);

        var users = sp.GetRequiredService<IUserRepository>();
        var roles = sp.GetRequiredService<IRoleRepository>();
        var permissions = sp.GetRequiredService<IPermissionRepository>();
        var rolePermissions = sp.GetRequiredService<IRolePermissionRepository>();
        var userRoles = sp.GetRequiredService<IUserRoleRepository>();
        var hasher = sp.GetRequiredService<IPasswordHasher>();
        var tokens = sp.GetRequiredService<ITokenService>();

        var role = await roles.CreateAsync(
            new Role($"roles-admin-{stamp}", "Roles Admin (disposable)", "BL-412 acceptance fixture", tenantId), CancellationToken.None);

        foreach (var key in RolesAdminPermissionKeys)
        {
            var permission = await permissions.GetByKeyAsync(key, CancellationToken.None)
                ?? throw new InvalidOperationException($"{key} is not in the seeded catalog.");
            await rolePermissions.AssignAsync(
                RolePermission.ManualGrant(role.Id, permission.Id, tenantId, AccountKindAcceptance.SeedActor), CancellationToken.None);
        }

        var user = new User($"roles-admin.{stamp}@acceptance.invalid", hasher.Hash(AccountKindAcceptance.DisposablePassword), "Roles", "Admin", tenantId);
        user.ConfirmEmail();
        var created = await users.CreateAsync(user, CancellationToken.None);
        await userRoles.AssignAsync(new UserRole(created.Id, role.Id, tenantId, AccountKindAcceptance.SeedActor), CancellationToken.None);

        var permissionKeys = (await rolePermissions.GetPermissionsByRoleAsync(role.Id, tenantId, CancellationToken.None)).ToArray();
        var token = tokens.GenerateAccessToken(created, new[] { role.Name }, permissionKeys, expiresInMinutes: 60);

        return new RolesAdmin(tenantId, created.Id, token);
    }
}
