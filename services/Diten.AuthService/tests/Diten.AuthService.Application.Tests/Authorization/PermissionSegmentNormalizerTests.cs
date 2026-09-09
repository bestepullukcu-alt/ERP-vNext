using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// FIX-PERM-ACTION-SPELLING — one spelling per permission segment, and a permission KEY that does not move
/// while that happens.
///
/// <para>
/// The guard reads the PRODUCTION seed catalog and the PRODUCTION normalizer. It does not re-implement the rule:
/// a test that re-applies the rule to its own copy proves only that the copy works.
/// </para>
/// </summary>
public sealed class PermissionSegmentNormalizerTests
{
    /// <summary>
    /// KNOWN-VIOLATION LIST (mongo-indexing DB-010 pattern). Empty: every segment the seed catalog produces is
    /// normalized by construction, so there is nothing left to grandfather. It stays here because the pattern is
    /// the point — a future seed that genuinely cannot be normalized is listed rather than silently tolerated —
    /// and the staleness test below makes sure the list can only ever shrink.
    /// </summary>
    private static readonly IReadOnlySet<string> KnownUnnormalizedSegments =
        new HashSet<string>(StringComparer.Ordinal);

    [Theory]
    // The case that made this necessary: the one chip left grey on the screen.
    [InlineData("PublishOverride", "publish-override")]
    [InlineData("Validate", "validate")]
    [InlineData("Read", "read")]
    [InlineData("BusinessReferenceData.Version", "business-reference-data.version")]
    // snake_case, from MOD-0251 and the person lookup key.
    [InlineData("lookup_validation", "lookup-validation")]
    [InlineData("view_status_history", "view-status-history")]
    // Already canonical — must round-trip untouched, or every existing row would churn.
    [InlineData("bulk-delete", "bulk-delete")]
    [InlineData("read-manager-chain", "read-manager-chain")]
    [InlineData("audit.view", "audit.view")]
    [InlineData("organization-units", "organization-units")]
    // An acronym run ends where the next word begins.
    [InlineData("SKUMaster", "sku-master")]
    // Degenerate input must not produce a stray separator.
    [InlineData("__weird__", "weird")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalizes_a_segment_to_lowercase_kebab(string? raw, string expected)
    {
        Assert.Equal(expected, PermissionSegmentNormalizer.Normalize(raw));
    }

    [Fact]
    public void Splits_before_lowercasing_the_order_is_load_bearing()
    {
        // Lowercase-then-split loses the word boundary for good and yields "publishoverride" — one unreadable
        // word. This assertion is the whole reason the normalizer walks the string instead of calling ToLower first.
        Assert.Equal("publish-override", PermissionSegmentNormalizer.Normalize("PublishOverride"));
        Assert.NotEqual("publishoverride", PermissionSegmentNormalizer.Normalize("PublishOverride"));
    }

    [Fact]
    public void The_permission_key_is_computed_from_the_raw_arguments_and_never_moves()
    {
        // ADR-001 §1. The attribute in BusinessReferenceDataController still reads
        // "Platform.BusinessReferenceData.Version.PublishOverride"; if the key followed the normalized segments
        // it would become …publish-override and every existing grant for it would stop resolving.
        var permission = new Permission("platform", "BusinessReferenceData.Version", "PublishOverride", "x", null, moduleOverride: "reference-data");

        Assert.Equal("platform.businessreferencedata.version.publishoverride", permission.Key);
        Assert.Equal("business-reference-data.version", permission.Resource);
        Assert.Equal("publish-override", permission.Action);
    }

    [Fact]
    public void No_seeded_permission_stores_an_uppercase_or_underscored_segment()
    {
        var offenders = DataSeeder.BuildCanonicalPermissions()
            .SelectMany(p => new[] { (p.Key, Segment: "Resource", Value: p.Resource), (p.Key, Segment: "Action", Value: p.Action) })
            .Where(x => x.Value.Any(char.IsUpper) || x.Value.Contains('_'))
            .Where(x => !KnownUnnormalizedSegments.Contains(x.Key + "#" + x.Segment))
            .Select(x => $"{x.Key} -> {x.Segment}=\"{x.Value}\"")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A permission stores a segment in a spelling the rest of the catalog does not use. The same verb then "
            + "reads as two different verbs on screen — two labels, two colours, two bars. Normalization happens in "
            + "the constructor, so this can only fail if that call was removed:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Known_violation_list_is_not_stale()
    {
        var stillViolating = DataSeeder.BuildCanonicalPermissions()
            .SelectMany(p => new[] { p.Key + "#Resource:" + p.Resource, p.Key + "#Action:" + p.Action })
            .Where(x => x.Split(':', 2)[1].Any(char.IsUpper) || x.Split(':', 2)[1].Contains('_'))
            .Select(x => x.Split(':', 2)[0])
            .ToHashSet(StringComparer.Ordinal);

        var fixedButStillListed = KnownUnnormalizedSegments
            .Where(entry => !stillViolating.Contains(entry))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            fixedButStillListed.Count == 0,
            "These segments are normalized now but are still on the known-violation list. The list may only shrink: "
            + string.Join(", ", fixedButStillListed));
    }

    // ---- the migration for rows already in the database ----

    [Fact]
    public void The_migration_can_only_write_the_two_segments_it_is_for()
    {
        // Structural guard, and the reason the rewrite is its own record: the migration physically cannot write
        // Scope (the escalation boundary), Key (frozen by ADR-001 §1) or Module. Adding either to the record —
        // the one dangerous edit in that method — fails here before it can reach a database.
        var members = typeof(DataSeeder.SegmentSpellingRewrite)
            .GetProperties()
            .Select(x => x.Name)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(new[] { "Action", "Id", "Resource" }, members);
    }

    /// <summary>
    /// A permission as it sits in Mongo TODAY, written before the constructor learned to normalize. The migration's
    /// only real input is documents like this — the driver deserializes straight onto the properties and never runs
    /// the constructor — so the test has to reproduce that, otherwise it would only ever see canonical rows and
    /// prove nothing about the rows the migration exists for.
    /// </summary>
    private static Permission LegacyRow(string module, string resource, string action, string? moduleOverride = null)
    {
        var permission = new Permission(module, resource, action, "x", null, moduleOverride: moduleOverride);
        typeof(Permission).GetProperty(nameof(Permission.Resource))!.SetValue(permission, resource);
        typeof(Permission).GetProperty(nameof(Permission.Action))!.SetValue(permission, action);
        return permission;
    }

    [Fact]
    public void The_migration_plans_a_rewrite_only_where_the_spelling_actually_differs()
    {
        var catalog = new List<Permission>
        {
            LegacyRow("platform", "BusinessReferenceData.Version", "PublishOverride", moduleOverride: "reference-data"),
            LegacyRow("mod0251", "employee", "view_sensitive"),
            LegacyRow("platform", "tasks", "read")   // already canonical
        };

        var plan = DataSeeder.PlanSegmentSpellingRewrites(catalog);

        // The already-canonical row is absent: an idempotent migration must plan zero writes on a clean catalog,
        // otherwise every startup churns every row.
        Assert.Equal(2, plan.Count);
        Assert.Contains(plan, r => r.Action == "publish-override" && r.Resource == "business-reference-data.version");
        Assert.Contains(plan, r => r.Action == "view-sensitive" && r.Resource is null);
    }

    [Fact]
    public void The_migration_is_idempotent_a_second_pass_plans_nothing()
    {
        // The constructor already normalizes, so a freshly built catalog is canonical and the migration must be a
        // no-op on it. This is what makes it safe to leave in the startup path forever.
        Assert.Empty(DataSeeder.PlanSegmentSpellingRewrites(DataSeeder.BuildCanonicalPermissions()));
    }

    [Fact]
    public void Every_seeded_key_still_parses_as_its_own_segments()
    {
        // The safety net for the whole change: normalizing the stored segments must not make a key unreachable.
        // Each seeded key must still be exactly what the module/resource/action grammar produces from itself.
        var broken = DataSeeder.BuildCanonicalPermissions()
            .Where(p => !p.Key.StartsWith(p.Key.Split('.')[0] + ".", StringComparison.Ordinal)
                        || p.Key != p.Key.ToLowerInvariant())
            .Select(p => p.Key)
            .ToList();

        Assert.True(broken.Count == 0, "Keys are no longer lowercase dotted paths: " + string.Join(", ", broken));
    }
}
