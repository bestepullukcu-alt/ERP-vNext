namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// FIX-RBAC-PERM-MODULE-ATTRIBUTION — the single place that answers "which MODULE owns this permission?"
/// when nobody said so explicitly.
///
/// <para>
/// A permission key is <c>{namespace}.{resource}.{action}</c>. Historically the key's first segment was taken
/// as the module. That is correct when the namespace IS a module (<c>crm.accounts.read</c> → <c>crm</c>), and
/// wrong when it is a SERVICE that hosts many modules: <c>platform</c> is a service, and 16 different products
/// (document-management, tenants, notifications, module-catalog, …) publish keys under it. Attributing them all
/// to "platform" collapsed 40% of the catalog into one unreadable box on the Role Permissions screen.
/// </para>
///
/// <para>
/// The rule: a SERVICE namespace is never a module code; for those keys the module is the SECOND key segment
/// (the resource head), which is exactly the module slug the key was minted under. Everything else keeps the
/// first segment. Derivation is a FALLBACK ONLY — an explicit attribution (a seed <c>moduleOverride</c> or a
/// self-registration manifest <c>ModuleCode</c>) always wins, so deliberate groupings
/// (<c>platform.tenant-security.*</c> → <c>tenant-settings</c>) are untouched.
/// </para>
///
/// <para>
/// ⚠ This decides GROUPING ONLY. The tenant/platform escalation boundary is <see cref="Diten.AuthService.Domain.Entities.PermissionScope"/>,
/// which is carried explicitly and is NOT recomputed from the derived module — see the <c>Permission</c> ctor.
/// </para>
/// </summary>
public static class PermissionModuleAttribution
{
    /// <summary>
    /// The umbrella namespaces that are SERVICES, not modules — the single source of this list.
    ///
    /// <para>
    /// Evidence-based, not speculative: these are the three namespaces the catalog actually mints keys under
    /// while the owning module is something else. <c>auth.*</c> and <c>mdm.*</c> are already fully re-attributed
    /// by manifests (access-governance, legal-entity, product-item-sku-master, brand-product-master); they stay
    /// listed so a NEW key minted under them cannot silently reintroduce the service-name attribution.
    /// </para>
    ///
    /// <para>
    /// Deliberately NOT listed: <c>ppm</c> and <c>pvg</c>. They are domain short codes that today serve as the
    /// real module code for a single coherent module — deriving there would SHATTER one module into one group
    /// per resource (portfolios, initiatives, …), which is the opposite of the fix. Add a namespace here only
    /// when it is shown to host more than one module.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> ServiceNamespaces =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "platform", "auth", "mdm" };

    /// <summary>True when <paramref name="value"/> is a service namespace and therefore not a valid module code.</summary>
    public static bool IsServiceNamespace(string? value)
        => !string.IsNullOrWhiteSpace(value) && ServiceNamespaces.Contains(value.Trim());

    /// <summary>
    /// Derives the owning module from a key's <paramref name="keyNamespace"/> (first segment) and
    /// <paramref name="resource"/> (everything between namespace and action, possibly dotted).
    /// Non-service namespaces are returned as-is; service namespaces yield the resource HEAD.
    /// Falls back to the namespace when the resource is empty, so this never returns an empty module.
    /// </summary>
    public static string Derive(string? keyNamespace, string? resource)
    {
        var ns = (keyNamespace ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsServiceNamespace(ns))
        {
            return ns;
        }

        var head = HeadSegment(resource);
        return head.Length > 0 ? head : ns;
    }

    /// <summary>
    /// Migration/guard form of <see cref="Derive"/>: takes a full <c>namespace.resource.action</c> key.
    /// Returns an empty string for a key with fewer than three segments (nothing safe can be derived).
    /// </summary>
    public static string DeriveFromKey(string? permissionKey)
    {
        var parts = (permissionKey ?? string.Empty).Trim().ToLowerInvariant()
            .Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            return string.Empty;
        }

        return Derive(parts[0], string.Join('.', parts[1..^1]));
    }

    private static string HeadSegment(string? resource)
    {
        var value = (resource ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0)
        {
            return string.Empty;
        }

        var dot = value.IndexOf('.');
        return dot < 0 ? value : value[..dot];
    }
}
