extern alias PlatformProducer;

using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;
using Diten.BuildingBlocks.ModuleRegistration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using PlatformModulePages = PlatformProducer::Diten.Platform.Application.Features.ModulePages;
using PlatformTasks = PlatformProducer::Diten.Platform.Application.Features.Tasks.SelfRegistration;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// BL-411 (CT benchmark D3) — the allowlist is measured against PRODUCTION code on both sides of the sync, not against
/// a copy: the Platform Tasks manifest that registers each key's page route, and Platform's route→scope rule that
/// authors every route-derived Scope. If an entry's route drifts, or the list grows, this goes red.
/// </summary>
public sealed class TenantRouteScopeCorrectionTests
{
    internal const string ChecklistTemplatesManage = "platform.tasks.checklist-templates.manage";
    internal const string TemplatesManage = "platform.tasks.templates.manage";

    internal static readonly ModuleManifestDocument TasksManifest = new PlatformTasks.TaskManifestProvider().GetManifest();

    [Fact]
    public void The_allowlist_is_exactly_the_two_BL411_template_keys()
    {
        Assert.Equal(
            new[] { ChecklistTemplatesManage, TemplatesManage },
            TenantRouteScopeCorrections.Allowlist.Select(entry => entry.PermissionKey).OrderBy(key => key, StringComparer.Ordinal));
    }

    // Gate 3 (registered route must be a tenant route) is unreachable with today's allowlist — both entries are tenant
    // routes, which this test proves from the production manifest. It stays as a fail-closed guard for a future entry.
    [Fact]
    public void Each_allowlisted_key_is_registered_by_the_production_Tasks_manifest_on_its_listed_tenant_route_and_nowhere_on_a_platform_route()
    {
        foreach (var entry in TenantRouteScopeCorrections.Allowlist)
        {
            var page = Assert.Single(TasksManifest.Pages, p => p.RequiredPermission == entry.PermissionKey);
            Assert.Equal(entry.RegisteredRoutePath, page.RoutePath);
            Assert.Equal("Tenant", PlatformModulePages.ModulePageDescriptorNormalizer.ScopeFromRoute(page.RoutePath));
            Assert.True(TenantRouteScopeCorrections.IsTenantRoute(entry.RegisteredRoutePath));

            // The manifest reconcile syncs every page key AND every action key with its page's route; none may be platform.
            var declaredRoutes = TasksManifest.Pages
                .SelectMany(p => p.Actions.Select(a => (Key: a.PermissionKey, p.RoutePath)).Append((Key: p.RequiredPermission, p.RoutePath)))
                .Where(declared => declared.Key == entry.PermissionKey)
                .ToList();
            Assert.NotEmpty(declaredRoutes);
            Assert.All(declaredRoutes, declared =>
                Assert.Equal("Tenant", PlatformModulePages.ModulePageDescriptorNormalizer.ScopeFromRoute(declared.RoutePath)));
        }
    }

    [Fact]
    public void Neither_template_key_is_in_the_production_seed_catalog()
    {
        // They are synced-only: the seed-list Scope reconcile can never reach them, which is why the correction exists.
        var seeded = DataSeeder.BuildCanonicalPermissions().Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(ChecklistTemplatesManage, seeded);
        Assert.DoesNotContain(TemplatesManage, seeded);
    }

    [Theory]
    [InlineData("/Platform")]
    [InlineData("/Platform/")]
    [InlineData("/platform/tenants")]
    [InlineData(" /Platform/Tasks/Templates ")]
    [InlineData("/PlatformTools")]
    [InlineData("/Tasks/Templates")]
    [InlineData("/Tasks/ChecklistTemplates")]
    [InlineData("/Organization/Units")]
    public void The_route_rule_agrees_with_Platforms_ScopeFromRoute(string route)
    {
        Assert.Equal(
            PlatformModulePages.ModulePageDescriptorNormalizer.ScopeFromRoute(route) == "Tenant",
            TenantRouteScopeCorrections.IsTenantRoute(route));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_route_never_qualifies_as_a_tenant_route(string? route)
    {
        Assert.False(TenantRouteScopeCorrections.IsTenantRoute(route));
    }
}

/// <summary>
/// BL-411 — the correction against a test-owned mongod (<see cref="AccountKindAcceptance.AuthTestHost"/>'s own process,
/// never the shared 27017). The permission catalog is global, not tenant-bound, so the pure correction cases and the
/// startup-wiring proof use their own FIXED-name databases on that runner (DB-010), reset per test. The HTTP grant
/// case and the entitlement-sync case run against the host's own database through production DI, each in a fresh
/// tenant; the two template rows there are reset to PlatformAdmin by each test that needs them.
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class TenantRouteScopeCorrectionMongoTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private const string CorrectionDatabase = "diten_auth_itest_scope_correction";
    private const string SeederDatabase = "diten_auth_itest_scope_correction_seeder";

