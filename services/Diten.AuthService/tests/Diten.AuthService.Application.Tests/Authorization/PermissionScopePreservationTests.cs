using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// FIX-RBAC-PERM-MODULE-ATTRIBUTION — the boundary test.
///
/// <para>
/// <see cref="Permission.Module"/> is a GROUPING label; <see cref="PermissionScope"/> is the tenant/platform-admin
/// ESCALATION boundary (<c>DefaultRolePermissionTemplate</c>: platform permissions are never granted to tenant
/// roles). Re-attributing a permission from "platform" to the module that owns it must not move a single row
/// across that boundary — if it did, a platform-admin key would silently become assignable to a tenant role.
/// </para>
///
/// <para>
/// The expected values are NOT recomputed here. <c>permission-scope-baseline.csv</c> is a captured snapshot of the
/// LIVE catalog's Scope from before the re-attribution, so this compares the production seed against reality
/// rather than against a second copy of the rule. Both directions are checked: a shifted Scope fails, and a
/// baseline that no longer matches the catalog fails as stale.
/// </para>
/// </summary>
public sealed class PermissionScopePreservationTests
{
    [Fact]
    public void No_seeded_permission_changed_scope()
    {
        var baseline = LoadBaseline();
        var drifted = new List<string>();

        foreach (var permission in DataSeeder.BuildCanonicalPermissions())
        {
            if (!baseline.TryGetValue(permission.Key, out var expected))
            {
                continue; // covered by the staleness test below
            }

            if (permission.Scope != expected)
            {
                drifted.Add($"{permission.Key}: {expected} -> {permission.Scope} (Module=\"{permission.Module}\")");
            }
        }

        Assert.True(
            drifted.Count == 0,
            "PermissionScope moved for the following permission(s). Scope is the privilege-escalation boundary and "
            + "must survive module re-attribution unchanged — a Tenant scope on a platform-admin key makes it "
            + "assignable to tenant roles:" + Environment.NewLine + string.Join(Environment.NewLine, drifted));
    }

    [Fact]
    public void Baseline_covers_every_seeded_permission_and_nothing_else()
    {
        var baseline = LoadBaseline();
        var seeded = DataSeeder.BuildCanonicalPermissions()
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = seeded.Where(k => !baseline.ContainsKey(k)).OrderBy(x => x, StringComparer.Ordinal).ToList();
        var removed = baseline.Keys.Where(k => !seeded.Contains(k)).OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.True(
            missing.Count == 0 && removed.Count == 0,
            "permission-scope-baseline.csv no longer matches the seed catalog. A NEW permission must be added to the "
            + "baseline with the Scope it is meant to have (state it, do not let it be inferred); a REMOVED one must "
            + "be deleted from the baseline."
            + Environment.NewLine + "seeded but not in baseline: " + string.Join(", ", missing)
            + Environment.NewLine + "in baseline but no longer seeded: " + string.Join(", ", removed));
    }

    private static Dictionary<string, PermissionScope> LoadBaseline()
    {
        var map = new Dictionary<string, PermissionScope>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in File.ReadAllLines(GetBaselinePath()))
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith('#'))
            {
                continue;
            }

            var parts = text.Split(',', 2);
            map[parts[0].Trim()] = (PermissionScope)int.Parse(parts[1].Trim());
        }

        return map;
    }

    private static string GetBaselinePath()
    {
        var directory = Path.GetDirectoryName(typeof(PermissionScopePreservationTests).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to resolve the test assembly directory.");

        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "Authorization", "permission-scope-baseline.csv");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new FileNotFoundException("permission-scope-baseline.csv was not found above the test assembly.");
    }
}
