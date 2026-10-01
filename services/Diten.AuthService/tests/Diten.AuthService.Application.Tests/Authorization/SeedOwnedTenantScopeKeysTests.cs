using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// Owner + CT decision 2026-10-01 — the seed-sourced exception to the catalog sync's "most restrictive wins" rule is
/// measured against the PRODUCTION seed (<see cref="DataSeeder.BuildCanonicalPermissions"/>), not against a copy of
/// the list. If the list grows, a listed key stops being Tenant in the seed, or another Workflow key becomes Tenant,
/// this goes red. The sync's own behaviour is pinned in <c>InternalPermissionsControllerTests</c>.
/// </summary>
public sealed class SeedOwnedTenantScopeKeysTests
{
    private const string WorkflowPrefix = "platform.workflow.";

    private static readonly string[] TheThreeKeys =
    [
        "platform.workflow.instances.start",
        "platform.workflow.tasks.approve",
        "platform.workflow.tasks.reject"
    ];

    [Fact]
    public void The_list_is_exactly_the_three_workflow_execution_keys()
    {
        Assert.Equal(TheThreeKeys, SeedOwnedTenantScopeKeys.Keys.OrderBy(key => key, StringComparer.Ordinal));
    }

    [Fact]
    public void Each_listed_key_is_a_Tenant_system_row_in_the_production_seed()
    {
        var seeded = DataSeeder.BuildCanonicalPermissions();

        foreach (var key in SeedOwnedTenantScopeKeys.Keys)
        {
            var literal = Assert.Single(seeded, permission => permission.Key == key);
            Assert.Equal(PermissionScope.Tenant, literal.Scope);
            Assert.True(literal.IsSystem);
            Assert.True(SeedOwnedTenantScopeKeys.KeepsTenantScope(literal));
        }
    }

    [Fact]
    public void Of_the_thirteen_seeded_workflow_keys_only_the_listed_three_are_Tenant()
    {
        var workflow = DataSeeder.BuildCanonicalPermissions()
            .Where(permission => permission.Key.StartsWith(WorkflowPrefix, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(13, workflow.Count);
        Assert.Equal(
            TheThreeKeys,
            workflow.Where(permission => permission.Scope == PermissionScope.Tenant)
                .Select(permission => permission.Key)
                .OrderBy(key => key, StringComparer.Ordinal));
        Assert.All(
            workflow.Where(permission => !SeedOwnedTenantScopeKeys.Keys.Contains(permission.Key)),
            permission =>
            {
                Assert.Equal(PermissionScope.PlatformAdmin, permission.Scope);
                Assert.False(SeedOwnedTenantScopeKeys.KeepsTenantScope(permission));
            });
    }

    [Theory]
    [MemberData(nameof(ListedKeys))]
    public void A_listed_key_on_a_catalog_created_row_does_not_qualify(string key)
    {
        var row = Row(key, PermissionScope.Tenant);
        row.MarkAsUserDefined();

        Assert.False(SeedOwnedTenantScopeKeys.KeepsTenantScope(row));
    }

    [Theory]
    [MemberData(nameof(ListedKeys))]
    public void A_listed_key_on_a_PlatformAdmin_row_does_not_qualify(string key)
    {
        // One direction only: the exception keeps a Tenant row Tenant; it never lowers a PlatformAdmin row.
        Assert.False(SeedOwnedTenantScopeKeys.KeepsTenantScope(Row(key, PermissionScope.PlatformAdmin)));
    }

    [Theory]
    [InlineData("platform.workflow.tasks.delegate")]
    [InlineData("platform.workflow.instances.view")]
    [InlineData("platform.tenants.quotas.manage")]
    public void An_unlisted_Tenant_system_row_does_not_qualify(string key)
    {
        Assert.False(SeedOwnedTenantScopeKeys.KeepsTenantScope(Row(key, PermissionScope.Tenant)));
    }

    [Fact]
    public void The_seed_sourced_list_and_the_route_sourced_allowlist_do_not_overlap()
    {
        Assert.DoesNotContain(
            TenantRouteScopeCorrections.Allowlist,
            entry => SeedOwnedTenantScopeKeys.Keys.Contains(entry.PermissionKey));
    }

    public static TheoryData<string> ListedKeys()
    {
        var data = new TheoryData<string>();
        foreach (var key in SeedOwnedTenantScopeKeys.Keys.OrderBy(key => key, StringComparer.Ordinal))
        {
            data.Add(key);
        }

        return data;
    }

    private static Permission Row(string key, PermissionScope scope)
    {
        var segments = key.Split('.');
        return new Permission(
            segments[0],
            string.Join('.', segments[1..^1]),
            segments[^1],
            key,
            description: null,
            moduleOverride: segments[1],
            scope: scope);
    }
}