    private const string ChecklistTemplatesManage = TenantRouteScopeCorrectionTests.ChecklistTemplatesManage;
    private const string TemplatesManage = TenantRouteScopeCorrectionTests.TemplatesManage;

    // A real platform-admin key (/Platform/Tenants/... surface): must stay PlatformAdmin.
    private const string RealPlatformAdminKey = "platform.tenants.quotas.manage";
    // Tenant-route keys that are NOT allowlisted, stamped PlatformAdmin: must NOT change (narrowness).
    private const string UnlistedTenantRouteKey = "platform.tasks.task-types.manage";
    private const string UnlistedB2Key = "platform.work-aggregation.inbox.view";
    private const string AlreadyTenantKey = "platform.tasks.read";
    // Ordinary tasks keys, Tenant in the dev catalog (measured: every platform.tasks.* key but the two is Scope 0).
    private const string TasksReadKey = "platform.tasks.read";
    private const string TasksCreateKey = "platform.tasks.create";

    // EntitlementSyncConsumer.Actor — the actor the real consumer passes to the sync.
    private const string EntitlementActor = "entitlement-sync";

    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly AccountKindAcceptance.AuthTestHost _host;

    public TenantRouteScopeCorrectionMongoTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    // (a) ─────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Correction_moves_exactly_the_two_allowlisted_keys_to_Tenant_and_leaves_every_other_row_and_every_grant_as_it_was()
    {
        var database = await FreshDatabaseAsync(CorrectionDatabase);
        var permissions = database.GetCollection<Permission>("permissions");
        var checklist = CatalogRow(ChecklistTemplatesManage, PermissionScope.PlatformAdmin);
        var templates = CatalogRow(TemplatesManage, PermissionScope.PlatformAdmin);
        await permissions.InsertManyAsync(
        [
            checklist,
            templates,
            CatalogRow(RealPlatformAdminKey, PermissionScope.PlatformAdmin),
            CatalogRow(AlreadyTenantKey, PermissionScope.Tenant)
        ]);

        // The measured dev holder shape: the default tenant's SuperAdmin holds both keys through a system grant.
        var superAdminRoleId = Guid.NewGuid();
        await database.GetCollection<RolePermission>("rolePermissions").InsertManyAsync(
        [
            RolePermission.SystemGrant(superAdminRoleId, checklist.Id, DefaultTenantId, "system"),
            RolePermission.SystemGrant(superAdminRoleId, templates.Id, DefaultTenantId, "system")
        ]);

        var correctedBefore = await RawByKeysAsync(database, [ChecklistTemplatesManage, TemplatesManage]);
        var othersBefore = await RawExceptKeysAsync(database, [ChecklistTemplatesManage, TemplatesManage]);
        var grantsBefore = await RawGrantsAsync(database);

        var applied = await DataSeeder.ApplyTenantRouteScopeCorrectionsAsync(permissions);

        Assert.Equal(
            new[] { ChecklistTemplatesManage, TemplatesManage },
            applied.Select(c => c.PermissionKey).OrderBy(key => key, StringComparer.Ordinal));

        foreach (var key in new[] { ChecklistTemplatesManage, TemplatesManage })
        {
            var row = await permissions.Find(p => p.Key == key).SingleAsync();
            Assert.Equal(PermissionScope.Tenant, row.Scope);
            Assert.Equal("system", row.UpdatedBy);
            Assert.NotNull(row.UpdatedAt);
        }

        // Only Scope/UpdatedAt/UpdatedBy moved on the two corrected rows; everything else is byte-identical.
        var correctedAfter = await RawByKeysAsync(database, [ChecklistTemplatesManage, TemplatesManage]);
        Assert.Equal(WithoutCorrectionFields(correctedBefore), WithoutCorrectionFields(correctedAfter));
        Assert.Equal(othersBefore, await RawExceptKeysAsync(database, [ChecklistTemplatesManage, TemplatesManage]));
        Assert.Equal(PermissionScope.PlatformAdmin, (await permissions.Find(p => p.Key == RealPlatformAdminKey).SingleAsync()).Scope);

        // The correction run itself adds or removes no grant — the SuperAdmin rows are exactly what they were.
        Assert.Equal(grantsBefore, await RawGrantsAsync(database));
    }

