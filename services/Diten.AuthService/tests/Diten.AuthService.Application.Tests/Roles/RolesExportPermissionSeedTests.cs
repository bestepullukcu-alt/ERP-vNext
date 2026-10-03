using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Roles;

/// <summary>
/// WP-ROLES-CLOSE-01 — the new <c>auth.roles.export</c> key on the REAL seeder, against the test-owned mongod: it is in
/// the catalog as a seeded (system) Tenant key a tenant administrator can hand out, and the Admin template carries it
/// while Viewer does not. What the one-way backfill does with it — for this key and for <c>auth.users.export</c> — is
/// in <c>ExportGrantBackfillScenarioTests</c> (S-A … S-H).
/// </summary>
[Collection("AccountKindAcceptance")]
public sealed class RolesExportPermissionSeedTests : IClassFixture<AccountKindAcceptance.AuthTestHost>
{
    private readonly AccountKindAcceptance.AuthTestHost _host;

    public RolesExportPermissionSeedTests(AccountKindAcceptance.AuthTestHost host) => _host = host;

    private IMongoCollection<Permission> Permissions => _host.Database.GetCollection<Permission>("permissions");

    [Fact]
    public async Task The_export_key_is_a_seeded_tenant_key_the_Admin_template_grants_and_Viewer_does_not()
    {
        var export = await Permissions.Find(p => p.Key == RolesExportGrantBackfill.ExportKey).SingleAsync();

        Assert.True(export.IsSystem);
        Assert.Equal(PermissionScope.Tenant, export.Scope);
        Assert.Equal("access-governance", export.Module);
        Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(export)); // a tenant administrator can hand it out

        var catalog = await Permissions.Find(_ => true).ToListAsync();
        Assert.Contains(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.AdminRole, catalog), p => p.Key == export.Key);
        Assert.DoesNotContain(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.ViewerRole, catalog), p => p.Key == export.Key);
    }

    // What the key does NOT do, said where the key is defined: it draws the menu, it does not serve the rows.
    [Fact]
    public async Task The_keys_own_description_says_it_shows_the_menu_and_that_the_rows_are_read_with_the_read_key()
    {
        var export = await Permissions.Find(p => p.Key == RolesExportGrantBackfill.ExportKey).SingleAsync();

        Assert.Contains("menu", export.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(RolesExportGrantBackfill.ReadKey, export.Description, StringComparison.Ordinal);
    }
}
