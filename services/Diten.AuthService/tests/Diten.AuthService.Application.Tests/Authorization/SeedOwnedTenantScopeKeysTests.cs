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

    // WP-LANE-MAIN-SYNC-02 A1 (owner decision 2026-10-14): main's WP-0023-FX made four more workflow runtime keys Tenant
    // (TenantRouteScopeCorrections, TenantApiPermission). The Tenant workflow keys are now EXACTLY these seven, by name —
    // a name list, not a ceiling: an eighth Tenant key, or one of these back to PlatformAdmin, is red.
    private static readonly string[] TheSevenTenantWorkflowKeys =
    [
        "platform.workflow.instances.start",
        "platform.workflow.instances.view",
        "platform.workflow.tasks.approve",
        "platform.workflow.tasks.cancel",
        "platform.workflow.tasks.delegate",
        "platform.workflow.tasks.reject",
        "platform.workflow.tasks.request-info"
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
    public void Of_the_thirteen_seeded_workflow_keys_exactly_the_named_seven_are_Tenant()
    {
        var workflow = DataSeeder.BuildCanonicalPermissions()
            .Where(permission => permission.Key.StartsWith(WorkflowPrefix, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(13, workflow.Count);
        // A1 — the seven by name (was: only the three seed-owned ones).
        Assert.Equal(
            TheSevenTenantWorkflowKeys,
            workflow.Where(permission => permission.Scope == PermissionScope.Tenant)
                .Select(permission => permission.Key)
                .OrderBy(key => key, StringComparer.Ordinal));
        // The four beyond the seed-owned three are Tenant only through the allowlisted, audited WP-0023-FX correction.
        Assert.All(
            TheSevenTenantWorkflowKeys.Except(TheThreeKeys),
            key => Assert.Contains(TenantRouteScopeCorrections.Allowlist, entry => entry.PermissionKey == key));
        Assert.All(
            workflow.Where(permission => !TheSevenTenantWorkflowKeys.Contains(permission.Key)),
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

    // ── CT acceptance (2026-10-01, from the independent security review) ────────────────────────────────────────────

    /// <summary>The list cannot be widened at run time: a public HashSet behind IReadOnlySet could be downcast and added to.</summary>
    [Fact]
    public void The_list_cannot_be_added_to_by_code_that_holds_a_reference_to_it()
    {
        Assert.False(SeedOwnedTenantScopeKeys.Keys is HashSet<string>);
        Assert.Throws<NotSupportedException>(
            () => ((ICollection<string>)SeedOwnedTenantScopeKeys.Keys).Add("platform.workflow.tasks.delegate"));
        Assert.Equal(3, SeedOwnedTenantScopeKeys.Keys.Count);
    }

    /// <summary>
    /// The exception holds only for a SYSTEM row. A listed key on a catalog-created row is the one state in which the
    /// decision silently does not hold; it is named, so the seeder can report it. Deleted rows and unlisted keys are not.
    /// </summary>
    [Fact]
    public void A_listed_key_on_a_row_that_is_not_a_system_row_is_named_and_nothing_else_is()
    {
        var systemApprove = Row("platform.workflow.tasks.approve", PermissionScope.Tenant);
        var catalogCreatedReject = Row("platform.workflow.tasks.reject", PermissionScope.Tenant);
        catalogCreatedReject.MarkAsUserDefined();
        var catalogCreatedUnlisted = Row("platform.workflow.tasks.delegate", PermissionScope.PlatformAdmin);
        catalogCreatedUnlisted.MarkAsUserDefined();
        var deletedCatalogCreatedStart = Row("platform.workflow.instances.start", PermissionScope.Tenant);
        deletedCatalogCreatedStart.MarkAsUserDefined();
        deletedCatalogCreatedStart.IsDeleted = true;

        var named = SeedOwnedTenantScopeKeys.RowsTheExceptionCannotHold(
            [systemApprove, catalogCreatedReject, catalogCreatedUnlisted, deletedCatalogCreatedStart]);

        Assert.Equal(["platform.workflow.tasks.reject"], named);
        Assert.Empty(SeedOwnedTenantScopeKeys.RowsTheExceptionCannotHold([systemApprove]));
    }

    // A1 — WP-0023-FX's allowlist names all seven runtime keys, so it now holds the three seed-owned ones too. The overlap
    // is EXACTLY those three (by name); any other seed-owned key on the route allowlist is still red.
    [Fact]
    public void The_seed_sourced_list_and_the_route_sourced_allowlist_overlap_only_in_the_three_named_keys()
    {
        Assert.Equal(
            TheThreeKeys,
            TenantRouteScopeCorrections.Allowlist
                .Where(entry => SeedOwnedTenantScopeKeys.Keys.Contains(entry.PermissionKey))
                .Select(entry => entry.PermissionKey)
                .OrderBy(key => key, StringComparer.Ordinal));
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