    // (b) ─────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task A_second_run_changes_nothing_and_reports_nothing()
    {
        var database = await FreshDatabaseAsync(CorrectionDatabase);
        var permissions = database.GetCollection<Permission>("permissions");
        await permissions.InsertManyAsync(
        [
            CatalogRow(ChecklistTemplatesManage, PermissionScope.PlatformAdmin),
            CatalogRow(TemplatesManage, PermissionScope.PlatformAdmin),
            CatalogRow(RealPlatformAdminKey, PermissionScope.PlatformAdmin)
        ]);

        var first = await DataSeeder.ApplyTenantRouteScopeCorrectionsAsync(permissions);
        Assert.Equal(2, first.Count);
        var afterFirst = await RawExceptKeysAsync(database, []);

        var second = await DataSeeder.ApplyTenantRouteScopeCorrectionsAsync(permissions);

        Assert.Empty(second);
        Assert.Equal(afterFirst, await RawExceptKeysAsync(database, []));
    }

    // (c) ─────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task A_tenant_route_key_outside_the_allowlist_stamped_PlatformAdmin_is_not_changed()
    {
        // Precondition from production code, so this proves NARROWNESS and not the route gate: the unlisted key is
        // registered by the Tasks manifest on a tenant route, exactly like the two allowlisted ones.
        var unlistedPage = Assert.Single(TenantRouteScopeCorrectionTests.TasksManifest.Pages, p => p.RequiredPermission == UnlistedTenantRouteKey);
        Assert.Equal("Tenant", PlatformModulePages.ModulePageDescriptorNormalizer.ScopeFromRoute(unlistedPage.RoutePath));

        var database = await FreshDatabaseAsync(CorrectionDatabase);
        var permissions = database.GetCollection<Permission>("permissions");
        await permissions.InsertManyAsync(
        [
            CatalogRow(ChecklistTemplatesManage, PermissionScope.PlatformAdmin), // positive control in the same run
            CatalogRow(UnlistedTenantRouteKey, PermissionScope.PlatformAdmin),
            CatalogRow(UnlistedB2Key, PermissionScope.PlatformAdmin)
        ]);
        var unlistedBefore = await RawByKeysAsync(database, [UnlistedTenantRouteKey, UnlistedB2Key]);

        var applied = await DataSeeder.ApplyTenantRouteScopeCorrectionsAsync(permissions);

        Assert.Equal(new[] { ChecklistTemplatesManage }, applied.Select(c => c.PermissionKey));
        Assert.Equal(unlistedBefore, await RawByKeysAsync(database, [UnlistedTenantRouteKey, UnlistedB2Key]));
        Assert.Equal(PermissionScope.PlatformAdmin, (await permissions.Find(p => p.Key == UnlistedTenantRouteKey).SingleAsync()).Scope);
        Assert.Equal(PermissionScope.PlatformAdmin, (await permissions.Find(p => p.Key == UnlistedB2Key).SingleAsync()).Scope);
    }

