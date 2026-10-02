using System.Net;
using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles;
using Diten.AuthService.Application.Tests.Roles;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// BL-452 / WP-ROLES-CLOSE-01 — the one-way export backfill as an AUTHORITY WRITER, scenario by scenario (CT acceptance
/// S-A … S-H), for BOTH export keys (<c>auth.users.export</c>, <c>auth.roles.export</c>): the production
/// <see cref="ExportGrantBackfillRunner"/> and the production <see cref="DataSeeder"/> on the test-owned mongod.
///
/// <para>An OLD tenant here is what an old tenant is in production: roles that exist in the database and no mark. Its
/// roles are therefore written straight into the collection (the role repository of this build would mark the tenant
/// "born"). A tenant created AFTER the feature goes through the production provisioning service.</para>
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class ExportGrantBackfillScenarioTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly AccountKindAcceptance.AuthTestHost _host;

    public ExportGrantBackfillScenarioTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    public static TheoryData<string> Keys => new() { "users", "roles" };

    private static ExportGrantBackfill.KeyPair Pair(string key) => key == "users" ? ExportGrantBackfill.Users : ExportGrantBackfill.Roles;

    private IMongoCollection<Permission> Permissions => _host.Database.GetCollection<Permission>("permissions");
    private IMongoCollection<Role> RoleCol => _host.Database.GetCollection<Role>("roles");
    private IMongoCollection<RolePermission> Grants => _host.Database.GetCollection<RolePermission>("rolePermissions");
    private IMongoCollection<AuthAuditLog> Audit => _host.Database.GetCollection<AuthAuditLog>("authAuditLogs");
    private IMongoCollection<PermissionReconciliationMark> Marks => _host.Database.GetCollection<PermissionReconciliationMark>(PermissionReconciliationMark.CollectionName);

    // ── the base case ────────────────────────────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task An_old_tenants_reading_roles_receive_export_once_a_role_without_read_does_not_and_a_second_start_writes_nothing(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        var auditors = await OldRoleAsync(tenant, "Auditors", pair.ReadKey);
        var writers = await OldRoleAsync(tenant, "Writers", "auth.roles.create"); // no read → no export

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsAsync(readers, pair));
        Assert.True(await HoldsAsync(auditors, pair));
        Assert.False(await HoldsAsync(writers, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, readers, auditors);
        Assert.Equal(ExportGrantBackfill.OriginBackfilled, (await MarkAsync(tenant, pair)).Origin);
        var grants = await Grants.CountDocumentsAsync(g => g.TenantId == tenant);
        var audit = await Audit.CountDocumentsAsync(a => a.TenantId == tenant);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.Equal(grants, await Grants.CountDocumentsAsync(g => g.TenantId == tenant));
        Assert.Equal(audit, await Audit.CountDocumentsAsync(a => a.TenantId == tenant));
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task The_audit_row_of_a_backfilled_grant_names_the_role_the_key_and_the_system_actor(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        var row = Assert.Single(await BackfillAuditRowsAsync(tenant, pair));
        Assert.Equal(ExportGrantBackfill.AuditEventName, row.EventName);
        Assert.Equal(Guid.Empty, row.UserId); // no person
        using var metadata = JsonDocument.Parse(row.Metadata);
        Assert.Equal(readers, metadata.RootElement.GetProperty("roleId").GetGuid());
        Assert.Equal("Readers", metadata.RootElement.GetProperty("roleName").GetString());
        Assert.Equal(pair.ExportKey, metadata.RootElement.GetProperty("permissionKey").GetString());
        Assert.Equal("system", metadata.RootElement.GetProperty("actor").GetString());
        Assert.Equal(pair.AuditSource, metadata.RootElement.GetProperty("source").GetString());
    }

    // ── S-A ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("users", ExportGrantBackfillRunner.Stage.AuditWritten)]
    [InlineData("users", ExportGrantBackfillRunner.Stage.GrantWritten)]
    [InlineData("users", ExportGrantBackfillRunner.Stage.TenantGrantsWritten)]
    [InlineData("roles", ExportGrantBackfillRunner.Stage.AuditWritten)]
    [InlineData("roles", ExportGrantBackfillRunner.Stage.GrantWritten)]
    [InlineData("roles", ExportGrantBackfillRunner.Stage.TenantGrantsWritten)]
    public async Task S_A_A_run_that_dies_half_way_is_finished_by_the_next_start_with_exactly_one_audit_row_per_grant(
        string key, ExportGrantBackfillRunner.Stage dieAt)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var roles = new[]
        {
            await OldRoleAsync(tenant, "Readers-1", pair.ReadKey),
            await OldRoleAsync(tenant, "Readers-2", pair.ReadKey),
            await OldRoleAsync(tenant, "Readers-3", pair.ReadKey)
        };
        var seen = 0;

        // The first start dies inside this tenant: after the 2nd role's audit row / after its grant / before the mark.
        var died = await Assert.ThrowsAsync<InvalidOperationException>(() => ExportGrantBackfillRunner.RunAsync(_host.Database, (stage, p, id) =>
        {
            if (p != pair || stage != dieAt) return Task.CompletedTask;
            var mine = stage == ExportGrantBackfillRunner.Stage.TenantGrantsWritten ? id == tenant : roles.Contains(id);
            if (mine && (stage == ExportGrantBackfillRunner.Stage.TenantGrantsWritten || ++seen == 2))
            {
                throw new InvalidOperationException("the process died here");
            }

            return Task.CompletedTask;
        }));
        Assert.Equal("the process died here", died.Message);
        Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey)); // not marked: not finished

        await DataSeeder.SeedAsync(_host.Database); // the next start

        foreach (var role in roles) Assert.True(await HoldsAsync(role, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, roles);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    // ── S-B ──────────────────────────────────────────────────────────────────────────────────────────────

    // What a first start that failed in an EARLIER seed step leaves behind: the template already gave the system Admin
    // the new key, the backfill never ran. (The failing step itself cannot be injected into the seeder; its outcome is.)
    [Theory, MemberData(nameof(Keys))]
    public async Task S_B_After_a_first_start_that_failed_before_the_backfill_the_old_tenant_is_still_processed_in_full(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var admin = await OldRoleAsync(tenant, "Admin", system: true, source: GrantSource.System, pair.ReadKey, pair.ExportKey);
        var viewer = await OldRoleAsync(tenant, "Viewer", system: true, source: GrantSource.System, pair.ReadKey);
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsAsync(viewer, pair));
        Assert.True(await HoldsAsync(readers, pair));
        Assert.True(await HoldsAsync(admin, pair)); // still exactly one grant (HoldsAsync counts == 1)
        await AssertOneAuditRowPerGrantAsync(tenant, pair, viewer, readers);
    }

    // ── S-C ──────────────────────────────────────────────────────────────────────────────────────────────

    // The host started on an empty database: the production seeder created the default tenant's roles.
    [Theory, MemberData(nameof(Keys))]
    public async Task S_C_On_a_brand_new_database_the_default_tenants_Viewer_reads_but_does_not_export(string key)
    {
        var pair = Pair(key);
        var viewer = await RoleCol.Find(r => r.TenantId == DefaultTenantId && r.Name == DefaultRolePermissionTemplate.ViewerRole).SingleAsync();
        var admin = await RoleCol.Find(r => r.TenantId == DefaultTenantId && r.Name == DefaultRolePermissionTemplate.AdminRole).SingleAsync();

        await DataSeeder.SeedAsync(_host.Database); // and however many starts follow

        Assert.True(await HoldsKeyAsync(viewer.Id, pair.ReadKey));
        Assert.False(await HoldsAsync(viewer.Id, pair));
        Assert.True(await HoldsAsync(admin.Id, pair)); // the template gives it to Admin
        Assert.Equal(ExportGrantBackfill.OriginBorn, (await MarkAsync(DefaultTenantId, pair)).Origin);
        Assert.Empty(await BackfillAuditRowsAsync(DefaultTenantId, pair));
    }

    // ── S-D ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task S_D_A_tenant_provisioned_after_the_feature_never_receives_the_backfill(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        Guid custom;
        using (var scope = _host.Factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenant);
            // The production path a new tenant takes (Admin + Viewer from the template) …
            await scope.ServiceProvider.GetRequiredService<IRoleProvisioningService>().EnsureDefaultRolesAsync(tenant);
            // … and a reading role its administrator then opens ON PURPOSE without export.
            var role = await scope.ServiceProvider.GetRequiredService<IRoleRepository>()
                .CreateAsync(new Role("Readers", "Readers", "opened without export", tenant), CancellationToken.None);
            custom = role.Id;
            await GrantAsync(custom, tenant, GrantSource.Manual, pair.ReadKey);
        }

        var viewer = await RoleCol.Find(r => r.TenantId == tenant && r.Name == DefaultRolePermissionTemplate.ViewerRole).SingleAsync();
        Assert.Equal(ExportGrantBackfill.OriginBorn, (await MarkAsync(tenant, pair)).Origin);

        await DataSeeder.SeedAsync(_host.Database);
        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsKeyAsync(viewer.Id, pair.ReadKey));
        Assert.False(await HoldsAsync(viewer.Id, pair));
        Assert.False(await HoldsAsync(custom, pair));
        Assert.Empty(await BackfillAuditRowsAsync(tenant, pair));
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    // ── S-E ──────────────────────────────────────────────────────────────────────────────────────────────

    // Platform deployed first: its catalog sync created the key in the old AuthService and the entitlement sync handed
    // it to Admin as a MODULE grant — before this AuthService ever ran its backfill.
    [Theory, MemberData(nameof(Keys))]
    public async Task S_E_An_Admin_that_already_holds_the_key_as_a_module_grant_does_not_make_the_tenant_done(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var admin = await OldRoleAsync(tenant, "Admin", system: true, source: GrantSource.Module, pair.ReadKey, pair.ExportKey);
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        var auditors = await OldRoleAsync(tenant, "Auditors", pair.ReadKey);

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsAsync(readers, pair));
        Assert.True(await HoldsAsync(auditors, pair));
        Assert.True(await HoldsAsync(admin, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, readers, auditors);
    }

    // ── S-F ──────────────────────────────────────────────────────────────────────────────────────────────

    // Deterministic interleaving: instance A has planned and written the first audit row when instance B runs from
    // start to finish. A then meets B's audit rows, grants and mark as duplicate keys — and must not fall over.
    [Theory, MemberData(nameof(Keys))]
    public async Task S_F_Two_instances_starting_together_leave_one_grant_one_audit_row_and_one_mark_and_neither_fails(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var roles = new[] { await OldRoleAsync(tenant, "Readers-1", pair.ReadKey), await OldRoleAsync(tenant, "Readers-2", pair.ReadKey) };
        var otherInstanceRan = false;

        await ExportGrantBackfillRunner.RunAsync(_host.Database, async (stage, p, id) =>
        {
            if (otherInstanceRan || p != pair || stage != ExportGrantBackfillRunner.Stage.AuditWritten || !roles.Contains(id)) return;
            otherInstanceRan = true;
            await ExportGrantBackfillRunner.RunAsync(_host.Database);
        });

        Assert.True(otherInstanceRan);
        foreach (var role in roles) Assert.True(await HoldsAsync(role, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, roles);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task S_F_Four_instances_racing_for_real_leave_the_same_single_result(string key)
    {
        var pair = Pair(key);
        var tenant = Guid.NewGuid();
        var roles = new[] { await OldRoleAsync(tenant, "Readers-1", pair.ReadKey), await OldRoleAsync(tenant, "Readers-2", pair.ReadKey) };

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => ExportGrantBackfillRunner.RunAsync(_host.Database))));

        foreach (var role in roles) Assert.True(await HoldsAsync(role, pair));
        await AssertOneAuditRowPerGrantAsync(tenant, pair, roles);
        Assert.Equal(1, await Marks.CountDocumentsAsync(m => m.TenantId == tenant && m.Key == pair.ExportKey));
    }

    // ── S-G ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task S_G_Export_backfilled_onto_a_custom_role_can_be_taken_away_on_the_products_own_path_and_stays_away(string key)
    {
        var pair = Pair(key);
        // A person of the old tenant who administers roles (the user is written first; a user is not a role, so the
        // tenant is still an old, unmarked tenant when its roles are written below).
        var world = RoleEndpointWorld.Create(_host, "Rana", "Steward");
        var tenant = world.TenantId;
        var admin = await OldRoleAsync(tenant, "Admin", system: true, source: GrantSource.System, pair.ReadKey);
        var viewer = await OldRoleAsync(tenant, "Viewer", system: true, source: GrantSource.System, pair.ReadKey);
        var readers = await OldRoleAsync(tenant, "Readers", pair.ReadKey);
        await DataSeeder.SeedAsync(_host.Database);
        var export = await Permissions.Find(p => p.Key == pair.ExportKey).SingleAsync();

        // Admin: the template gives it export → template-managed, locked on the screen like the rest of its baseline.
        // Viewer and the custom role: nothing re-provisions the grant → it is the administrator's to take away.
        Assert.Equal(GrantSource.System, (await GrantRowAsync(admin, export.Id)).GrantSource);
        Assert.Equal(GrantSource.Manual, (await GrantRowAsync(viewer, export.Id)).GrantSource);
        Assert.Equal(GrantSource.Manual, (await GrantRowAsync(readers, export.Id)).GrantSource);

        using var client = world.Client();
        // The real DELETE api/roles/{id}/permissions/{permissionId} → RevokePermissionCommand.
        var revoked = await client.DeleteAsync($"api/roles/{readers}/permissions/{export.Id}");
        Assert.True(revoked.StatusCode == HttpStatusCode.NoContent, await revoked.Content.ReadAsStringAsync());
        Assert.False(await HoldsAsync(readers, pair));

        var locked = await client.DeleteAsync($"api/roles/{admin}/permissions/{export.Id}");
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        Assert.Contains(RoleErrorCodes.PermissionGrantManaged, await locked.Content.ReadAsStringAsync());

        await DataSeeder.SeedAsync(_host.Database); // a restart
        await DataSeeder.SeedAsync(_host.Database);

        Assert.False(await HoldsAsync(readers, pair)); // not handed back
        Assert.True(await HoldsAsync(viewer, pair));
        Assert.True(await HoldsAsync(admin, pair));
    }

    // ── S-H ──────────────────────────────────────────────────────────────────────────────────────────────

    [Theory, MemberData(nameof(Keys))]
    public async Task S_H_A_role_without_a_tenant_is_not_processed_and_the_empty_tenant_is_never_marked(string key)
    {
        var pair = Pair(key);
        var orphan = await OldRoleAsync(Guid.Empty, "Orphans-" + Guid.NewGuid().ToString("N")[..8], pair.ReadKey);
        try
        {
            await DataSeeder.SeedAsync(_host.Database);

            Assert.False(await HoldsAsync(orphan, pair));
            Assert.Equal(0, await Marks.CountDocumentsAsync(m => m.TenantId == Guid.Empty));
            Assert.Empty(await BackfillAuditRowsAsync(Guid.Empty, pair));
        }
        finally
        {
            // The fixture is test-owned; it must not sit in the shared test database as a tenant-less role.
            await Grants.DeleteManyAsync(g => g.RoleId == orphan);
            await RoleCol.DeleteOneAsync(r => r.Id == orphan);
        }
    }

    [Theory, MemberData(nameof(Keys))]
    public async Task One_tenants_backfill_writes_nothing_into_another_tenant(string key)
    {
        var pair = Pair(key);
        var mine = Guid.NewGuid();
        var theirs = Guid.NewGuid();
        var myReaders = await OldRoleAsync(mine, "Readers", pair.ReadKey);
        var theirWriters = await OldRoleAsync(theirs, "Writers", "auth.roles.create");

        await DataSeeder.SeedAsync(_host.Database);

        Assert.True(await HoldsAsync(myReaders, pair));
        Assert.False(await HoldsAsync(theirWriters, pair));
        Assert.Empty(await BackfillAuditRowsAsync(theirs, pair));
        Assert.All(await BackfillAuditRowsAsync(mine, pair), row => Assert.Equal(mine, row.TenantId));
    }

    // ── helpers ──────────────────────────────────────────────────────────────────────────────────────────

    private Task<Guid> OldRoleAsync(Guid tenant, string name, params string[] keys)
        => OldRoleAsync(tenant, name, system: false, source: GrantSource.Manual, keys);

    /// <summary>A role as an old database holds it: written straight into the collection, no mark.</summary>
    private async Task<Guid> OldRoleAsync(Guid tenant, string name, bool system, GrantSource source, params string[] keys)
    {
        var role = new Role(name, name, "export backfill fixture", tenant);
        if (system) role.MarkAsSystem();
        await RoleCol.InsertOneAsync(role);
        foreach (var key in keys) await GrantAsync(role.Id, tenant, source, key);
        return role.Id;
    }

    private async Task GrantAsync(Guid roleId, Guid tenant, GrantSource source, string key)
    {
        var permission = await Permissions.Find(p => p.Key == key).SingleAsync();
        await Grants.InsertOneAsync(source switch
        {
            GrantSource.System => RolePermission.SystemGrant(roleId, permission.Id, tenant, "system"),
            GrantSource.Module => RolePermission.ModuleGrant(roleId, permission.Id, tenant, "system", "access-governance"),
            _ => RolePermission.ManualGrant(roleId, permission.Id, tenant, "export-backfill-fixture")
        });
    }

    private Task<bool> HoldsAsync(Guid roleId, ExportGrantBackfill.KeyPair pair) => HoldsKeyAsync(roleId, pair.ExportKey);

    /// <summary>EXACTLY one live grant of the key on the role (a duplicate is a failure, not a pass).</summary>
    private async Task<bool> HoldsKeyAsync(Guid roleId, string key)
    {
        var id = (await Permissions.Find(p => p.Key == key).SingleAsync()).Id;
        return await Grants.CountDocumentsAsync(g => g.RoleId == roleId && g.PermissionId == id && g.IsDeleted == false) == 1;
    }

    private async Task<RolePermission> GrantRowAsync(Guid roleId, Guid permissionId)
        => await Grants.Find(g => g.RoleId == roleId && g.PermissionId == permissionId).SingleAsync();

    private async Task<PermissionReconciliationMark> MarkAsync(Guid tenant, ExportGrantBackfill.KeyPair pair)
        => await Marks.Find(m => m.TenantId == tenant && m.Key == pair.ExportKey).SingleAsync();

    private async Task<List<AuthAuditLog>> BackfillAuditRowsAsync(Guid tenant, ExportGrantBackfill.KeyPair pair)
    {
        var rows = await Audit.Find(a => a.TenantId == tenant && a.EventName == ExportGrantBackfill.AuditEventName).ToListAsync();
        return rows.Where(r => r.Metadata.Contains($"\"{pair.AuditSource}\"", StringComparison.Ordinal)).ToList();
    }

    private async Task AssertOneAuditRowPerGrantAsync(Guid tenant, ExportGrantBackfill.KeyPair pair, params Guid[] roles)
    {
        var rows = await BackfillAuditRowsAsync(tenant, pair);
        Assert.Equal(roles.Length, rows.Count);
        foreach (var role in roles)
        {
            Assert.Single(rows, r => r.Metadata.Contains(role.ToString(), StringComparison.OrdinalIgnoreCase));
        }
    }
}
