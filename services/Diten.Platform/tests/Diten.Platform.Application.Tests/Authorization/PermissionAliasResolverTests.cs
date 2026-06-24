using Diten.Platform.API.Security;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class PermissionAliasResolverTests
{
    [Fact]
    public void Expand_canonical_with_alias_returns_canonical_plus_alias()
    {
        var result = PermissionAliasResolver.Expand("platform.administrators.read");

        Assert.Equal(
            new HashSet<string> { "platform.administrators.read", "Platform.Administrators.Read" },
            result);
    }

    [Fact]
    public void Expand_canonical_with_no_alias_returns_only_itself()
    {
        // auth.users.create is canonical but not in the Platform alias map.
        var result = PermissionAliasResolver.Expand("auth.users.create");

        Assert.Equal(new HashSet<string> { "auth.users.create" }, result);
    }

    [Fact]
    public void Expand_unknown_key_is_fail_closed_returns_only_itself()
    {
        var result = PermissionAliasResolver.Expand("unknown.resource.action");

        Assert.Equal(new HashSet<string> { "unknown.resource.action" }, result);
    }

    [Fact]
    public void Expand_legacy_requirement_is_not_upgraded_to_canonical()
    {
        // A legacy-spelled requirement must NOT pull in the canonical key (no auto-upgrade, no case-folding match).
        var result = PermissionAliasResolver.Expand("Platform.Administrators.Read");

        Assert.Equal(new HashSet<string> { "Platform.Administrators.Read" }, result);
        Assert.DoesNotContain("platform.administrators.read", result);
    }

    [Fact]
    public void Expand_verb_alias_canonical_returns_view_alias()
    {
        var result = PermissionAliasResolver.Expand("platform.tenants.quotas.read");

        Assert.Equal(
            new HashSet<string> { "platform.tenants.quotas.read", "platform.tenants.quotas.view" },
            result);
    }

    [Theory]
    [InlineData("platform.document-management.collection-definitions.list", "MOD0028.COLLECTION_DEFINITION.LIST")]
    [InlineData("platform.document-management.collection-definitions.view", "MOD0028.COLLECTION_DEFINITION.VIEW")]
    [InlineData("platform.document-management.baseline-releases.list", "MOD0028.BASELINE_RELEASE.LIST")]
    [InlineData("platform.document-management.corporate-root.initialize", "MOD0028.CORPORATE_ROOT.INITIALIZE")]
    [InlineData("platform.document-management.collection-instances.view", "MOD0028.COLLECTION_INSTANCE.VIEW")]
    public void Expand_document_management_canonical_returns_approved_spec_alias(
        string canonical,
        string specAlias)
    {
        var result = PermissionAliasResolver.Expand(canonical);

        Assert.Equal(new HashSet<string> { canonical, specAlias }, result);
        Assert.Equal(new HashSet<string> { specAlias }, PermissionAliasResolver.Expand(specAlias));
    }

    [Fact]
    public void Expand_qms_baselines_publish_returns_approved_spec_alias_directionally()
    {
        const string canonical = "platform.document-management.qms-baselines.publish";
        const string specAlias = "MOD0028.BASELINE_RELEASE.PUBLISH";

        Assert.Equal(new HashSet<string> { canonical, specAlias }, PermissionAliasResolver.Expand(canonical));
        // Reverse is not granted: the legacy spec key satisfies only itself.
        Assert.Equal(new HashSet<string> { specAlias }, PermissionAliasResolver.Expand(specAlias));
    }

    [Theory]
    [InlineData("platform.document-management.qms-baselines.import")]
    [InlineData("platform.document-management.qms-baselines.view")]
    public void Qms_baselines_import_and_view_have_no_alias_pending_ea_decision(string canonical)
    {
        // FU02-native: no parent spec alias is registered until EA/MOD-0018 confirms a directional mapping.
        Assert.Equal(new HashSet<string> { canonical }, PermissionAliasResolver.Expand(canonical));
        Assert.False(PermissionAliasMap.CanonicalToAliases.ContainsKey(canonical));
    }

    [Fact]
    public void Expand_always_contains_the_input()
    {
        foreach (var canonical in PermissionAliasMap.CanonicalToAliases.Keys)
        {
            Assert.Contains(canonical, PermissionAliasResolver.Expand(canonical));
        }
    }

    // ── Structural invariants of the map ──

    [Fact]
    public void Map_has_the_expected_platform_enforced_entry_count()
    {
        // §1.2 (32) + §1.1 Platform-owned (20) + §1.3 verb aliases (3) + MOD-0028-FU01 (5) + MOD-0028-FU02 (1) = 61.
        Assert.Equal(61, PermissionAliasMap.CanonicalToAliases.Count);
    }

    [Fact]
    public void Every_alias_is_bound_to_exactly_one_canonical_no_duplicates()
    {
        var aliasOccurrences = PermissionAliasMap.CanonicalToAliases.Values
            .SelectMany(aliases => aliases)
            .GroupBy(alias => alias, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Assert.Empty(aliasOccurrences);
    }

    [Fact]
    public void No_alias_is_itself_a_canonical_key_no_alias_chains()
    {
        var canonicalKeys = PermissionAliasMap.CanonicalToAliases.Keys.ToHashSet(StringComparer.Ordinal);
        var allAliases = PermissionAliasMap.CanonicalToAliases.Values.SelectMany(a => a);

        Assert.DoesNotContain(allAliases, alias => canonicalKeys.Contains(alias));
    }

    [Fact]
    public void No_canonical_key_appears_as_an_alias_directional_only()
    {
        var allAliases = PermissionAliasMap.CanonicalToAliases.Values
            .SelectMany(a => a)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain(PermissionAliasMap.CanonicalToAliases.Keys, canonical => allAliases.Contains(canonical));
    }

    [Fact]
    public void Every_canonical_key_is_lowercase_dotted_pks001_shape()
    {
        foreach (var canonical in PermissionAliasMap.CanonicalToAliases.Keys)
        {
            Assert.Equal(canonical.ToLowerInvariant(), canonical);
            Assert.True(canonical.Split('.', StringSplitOptions.RemoveEmptyEntries).Length >= 3, canonical);
        }
    }
}