    // Gate coverage — soft-deleted row (CT 2026-09-15, item 3) ─────────────────────────────────────────
    // Intended rule: a soft-deleted allowlisted row IS corrected and STAYS deleted. Scope belongs to the key; the catalog
    // sync's reactivation (PermissionRepository.ReactivateAsync) never touches Scope and its update path cannot reach a
    // deleted row (ReplaceOneAsync filters IsDeleted=false), so leaving it PlatformAdmin would bring the stuck scope back
    // the day the key is reactivated. Nothing but Scope/UpdatedAt/UpdatedBy moves — in particular not IsDeleted.
    [Fact]
    public async Task A_soft_deleted_allowlisted_row_is_corrected_and_stays_deleted()
    {
        var database = await FreshDatabaseAsync(CorrectionDatabase);
        var permissions = database.GetCollection<Permission>("permissions");
        var deleted = CatalogRow(TemplatesManage, PermissionScope.PlatformAdmin);
        deleted.IsDeleted = true;
        var deletedPlatformKey = CatalogRow(RealPlatformAdminKey, PermissionScope.PlatformAdmin);
        deletedPlatformKey.IsDeleted = true;
        await permissions.InsertManyAsync([deleted, deletedPlatformKey]);
        var before = await RawByKeysAsync(database, [TemplatesManage]);
        var platformBefore = await RawByKeysAsync(database, [RealPlatformAdminKey]);

        var applied = await DataSeeder.ApplyTenantRouteScopeCorrectionsAsync(permissions);

        Assert.Equal(new[] { TemplatesManage }, applied.Select(c => c.PermissionKey));
        var row = await permissions.Find(p => p.Key == TemplatesManage).SingleAsync();
        Assert.Equal(PermissionScope.Tenant, row.Scope);
        Assert.True(row.IsDeleted);
        Assert.Equal(WithoutCorrectionFields(before), WithoutCorrectionFields(await RawByKeysAsync(database, [TemplatesManage])));
        Assert.Equal(platformBefore, await RawByKeysAsync(database, [RealPlatformAdminKey]));
    }

    // Gate coverage — stale snapshot / second writer (CT 2026-09-15, item 3) ─────────────────────────────
    // The plan is made from a read; another writer changes the row before the write lands. The write repeats the
    // PlatformAdmin condition server-side, so the stale plan writes nothing and reports nothing.
    [Fact]
    public async Task A_stale_plan_does_not_overwrite_a_second_writer_that_landed_first()
    {
        var database = await FreshDatabaseAsync(CorrectionDatabase);
        var permissions = database.GetCollection<Permission>("permissions");
        await permissions.InsertOneAsync(CatalogRow(ChecklistTemplatesManage, PermissionScope.PlatformAdmin));

        var plan = TenantRouteScopeCorrections.Plan(await permissions.Find(FilterDefinition<Permission>.Empty).ToListAsync());
        Assert.Single(plan);

        await permissions.UpdateOneAsync(
            p => p.Key == ChecklistTemplatesManage,
            Builders<Permission>.Update.Set(p => p.Scope, PermissionScope.Tenant).Set(p => p.UpdatedBy, "second-writer"));
        var afterSecondWriter = await RawByKeysAsync(database, [ChecklistTemplatesManage]);

        var applied = await DataSeeder.ApplyPlannedTenantRouteScopeCorrectionsAsync(permissions, plan);

        Assert.Empty(applied);
        Assert.Equal(afterSecondWriter, await RawByKeysAsync(database, [ChecklistTemplatesManage]));
    }

    // Item 2 window (CT 2026-09-15) — a re-stamp after the correction (a concurrent full-replace catalog sync from another
    // Auth process holding a stale PlatformAdmin copy) is not prevented; it fails closed and the next startup run corrects
    // it again. Documented, not a general mechanism.
    [Fact]
    public async Task A_re_stamp_after_the_correction_is_corrected_again_by_the_next_run()
    {
        var database = await FreshDatabaseAsync(CorrectionDatabase);
        var permissions = database.GetCollection<Permission>("permissions");
        await permissions.InsertOneAsync(CatalogRow(TemplatesManage, PermissionScope.PlatformAdmin));

        Assert.Single(await DataSeeder.ApplyTenantRouteScopeCorrectionsAsync(permissions));
        await permissions.UpdateOneAsync(p => p.Key == TemplatesManage, Builders<Permission>.Update.Set(p => p.Scope, PermissionScope.PlatformAdmin));

        var next = await DataSeeder.ApplyTenantRouteScopeCorrectionsAsync(permissions);

        Assert.Equal(new[] { TemplatesManage }, next.Select(c => c.PermissionKey));
        Assert.Equal(PermissionScope.Tenant, (await permissions.Find(p => p.Key == TemplatesManage).SingleAsync()).Scope);
    }

