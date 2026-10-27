using System.Security.Claims;
using Diten.AuthService.Api.Controllers;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Events;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.Seed;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// WP-0018-ROLE1 (FU-0022-SEED-02, owner decision 2026-10-10): "the default role template is applied to a tenant only
/// when it is first set up; afterwards only an administrator decides a role's content — the system adds no permission by
/// itself". Measured on a REAL Mongo (the acceptance host's own ephemeral mongod; DB-010: the host's shared database — its indexes
/// and its seeded catalog are the production ones — isolated by a fresh tenant id per test; the seed-tenant tests use their own
/// fixed databases) through the
/// PRODUCTION entry points: <see cref="InternalEventsController"/> (<c>tenant-activated</c>, <c>tenant-admin-invited</c>) with
/// the production <see cref="RoleProvisioningService"/> and repositories, and <see cref="DataSeeder.SeedAsync"/> for the seed
/// tenant. Only the outbound edges (internal key check, Platform entitlement / login-settings HTTP) are replaced.
/// </summary>
[Trait("Category", "Mongo")]
public sealed class DefaultRoleTemplateOnceTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private const string AuditEvent = "default_role_provisioned";
    private static readonly Guid SeedTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly AccountKindAcceptance.AuthTestHost _host;

    public DefaultRoleTemplateOnceTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    // ── tenant events ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_key_an_admin_removed_from_Viewer_is_not_put_back_by_tenant_admin_invited()
    {
        var w = await WorldAsync();
        var tenant = Guid.NewGuid();
        await w.ActivatedAsync(tenant);
        var removed = await w.RemoveOneAsync(tenant, "Viewer");

        await w.InvitedAsync(tenant, $"yonetici-{tenant:N}@ornek.test");

        Assert.DoesNotContain(removed, await w.KeysAsync(tenant, "Viewer"));
    }

    [Fact]
    public async Task A_key_an_admin_removed_from_Viewer_is_not_put_back_by_a_second_tenant_activated()
    {
        var w = await WorldAsync();
        var tenant = Guid.NewGuid();
        await w.ActivatedAsync(tenant);
        var removed = await w.RemoveOneAsync(tenant, "Viewer");

        await w.ActivatedAsync(tenant); // a re-activation is a new event (new EventId)

        Assert.DoesNotContain(removed, await w.KeysAsync(tenant, "Viewer"));
    }

    [Fact]
    public async Task A_key_added_to_the_template_later_does_not_reach_an_existing_tenant_through_either_event()
    {
        var w = await WorldAsync();
        var tenant = Guid.NewGuid();
        await w.ActivatedAsync(tenant);
        var before = new Dictionary<string, List<string>>
        {
            ["Admin"] = await w.KeysAsync(tenant, "Admin"),
            ["Viewer"] = await w.KeysAsync(tenant, "Viewer")
        };

        // a key that enters the catalog after the tenant exists (unique per run; removed again below — the catalog is shared)
        var later = new Permission("mdm", "role1-later-" + Guid.NewGuid().ToString("N")[..8], "read", "Read later resource", null, moduleOverride: "supplier");
        var permissions = w.Database.GetCollection<Permission>("permissions");
        await permissions.InsertOneAsync(later);
        try
        {
            Assert.Contains(later.Key, DefaultRolePermissionTemplate.SelectFor("Viewer", [later]).Select(p => p.Key)); // the template would grant it

            await w.InvitedAsync(tenant, $"ikinci-{tenant:N}@ornek.test");
            await w.ActivatedAsync(tenant);

            Assert.Equal(before["Admin"], await w.KeysAsync(tenant, "Admin"));
            Assert.Equal(before["Viewer"], await w.KeysAsync(tenant, "Viewer"));
        }
        finally
        {
            await permissions.DeleteOneAsync(p => p.Id == later.Id);
        }
    }

    [Fact]
    public async Task A_new_tenant_gets_every_template_role_with_every_template_key_and_one_audit_row_per_role()
    {
        var w = await WorldAsync();
        var tenant = Guid.NewGuid();

        await w.ActivatedAsync(tenant);

        var catalog = await w.CatalogAsync();
        foreach (var role in new[] { "Admin", "Viewer" })
        {
            var expected = DefaultRolePermissionTemplate.SelectFor(role, catalog).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
            Assert.NotEmpty(expected);
            Assert.Equal(expected, await w.KeysAsync(tenant, role));
            var audit = Assert.Single(await w.AuditAsync(tenant), a => a.Metadata.Contains($"\"roleName\":\"{role}\""));
            Assert.Contains($"\"permissionCount\":{expected.Count}", audit.Metadata);
            Assert.Contains("\"trigger\":\"tenant-activated\"", audit.Metadata);
        }
    }

    [Fact]
    public async Task A_default_role_the_tenant_lacks_is_created_from_the_template_and_the_other_role_is_left_alone()
    {
        var w = await WorldAsync();
        var tenant = Guid.NewGuid();
        await w.ActivatedAsync(tenant);
        var adminRemoved = await w.RemoveOneAsync(tenant, "Admin");
        await w.DeleteRoleAsync(tenant, "Viewer"); // an old tenant without a Viewer role
        var auditBefore = (await w.AuditAsync(tenant)).Count;

        await w.InvitedAsync(tenant, $"yonetici-{tenant:N}@ornek.test");

        var catalog = await w.CatalogAsync();
        Assert.Equal(DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal),
            await w.KeysAsync(tenant, "Viewer"));
        Assert.DoesNotContain(adminRemoved, await w.KeysAsync(tenant, "Admin"));
        var added = (await w.AuditAsync(tenant)).Skip(auditBefore).ToList();
        var viewerRow = Assert.Single(added);
        Assert.Contains("\"roleName\":\"Viewer\"", viewerRow.Metadata);
        Assert.Contains("\"trigger\":\"tenant-admin-invited\"", viewerRow.Metadata);
    }

    [Fact]
    public async Task Two_events_at_the_same_time_apply_the_template_once()
    {
        var w = await WorldAsync();
        var tenant = Guid.NewGuid();

        await Task.WhenAll(w.ActivatedAsync(tenant), w.InvitedAsync(tenant, $"yaris-{tenant:N}@ornek.test"), w.ActivatedAsync(tenant));

        var catalog = await w.CatalogAsync();
        foreach (var role in new[] { "Admin", "Viewer" })
        {
            Assert.Single(await w.Database.GetCollection<Role>("roles").Find(r => r.TenantId == tenant && r.Name == role && !r.IsDeleted).ToListAsync());
            Assert.Equal(DefaultRolePermissionTemplate.SelectFor(role, catalog).Count, (await w.KeysAsync(tenant, role)).Count);
            Assert.Single(await w.AuditAsync(tenant), a => a.Metadata.Contains($"\"roleName\":\"{role}\""));
        }
    }

    [Fact]
    public async Task The_invited_administrator_is_still_assigned_the_Admin_role_on_a_new_and_on_an_existing_tenant()
    {
        var w = await WorldAsync();
        var tenant = Guid.NewGuid();

        var emails = new[] { $"ilk-{tenant:N}@ornek.test", $"ikinci-{tenant:N}@ornek.test" };
        var first = await w.InvitedAsync(tenant, emails[0]);          // new tenant: roles come with the invitation
        Assert.IsType<OkObjectResult>(first);
        await w.RemoveOneAsync(tenant, "Admin");
        var second = await w.InvitedAsync(tenant, emails[1]);         // existing tenant
        Assert.IsType<OkObjectResult>(second);

        var admin = await w.Database.GetCollection<Role>("roles").Find(r => r.TenantId == tenant && r.Name == "Admin" && !r.IsDeleted).SingleAsync();
        foreach (var email in emails)
        {
            var user = await w.Database.GetCollection<User>("users").Find(u => u.TenantId == tenant && u.Email == email).SingleAsync();
            Assert.Single(await w.Database.GetCollection<UserRole>("userRoles").Find(ur => ur.UserId == user.Id && ur.RoleId == admin.Id && ur.TenantId == tenant).ToListAsync());
        }
    }

    // ── seed tenant (AuthService start-up) ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_key_removed_from_a_seed_tenant_role_is_not_put_back_by_the_next_start_up()
    {
        var database = await FreshDatabaseAsync("diten_auth_itest_role1_seed");
        await DataSeeder.SeedAsync(database);                                  // first start-up: the template is applied
        var w = new World(database);
        var catalog = await w.CatalogAsync();
        Assert.NotEmpty(DefaultRolePermissionTemplate.SelectFor("Viewer", catalog));

        var removed = await w.RemoveOneSystemAsync(SeedTenantId, "Viewer");
        var superRemoved = await w.RemoveOneSystemAsync(SeedTenantId, "SuperAdmin");

        await DataSeeder.SeedAsync(database);                                  // the next start-up

        Assert.DoesNotContain(removed, await w.KeysAsync(SeedTenantId, "Viewer"));
        Assert.DoesNotContain(superRemoved, await w.KeysAsync(SeedTenantId, "SuperAdmin"));
    }

    [Fact]
    public async Task The_first_seed_start_up_applies_the_template_and_audits_each_seed_role_once()
    {
        var database = await FreshDatabaseAsync("diten_auth_itest_role1_seed_first");
        await DataSeeder.SeedAsync(database);
        await DataSeeder.SeedAsync(database);
        var w = new World(database);
        var catalog = await w.CatalogAsync();

        foreach (var role in new[] { "SuperAdmin", "Admin", "Viewer" })
        {
            var template = DefaultRolePermissionTemplate.SelectFor(role, catalog).Select(p => p.Key).ToList();
            var held = await w.KeysAsync(SeedTenantId, role);
            Assert.All(template, key => Assert.Contains(key, held));
            var row = Assert.Single(await w.AuditAsync(SeedTenantId), a => a.Metadata.Contains($"\"roleName\":\"{role}\""));
            Assert.Contains("\"trigger\":\"seed\"", row.Metadata);
            Assert.Contains($"\"permissionCount\":{template.Count}", row.Metadata);
        }
    }

    // ── plumbing ───────────────────────────────────────────────────────────────────────────────────────

    private async Task<IMongoDatabase> FreshDatabaseAsync(string name)
    {
        var client = new MongoClient(_host.ConnectionString);
        await client.DropDatabaseAsync(name);
        return client.GetDatabase(name);
    }

    // The host's database: the production indexes (uq_roles_tenant_name_active, the role-permission unique key) and the catalog the
    // production DataSeeder wrote at host start. Every test uses a fresh tenant id, so tests never see each other's roles.
    private Task<World> WorldAsync() => Task.FromResult(new World(_host.Database));

    /// <summary>One test's world on a fresh database: the production controller + provisioning + repositories.</summary>
    private sealed class World(IMongoDatabase database)
    {
        public IMongoDatabase Database { get; } = database;

        private readonly ITenantContext _context = new NoTenant();

        public InternalEventsController Controller()
        {
            var roles = new RoleRepository(Database, _context);
            var controller = new InternalEventsController(
                new AlwaysAuthorized(),
                RoleProvisioning.Create(Database, _context),
                new NoEntitlements(),
                new NoSync(),
                new IntegrationEventInboxRepository(Database),
                new UserRepository(Database, _context),
                roles,
                new UserRoleRepository(Database, _context),
                new TenantUserMembershipRepository(Database),
                new PlainHasher(),
                // Main sync 2026-10-13: the lane's controller mints the set-password link itself (token + hash, refresh-token
                // store) and records the account write (IUserAuditRecorder) — where main asked the login settings and the
                // password policy. Fakes for exactly what the provisioning paths touch; the repository is the real one.
                Tokens,
                Tokens,
                NullLogger<InternalEventsController>.Instance,
                new RefreshTokenRepository(Database, _context, Tokens),
                new NoUserAudit());
            controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
            return controller;
        }

        public Task<IActionResult> ActivatedAsync(Guid tenant)
            => Controller().TenantActivated(new TenantActivatedIntegrationEvent(
                Guid.NewGuid(), tenant, "tenant.activated", 1, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, "platform"), CancellationToken.None);

        public Task<IActionResult> InvitedAsync(Guid tenant, string email)
            => Controller().TenantAdminInvited(new InternalEventsController.TenantAdminInvitationProvisioningRequest(
                tenant, Guid.NewGuid(), "T-" + tenant.ToString("N")[..6], "Tenant", email, "Yeni Yonetici"), CancellationToken.None);

        public async Task<List<Permission>> CatalogAsync() => await Database.GetCollection<Permission>("permissions").Find(_ => true).ToListAsync();

        public async Task<Role> RoleAsync(Guid tenant, string name)
            => await Database.GetCollection<Role>("roles").Find(r => r.TenantId == tenant && r.Name == name && !r.IsDeleted).SingleAsync();

        public async Task<List<string>> KeysAsync(Guid tenant, string roleName)
        {
            var role = await RoleAsync(tenant, roleName);
            var catalog = (await CatalogAsync()).ToDictionary(p => p.Id);
            var grants = await Database.GetCollection<RolePermission>("rolePermissions").Find(rp => rp.RoleId == role.Id && rp.TenantId == tenant && !rp.IsDeleted).ToListAsync();
            return grants.Select(g => catalog[g.PermissionId].Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        }

        /// <summary>What an administrator does in the role screen: one grant of the role is removed. Returns its key.</summary>
        public async Task<string> RemoveOneAsync(Guid tenant, string roleName)
        {
            var key = (await KeysAsync(tenant, roleName)).First();
            await RemoveAsync(tenant, roleName, key);
            return key;
        }

        /// <summary>The same, choosing a System (template) grant — the seed tenant also carries Module grants of other paths.</summary>
        public async Task<string> RemoveOneSystemAsync(Guid tenant, string roleName)
        {
            var role = await RoleAsync(tenant, roleName);
            var catalog = (await CatalogAsync()).ToDictionary(p => p.Id);
            var grant = (await Database.GetCollection<RolePermission>("rolePermissions")
                    .Find(rp => rp.RoleId == role.Id && rp.TenantId == tenant && !rp.IsDeleted && rp.GrantSource == GrantSource.System).ToListAsync())
                .Where(g => !DefaultRolePermissionTemplate.TenantSelfServicePermissions.Contains(catalog[g.PermissionId].Key))
                .OrderBy(g => catalog[g.PermissionId].Key, StringComparer.Ordinal)
                .First();
            await Database.GetCollection<RolePermission>("rolePermissions").DeleteOneAsync(rp => rp.Id == grant.Id);
            return catalog[grant.PermissionId].Key;
        }

        private async Task RemoveAsync(Guid tenant, string roleName, string key)
        {
            var role = await RoleAsync(tenant, roleName);
            var permission = (await CatalogAsync()).Single(p => p.Key == key);
            await Database.GetCollection<RolePermission>("rolePermissions").DeleteManyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id && rp.TenantId == tenant);
        }

        public async Task DeleteRoleAsync(Guid tenant, string roleName)
        {
            var role = await RoleAsync(tenant, roleName);
            await Database.GetCollection<RolePermission>("rolePermissions").DeleteManyAsync(rp => rp.RoleId == role.Id);
            await Database.GetCollection<Role>("roles").DeleteOneAsync(r => r.Id == role.Id);
        }

        public async Task<List<AuthAuditLog>> AuditAsync(Guid tenant)
            => await Database.GetCollection<AuthAuditLog>("authAuditLogs").Find(a => a.TenantId == tenant && a.EventName == AuditEvent).SortBy(a => a.OccurredAt).ToListAsync();
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public bool IsResolved => false;
        public bool IsPlatformContext => false;
        public Guid? TargetTenantId => null;
        public void SetTenant(Guid tenantId) { }
        public void SetPlatformContext(Guid targetTenantId) { }
    }

    private sealed class AlwaysAuthorized : IInternalEventAuthService
    {
        public bool IsAuthorized(string? apiKeyHeaderValue) => true;
    }

    private sealed class NoEntitlements : ITenantEntitlementClient
    {
        public Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<EntitledModulePermissionKeys>>([]);
        public Task<TenantEntitlementReadResult> ReadEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(TenantEntitlementReadResult.Unavailable());
    }

    private sealed class NoSync : IEntitlementPermissionSyncService
    {
        public Task GrantModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task RevokeModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SyncTenantModulesAsync(Guid tenantId, IReadOnlyCollection<string> entitledModuleCodes, string actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task GrantModuleWithKeysAsync(Guid tenantId, string moduleCode, IReadOnlyCollection<string> permissionKeys, string actor, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SyncTenantModulesWithKeysAsync(Guid tenantId, IReadOnlyCollection<EntitledModulePermissionKeys> modules, string actor, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static readonly SetupLinkTokens Tokens = new();

    private sealed class SetupLinkTokens : ITokenService, IRefreshTokenHasher
    {
        public string GenerateRefreshToken() => Guid.NewGuid().ToString("N");
        public string Hash(string refreshToken) => "test-hash:" + refreshToken;

        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions) => throw Unused();
        public string GenerateAccessToken(User user, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw Unused();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions) => throw Unused();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes) => throw Unused();
        public string GeneratePlatformAccessToken(Guid userId, string email, string? firstName, string? lastName, Guid tenantId, string actorType, IEnumerable<string> roles, IEnumerable<string> permissions, int expiresInMinutes, bool requiresPasswordChange) => throw Unused();
        public ClaimsPrincipal GetPrincipalFromExpiredToken(string token) => throw Unused();

        private static NotSupportedException Unused() => new("not used by the provisioning paths under test");
    }

    private sealed class NoUserAudit : IUserAuditRecorder
    {
        public Task RecordAsync(string eventName, Guid tenantId, Guid targetUserId, IReadOnlyDictionary<string, object?> metadata, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task RecordAsync(string eventName, Guid tenantId, Guid targetUserId, IReadOnlyDictionary<string, object?> metadata, bool succeeded, CancellationToken ct)
            => Task.CompletedTask;
    }

    private sealed class PlainHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password.Length;
        public bool Verify(string password, string hash) => false;
    }

    private sealed class DefaultLoginSettings : ITenantLoginSettingsClient
    {
        public Task<TenantLoginSettingsSnapshot> GetAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(new TenantLoginSettingsSnapshot(tenantId, false, false, true, false, 12, true, true, true, true, null, 60, 7, 5, 15));
    }

    private sealed class TemporaryPasswords : IPasswordPolicyService
    {
        public Task ValidateTenantPasswordAsync(Guid tenantId, Guid? userId, string password, string context, CancellationToken ct) => Task.CompletedTask;
        public string GenerateTemporaryPassword(TenantLoginSettingsSnapshot settings) => "Gecici-" + Guid.NewGuid().ToString("N")[..12];
    }
}

/// <summary>The production provisioning service over real repositories and the production audit recorder (authAuditLogs).</summary>
internal static class RoleProvisioning
{
    public static IRoleProvisioningService Create(IMongoDatabase database, ITenantContext context)
        => new RoleProvisioningService(
            new RoleRepository(database, context),
            new PermissionRepository(database),
            new RolePermissionRepository(database, context),
            new RbacAuditRecorder(new AuthAuditService(database), new NoUser(), NullLogger<RbacAuditRecorder>.Instance));

    /// <summary>The internal events carry no user: the audit actor is null.</summary>
    private sealed class NoUser : ICurrentUserAccessor
    {
        public Guid? UserId => null;
    }
}
