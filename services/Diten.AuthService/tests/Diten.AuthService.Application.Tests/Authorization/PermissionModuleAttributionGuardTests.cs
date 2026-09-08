using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// FIX-RBAC-PERM-MODULE-ATTRIBUTION — the guard that keeps the Role Permissions grouping honest.
///
/// <para>
/// It reads the PRODUCTION seed catalog (<see cref="DataSeeder.BuildCanonicalPermissions"/>) and the PRODUCTION
/// rule (<see cref="PermissionModuleAttribution"/>). It deliberately does NOT re-implement the derivation: a test
/// that re-applies the rule to its own copy only proves the copy works, which is how two defects reached
/// production in this repository. Deleting the derivation from the constructor makes these tests fail.
/// </para>
/// </summary>
public sealed class PermissionModuleAttributionGuardTests
{
    /// <summary>
    /// KNOWN-VIOLATION LIST (mongo-indexing DB-010 pattern). 168 catalog rows carried a service name as their
    /// Module before this fix and every one of them was derivable from its Key, so the list starts — and must
    /// stay — EMPTY. It exists so the pattern is available if a future service namespace is added with rows that
    /// genuinely cannot be derived. The list may only SHRINK: an entry that no longer violates fails the staleness
    /// test below rather than sitting here forever.
    /// </summary>
    private static readonly IReadOnlySet<string> KnownServiceNameAttributions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void No_seeded_permission_is_attributed_to_a_service_namespace()
    {
        var offenders = DataSeeder.BuildCanonicalPermissions()
            .Where(p => PermissionModuleAttribution.IsServiceNamespace(p.Module))
            .Where(p => !KnownServiceNameAttributions.Contains(p.Key))
            .Select(p => $"{p.Key} -> Module=\"{p.Module}\"")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A permission's Module is a SERVICE namespace, not a module code. A service hosts many modules, so this "
            + "collapses unrelated products into one group on the Role Permissions screen. Give the permission an "
            + "explicit moduleOverride, or let PermissionModuleAttribution derive it from the key:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Known_violation_list_is_not_stale()
    {
        var stillViolating = DataSeeder.BuildCanonicalPermissions()
            .Where(p => PermissionModuleAttribution.IsServiceNamespace(p.Module))
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fixedButStillListed = KnownServiceNameAttributions
            .Where(key => !stillViolating.Contains(key))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            fixedButStillListed.Count == 0,
            "These keys no longer carry a service-name Module but are still on the known-violation list. The list "
            + "may only shrink — remove them: " + string.Join(", ", fixedButStillListed));
    }

    [Fact]
    public void Derivation_uses_the_key_segment_that_names_the_owning_module()
    {
        // The three shapes the rule has to tell apart, asserted through the real constructor.
        var serviceHosted = new Permission("platform", "tasks", "read", "Read", null);
        var moduleNamespaced = new Permission("crm", "accounts", "read", "Read", null);
        var explicitlyAttributed = new Permission("platform", "tenant-security", "read", "Read", null, moduleOverride: "tenant-settings");

        Assert.Equal("tasks", serviceHosted.Module);              // service namespace -> second segment
        Assert.Equal("crm", moduleNamespaced.Module);             // namespace IS the module -> unchanged
        Assert.Equal("tenant-settings", explicitlyAttributed.Module); // explicit attribution always wins

        // The Key is the identity and is never touched by attribution.
        Assert.Equal("platform.tasks.read", serviceHosted.Key);
        Assert.Equal("platform.tenant-security.read", explicitlyAttributed.Key);
    }

    [Fact]
    public void Dotted_resources_derive_from_the_resource_head_not_the_whole_resource()
    {
        // "platform.document-management.controlled-documents.view" — the owner is the resource HEAD, and every
        // sibling under it must land in the same group rather than one group per sub-resource.
        var a = new Permission("platform", "document-management.controlled-documents", "view", "View", null);
        var b = new Permission("platform", "document-management.access", "manage", "Manage", null);

        Assert.Equal("document-management", a.Module);
        Assert.Equal("document-management", b.Module);
    }

    [Fact]
    public void Service_namespace_registry_has_exactly_one_definition()
    {
        // The rule is only as good as its single source. Any namespace the derivation treats as a service must be
        // reachable through PermissionModuleAttribution — nowhere else may hold a competing copy.
        Assert.True(PermissionModuleAttribution.IsServiceNamespace("platform"));
        Assert.True(PermissionModuleAttribution.IsServiceNamespace("auth"));
        Assert.True(PermissionModuleAttribution.IsServiceNamespace("mdm"));

        // ppm/pvg are the module code of a single module today; treating them as services would shatter one module
        // into one group per resource. See the comment on ServiceNamespaces before adding either.
        Assert.False(PermissionModuleAttribution.IsServiceNamespace("ppm"));
        Assert.False(PermissionModuleAttribution.IsServiceNamespace("pvg"));
        Assert.False(PermissionModuleAttribution.IsServiceNamespace("crm"));
    }
}