    // Startup wiring ───────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task The_production_seeder_applies_the_correction_on_startup_and_keeps_seeded_platform_keys_PlatformAdmin()
    {
        var database = await FreshDatabaseAsync(SeederDatabase);
        var permissions = database.GetCollection<Permission>("permissions");
        await permissions.InsertManyAsync(
        [
            CatalogRow(ChecklistTemplatesManage, PermissionScope.PlatformAdmin),
            CatalogRow(TemplatesManage, PermissionScope.PlatformAdmin),
            CatalogRow(UnlistedTenantRouteKey, PermissionScope.PlatformAdmin)
        ]);

        await DataSeeder.SeedAsync(database);

        Assert.Equal(PermissionScope.Tenant, (await permissions.Find(p => p.Key == ChecklistTemplatesManage).SingleAsync()).Scope);
        Assert.Equal(PermissionScope.Tenant, (await permissions.Find(p => p.Key == TemplatesManage).SingleAsync()).Scope);
        Assert.Equal(PermissionScope.PlatformAdmin, (await permissions.Find(p => p.Key == UnlistedTenantRouteKey).SingleAsync()).Scope);
        // A seeded platform-admin key (permission-scope-baseline.csv: 1) is still PlatformAdmin after the full seed.
        Assert.Equal(PermissionScope.PlatformAdmin, (await permissions.Find(p => p.Key == "platform.workflow.tasks.approve").SingleAsync()).Scope);
    }

