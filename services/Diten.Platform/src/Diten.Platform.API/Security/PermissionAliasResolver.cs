namespace Diten.Platform.API.Security;

/// <summary>
/// AG-STEP-004B Slice 1B — pure, static resolver over <see cref="PermissionAliasMap"/>.
///
/// <see cref="Expand"/> turns a canonical permission requirement into the set of claim values that satisfy it under
/// dual-read: the canonical key itself plus any legacy aliases mapped to it.
///
/// Fail-closed and non-widening:
/// <list type="bullet">
///   <item><description>A canonical key with no aliases expands to just itself.</description></item>
///   <item><description>An unknown / unmapped key expands to just itself.</description></item>
///   <item><description>A <b>legacy-spelled</b> requirement expands to just itself — it is <b>never</b> auto-upgraded
///   to its canonical key (the lookup is ordinal/case-sensitive, and aliases are never map keys).</description></item>
/// </list>
///
/// This type has no state, no I/O, and no dependency on ASP.NET — it is a deterministic function of the static map.
/// Wiring it into enforcement is a separate slice (Commit B); this type changes no authorization behavior on its own.
/// </summary>
public static class PermissionAliasResolver
{
    /// <summary>
    /// Returns <c>{ permission } ∪ aliases(permission)</c>. The returned set always contains the input and never any
    /// key not derived from <see cref="PermissionAliasMap"/>.
    /// </summary>
    public static IReadOnlySet<string> Expand(string permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        var expanded = new HashSet<string>(StringComparer.Ordinal) { permission };

        if (PermissionAliasMap.CanonicalToAliases.TryGetValue(permission, out var aliases))
        {
            expanded.UnionWith(aliases);
        }

        return expanded;
    }
}