    // (d) ─────────────────────────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task After_the_correction_a_tenant_role_can_be_granted_the_template_keys_through_the_real_assign_permission_path()
    {
        var hostPermissions = _host.Database.GetCollection<Permission>("permissions");
        // The measured dev state: catalog-created rows, PlatformAdmin (the seeder never creates them — see the seed test).
        var stuck = await EnsureTemplateRowsAsync(PermissionScope.PlatformAdmin);

        var admin = await SeedRolesAdminAsync();
        using var client = _host.Client(admin.Token, admin.TenantId);
        var roleId = await CreateRoleAsync(client, admin.TenantId);

        // Before: the stuck scope is exactly what refuses a tenant role (the 403 CT measured).
        foreach (var (key, permissionId) in stuck)
        {
            var refused = await client.PostAsJsonAsync($"api/roles/{roleId}/permissions", new { permissionId });
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{key} before correction: {(int)refused.StatusCode} {await refused.Content.ReadAsStringAsync()}");
        }

        var grants = _host.Database.GetCollection<RolePermission>("rolePermissions");
        var ids = stuck.Values.ToArray();
        var grantsBefore = await grants.CountDocumentsAsync(Builders<RolePermission>.Filter.In(rp => rp.PermissionId, ids));

        // The host's own seeded catalog (the production seeder already ran at host start, correction included): a seeded
        // platform-admin key is PlatformAdmin before and after — neither startup nor this run lowered anything else.
        const string seededPlatformAdminKey = "platform.workflow.tasks.approve";
        Assert.Equal(PermissionScope.PlatformAdmin, (await hostPermissions.Find(p => p.Key == seededPlatformAdminKey).SingleAsync()).Scope);

        var applied = await DataSeeder.ApplyTenantRouteScopeCorrectionsAsync(hostPermissions);

        Assert.Equal(new[] { ChecklistTemplatesManage, TemplatesManage }, applied.Select(c => c.PermissionKey).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(PermissionScope.PlatformAdmin, (await hostPermissions.Find(p => p.Key == seededPlatformAdminKey).SingleAsync()).Scope);
        Assert.Equal(grantsBefore, await grants.CountDocumentsAsync(Builders<RolePermission>.Filter.In(rp => rp.PermissionId, ids)));

        // After: the same request through the same pipeline is accepted and names the granting person.
        foreach (var (key, permissionId) in stuck)
        {
            var granted = await client.PostAsJsonAsync($"api/roles/{roleId}/permissions", new { permissionId });
            Assert.True(granted.StatusCode == HttpStatusCode.NoContent, $"{key} after correction: {(int)granted.StatusCode} {await granted.Content.ReadAsStringAsync()}");

            var grant = Assert.Single(await grants.Find(rp => rp.RoleId == roleId && rp.PermissionId == permissionId && rp.TenantId == admin.TenantId).ToListAsync());
            Assert.Equal(admin.UserId.ToString(), grant.AssignedBy);
        }
    }

    // Item 1 (CT decision 2026-09-15) — the accepted downstream effect, pinned ─────────────────────────────
    // The correction run changes no grant. A LATER entitlement sync for a tasks-entitled tenant then gives the tenant's
    // Admin exactly the two template keys (Admin = the module's full set), and Viewer neither (Viewer = read actions).
    // Measured through the production DI services on the host's own database, in a fresh tenant provisioned by the real
    // RoleProvisioningService. The first sync runs with no declared keys — the resolver fallback, which is the path that
    // excludes PlatformAdmin rows (ModulePermissionResolver) and therefore the one where the correction is observable.
    [Fact]
    public async Task After_the_correction_the_entitlement_sync_gives_Admin_of_a_tasks_entitled_tenant_exactly_the_two_keys_and_Viewer_neither()
    {
        var permissions = _host.Database.GetCollection<Permission>("permissions");
        var templateIds = await EnsureTemplateRowsAsync(PermissionScope.PlatformAdmin);
        await EnsureCatalogRowAsync(permissions, TasksReadKey, PermissionScope.Tenant);
        await EnsureCatalogRowAsync(permissions, TasksCreateKey, PermissionScope.Tenant);

        var tenantId = Guid.NewGuid();
        using var scope = _host.Factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var roles = sp.GetRequiredService<IRoleRepository>();
        var rolePermissions = sp.GetRequiredService<IRolePermissionRepository>();
        var sync = sp.GetRequiredService<IEntitlementPermissionSyncService>();

        await sp.GetRequiredService<IRoleProvisioningService>().EnsureDefaultRolesAsync(tenantId, CancellationToken.None);
        var adminRole = await roles.GetByNameAndTenantAsync(DefaultRolePermissionTemplate.AdminRole, tenantId, CancellationToken.None)
            ?? throw new InvalidOperationException("provisioning created no Admin role");
        var viewerRole = await roles.GetByNameAndTenantAsync(DefaultRolePermissionTemplate.ViewerRole, tenantId, CancellationToken.None)
            ?? throw new InvalidOperationException("provisioning created no Viewer role");

        IReadOnlyCollection<EntitledModulePermissionKeys> tasksEntitled = [new("TASKS", Array.Empty<string>())];

        // Before the correction: the tenant is entitled to tasks and synced — Admin has the Tenant tasks keys, not the two.
        await sync.SyncTenantModulesWithKeysAsync(tenantId, tasksEntitled, EntitlementActor, CancellationToken.None);
        var adminBefore = await KeysAsync(rolePermissions, adminRole.Id, tenantId);
        Assert.Contains(TasksCreateKey, adminBefore);
        Assert.DoesNotContain(ChecklistTemplatesManage, adminBefore);
        Assert.DoesNotContain(TemplatesManage, adminBefore);

        var grants = _host.Database.GetCollection<RolePermission>("rolePermissions");
        var rawBefore = await RawGrantsAsync(_host.Database);
        var idsBefore = (await grants.Find(FilterDefinition<RolePermission>.Empty).ToListAsync()).Select(rp => rp.Id).ToHashSet();

        var applied = await DataSeeder.ApplyTenantRouteScopeCorrectionsAsync(permissions);
        Assert.Equal(2, applied.Count);
        Assert.Equal(rawBefore, await RawGrantsAsync(_host.Database)); // the correction run itself grants nothing

        await sync.SyncTenantModulesWithKeysAsync(tenantId, tasksEntitled, EntitlementActor, CancellationToken.None);

        var added = (await grants.Find(FilterDefinition<RolePermission>.Empty).ToListAsync()).Where(rp => !idsBefore.Contains(rp.Id)).ToList();
        Assert.Equal(templateIds.Values.OrderBy(id => id), added.Select(rp => rp.PermissionId).OrderBy(id => id));
        Assert.All(added, rp =>
        {
            Assert.Equal(adminRole.Id, rp.RoleId);
            Assert.Equal(tenantId, rp.TenantId);
            Assert.Equal(GrantSource.Module, rp.GrantSource);
            Assert.Equal("tasks", rp.SourceModuleCode);
            Assert.Equal(EntitlementActor, rp.AssignedBy);
        });

        // Nothing else changed anywhere in rolePermissions: every earlier row is still there, byte-identical.
        var rawAfter = await RawGrantsAsync(_host.Database);
        Assert.Equal(rawBefore.Count + 2, rawAfter.Count);
        Assert.All(rawBefore, row => Assert.Contains(row, rawAfter));

        var viewerAfter = await KeysAsync(rolePermissions, viewerRole.Id, tenantId);
        Assert.DoesNotContain(ChecklistTemplatesManage, viewerAfter);
        Assert.DoesNotContain(TemplatesManage, viewerAfter);

        // The keyed form (the consumer's normal input: the module's declared keys) adds nothing further and still gives
        // Viewer neither key.
        IReadOnlyCollection<EntitledModulePermissionKeys> tasksDeclared =
            [new("TASKS", [TasksReadKey, TasksCreateKey, ChecklistTemplatesManage, TemplatesManage])];
        await sync.SyncTenantModulesWithKeysAsync(tenantId, tasksDeclared, EntitlementActor, CancellationToken.None);

        Assert.Equal(rawAfter, await RawGrantsAsync(_host.Database));
        var viewerKeyed = await KeysAsync(rolePermissions, viewerRole.Id, tenantId);
        Assert.DoesNotContain(ChecklistTemplatesManage, viewerKeyed);
        Assert.DoesNotContain(TemplatesManage, viewerKeyed);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────

    private async Task<IMongoDatabase> FreshDatabaseAsync(string name)
    {
        // The host's own client (same Guid representation as production writes), on the host's own test-owned runner.
        var client = _host.Database.Client;
        await client.DropDatabaseAsync(name);
        return client.GetDatabase(name);
    }

    private async Task<Dictionary<string, Guid>> EnsureTemplateRowsAsync(PermissionScope scope)
    {
        var permissions = _host.Database.GetCollection<Permission>("permissions");
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var key in new[] { ChecklistTemplatesManage, TemplatesManage })
        {
            ids[key] = await EnsureCatalogRowAsync(permissions, key, scope);
        }

        return ids;
    }

    // Insert the catalog-created row, or reset an existing one's Scope (the host database is shared by this class's tests).
    private static async Task<Guid> EnsureCatalogRowAsync(IMongoCollection<Permission> permissions, string key, PermissionScope scope)
    {
        var existing = await permissions.Find(p => p.Key == key).FirstOrDefaultAsync();
        if (existing is null)
        {
            var row = CatalogRow(key, scope);
            await permissions.InsertOneAsync(row);
            return row.Id;
        }

        await permissions.UpdateOneAsync(p => p.Id == existing.Id, Builders<Permission>.Update.Set(p => p.Scope, scope));
        return existing.Id;
    }

    private static async Task<HashSet<string>> KeysAsync(IRolePermissionRepository rolePermissions, Guid roleId, Guid tenantId) =>
        (await rolePermissions.GetPermissionsByRoleAsync(roleId, tenantId, CancellationToken.None)).ToHashSet(StringComparer.Ordinal);

    private static Permission CatalogRow(string key, PermissionScope scope)
    {
        var segments = key.Split('.');
        var permission = new Permission(
            segments[0],
            string.Join('.', segments[1..^1]),
            segments[^1],
            key,
            description: null,
            moduleOverride: segments[1],
            scope: scope);
        permission.MarkAsUserDefined();
        return permission;
    }

    private static Task<List<BsonDocument>> RawByKeysAsync(IMongoDatabase database, string[] keys) =>
        database.GetCollection<BsonDocument>("permissions")
            .Find(Builders<BsonDocument>.Filter.In("Key", keys))
            .Sort(Builders<BsonDocument>.Sort.Ascending("Key"))
            .ToListAsync();

    private static Task<List<BsonDocument>> RawExceptKeysAsync(IMongoDatabase database, string[] keys) =>
        database.GetCollection<BsonDocument>("permissions")
            .Find(Builders<BsonDocument>.Filter.Nin("Key", keys))
            .Sort(Builders<BsonDocument>.Sort.Ascending("Key"))
            .ToListAsync();

    private static Task<List<BsonDocument>> RawGrantsAsync(IMongoDatabase database) =>
        database.GetCollection<BsonDocument>("rolePermissions")
            .Find(FilterDefinition<BsonDocument>.Empty)
            .Sort(Builders<BsonDocument>.Sort.Ascending("_id"))
            .ToListAsync();

    private static List<BsonDocument> WithoutCorrectionFields(IEnumerable<BsonDocument> documents) =>
        documents.Select(document =>
        {
            var copy = document.DeepClone().AsBsonDocument;
            copy.Remove("Scope");
            copy.Remove("UpdatedAt");
            copy.Remove("UpdatedBy");
            return copy;
        }).ToList();

    private sealed record RolesAdmin(Guid TenantId, Guid UserId, string Token);

    private static readonly string[] RolesAdminPermissionKeys = ["auth.roles.create", "auth.roles.read", "auth.roles.assign-permission"];

    private static readonly ConditionalWeakTable<AccountKindAcceptance.AuthTestHost, Task<RolesAdmin>> RolesAdminCache = new();

    private Task<RolesAdmin> SeedRolesAdminAsync() => RolesAdminCache.GetValue(_host, static h => SeedRolesAdminCoreAsync(h));

    private static async Task<RolesAdmin> SeedRolesAdminCoreAsync(AccountKindAcceptance.AuthTestHost host)
    {
        var tenantId = host.Seeded.TenantId;
        var stamp = Guid.NewGuid().ToString("N")[..8];

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
            new Role($"bl411-roles-admin-{stamp}", "BL-411 Roles Admin (disposable)", "acceptance fixture", tenantId), CancellationToken.None);

        foreach (var key in RolesAdminPermissionKeys)
        {
            var permission = await permissions.GetByKeyAsync(key, CancellationToken.None)
                ?? throw new InvalidOperationException($"{key} is not in the seeded catalog.");
            await rolePermissions.AssignAsync(
                RolePermission.ManualGrant(role.Id, permission.Id, tenantId, AccountKindAcceptance.SeedActor), CancellationToken.None);
        }

        var user = new User($"bl411-roles-admin.{stamp}@acceptance.invalid", hasher.Hash(AccountKindAcceptance.DisposablePassword), "Roles", "Admin", tenantId);
        user.ConfirmEmail();
        var created = await users.CreateAsync(user, CancellationToken.None);
        await userRoles.AssignAsync(new UserRole(created.Id, role.Id, tenantId, AccountKindAcceptance.SeedActor), CancellationToken.None);

        var permissionKeys = (await rolePermissions.GetPermissionsByRoleAsync(role.Id, tenantId, CancellationToken.None)).ToArray();
        var token = tokens.GenerateAccessToken(created, new[] { role.Name }, permissionKeys, expiresInMinutes: 60);

        return new RolesAdmin(tenantId, created.Id, token);
    }

    private async Task<Guid> CreateRoleAsync(HttpClient client, Guid tenantId)
    {
        var name = $"bl411-template-editors-{Guid.NewGuid().ToString("N")[..8]}";
        var response = await client.PostAsJsonAsync("api/roles", new { name, displayName = "BL-411 Template Editors", description = "acceptance" });
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync());

        using var scope = _host.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
        var role = await scope.ServiceProvider.GetRequiredService<IRoleRepository>().GetByNameAndTenantAsync(name, tenantId, CancellationToken.None)
            ?? throw new InvalidOperationException($"POST api/roles answered 201 but role '{name}' was not persisted.");
        return role.Id;
    }
}
