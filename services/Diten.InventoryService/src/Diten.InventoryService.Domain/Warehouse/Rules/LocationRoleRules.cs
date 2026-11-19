using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Warehouse.Enums;

namespace Diten.InventoryService.Domain.Warehouse.Rules;

/// <summary>What is wrong with a set of storage-condition tags (pack §12: 400 <c>WMS_STORAGE_CONDITION_INVALID</c> for every case but None).</summary>
public enum ConditionTagProblem
{
    None,
    Unknown,
    Duplicate,
    MoreThanOneTemperature
}

/// <summary>What is wrong with a role set on its own node.</summary>
public enum RoleSetProblem
{
    None,
    Duplicate,

    /// <summary>Receiving and Staging on the same node (M-5) → 409 <c>WMS_LOCATION_ROLE_CONFLICT</c>.</summary>
    ReceivingWithStaging
}

/// <summary>The outcome of the storage-condition fit of a product on a target (pack §8.12).</summary>
public enum StorageConditionFit
{
    Fits,

    /// <summary>The product's conditions are unknown (field absent) → 422 <c>WMS_STORAGE_CONDITIONS_UNKNOWN</c>; never read as "none".</summary>
    Unknown,

    /// <summary>→ 422 <c>WMS_STORAGE_CONDITION_MISMATCH</c>.</summary>
    Mismatch
}

/// <summary>
/// The effective accepted set of a node (pack §8.12): the conditions of the NEAREST node on its LOCATION path (itself included) whose
/// record carries any; <see cref="SourceLocationId"/> is that node, <see cref="Inherited"/> = it is not the node itself ("üst düğümden").
/// </summary>
public sealed record EffectiveStorageConditions(IReadOnlyList<string> Codes, Guid SourceLocationId, bool Inherited);

/// <summary>One node of a LOCATION path with the role record it carries (none = empty roles and conditions).</summary>
public sealed record LocationRoleNode(Guid LocationId, IReadOnlyCollection<WarehouseRole> Roles, IReadOnlyList<string> AcceptedStorageConditions);

/// <summary>
/// The pure rules of the MOD-0178 location roles (pack §4.4, §8.12, §12; WMS-BUNDLE draft §3.5–§3.6). No I/O: the handler reads the
/// LOCATION path / subtree and the role records, these rules decide.
/// </summary>
public static class LocationRoleRules
{
    public const string Ambient = "AMBIENT_15_25";
    public const string Cool = "COOL_2_8";
    public const string Frozen = "FROZEN_BELOW_MINUS_20";
    public const string ProtectFromLight = "PROTECT_FROM_LIGHT";
    public const string ProtectFromMoisture = "PROTECT_FROM_MOISTURE";

    /// <summary>The five PRODUCT-MASTER v1.1 <c>StorageConditionCode</c> values a tag may carry (<c>product-master-bundle.openapi.yaml:601-604</c>).</summary>
    public static readonly IReadOnlyList<string> KnownStorageConditions = [Ambient, Cool, Frozen, ProtectFromLight, ProtectFromMoisture];

    /// <summary>The temperature codes — at most one per tag set (the PRODUCT-MASTER rule, <c>:681-682</c>).</summary>
    public static readonly IReadOnlyList<string> TemperatureConditions = [Ambient, Cool, Frozen];

    public const int MaxNoteLength = 500;

    /// <summary>Roles are given to a StorageLocation, Zone or Bin — never a Site or Warehouse (422 <c>WMS_LOCATION_TYPE_NOT_ALLOWED</c>).</summary>
    public static bool IsRoleLocationType(LocationType type) => type is LocationType.StorageLocation or LocationType.Zone or LocationType.Bin;

    public static ConditionTagProblem CheckConditionTags(IReadOnlyList<string> codes)
    {
        if (codes.Any(c => !KnownStorageConditions.Contains(c, StringComparer.Ordinal))) return ConditionTagProblem.Unknown;
        if (codes.Distinct(StringComparer.Ordinal).Count() != codes.Count) return ConditionTagProblem.Duplicate;
        return codes.Count(c => TemperatureConditions.Contains(c, StringComparer.Ordinal)) > 1 ? ConditionTagProblem.MoreThanOneTemperature : ConditionTagProblem.None;
    }

    public static RoleSetProblem CheckRoles(IReadOnlyList<WarehouseRole> roles)
    {
        if (roles.Distinct().Count() != roles.Count) return RoleSetProblem.Duplicate;
        return roles.Contains(WarehouseRole.Receiving) && roles.Contains(WarehouseRole.Staging) ? RoleSetProblem.ReceivingWithStaging : RoleSetProblem.None;
    }

    /// <summary>A record must say something: a role or an accepted condition (pack §4.4 "Kural").</summary>
    public static bool IsEmpty(IReadOnlyCollection<WarehouseRole> roles, IReadOnlyCollection<string> conditions) => roles.Count == 0 && conditions.Count == 0;

    /// <summary>The role that may not meet <paramref name="role"/> on one path (F-13): Receiving ⇄ Staging.</summary>
    public static WarehouseRole Opposite(WarehouseRole role) => role == WarehouseRole.Receiving ? WarehouseRole.Staging : WarehouseRole.Receiving;

    /// <summary>
    /// F-13: a node may not take a role whose opposite is held by an ancestor (inherited down to it) or by a node of its subtree (which it
    /// would inherit). Returns the first role of <paramref name="roles"/> that conflicts, or null → 409 <c>WMS_LOCATION_ROLE_INHERITED_CONFLICT</c>.
    /// </summary>
    public static WarehouseRole? InheritedConflict(
        IReadOnlyCollection<WarehouseRole> roles, IEnumerable<WarehouseRole> ancestorRoles, IEnumerable<WarehouseRole> descendantRoles)
    {
        var around = ancestorRoles.Concat(descendantRoles).ToHashSet();
        foreach (var role in roles.OrderBy(r => r))
        {
            if (around.Contains(Opposite(role))) return role;
        }

        return null;
    }

    /// <summary>
    /// The roles in force on a node: its own and every ancestor's (F-13 subtree inheritance). <paramref name="pathNearestFirst"/> is the node
    /// itself, then its parent … up to the root.
    /// </summary>
    public static IReadOnlySet<WarehouseRole> EffectiveRoles(IEnumerable<LocationRoleNode> pathNearestFirst)
        => pathNearestFirst.SelectMany(n => n.Roles).ToHashSet();

    /// <summary>
    /// Pack §8.12 "etkin kabul kümesi": the accepted conditions of the nearest node of <paramref name="pathNearestFirst"/> (the node itself
    /// first) whose record carries any; null = no tag on the path (an unlabelled place).
    /// </summary>
    public static EffectiveStorageConditions? EffectiveConditions(IReadOnlyList<LocationRoleNode> pathNearestFirst)
    {
        for (var i = 0; i < pathNearestFirst.Count; i++)
        {
            var node = pathNearestFirst[i];
            if (node.AcceptedStorageConditions.Count > 0)
            {
                return new EffectiveStorageConditions(node.AcceptedStorageConditions, node.LocationId, i > 0);
            }
        }

        return null;
    }

    /// <summary>
    /// Pack §8.12: every condition of the product must be in the effective set (an unknown, future code too — it is looked up like any
    /// other). An unlabelled place takes only a product without conditions or with <c>AMBIENT_15_25</c> alone. Product conditions null
    /// (PRODUCT-MASTER did not state them) is UNKNOWN, never "no conditions".
    /// </summary>
    public static StorageConditionFit Fit(IReadOnlyList<string>? productConditions, EffectiveStorageConditions? effective)
    {
        if (productConditions is null) return StorageConditionFit.Unknown;
        var accepted = effective?.Codes ?? [Ambient];
        return productConditions.All(c => accepted.Contains(c, StringComparer.Ordinal)) ? StorageConditionFit.Fits : StorageConditionFit.Mismatch;
    }

    /// <summary>The stored / fingerprinted order of a role set (enum order) — "sıralı roles" (pack §8.1).</summary>
    public static List<WarehouseRole> Canonical(IEnumerable<WarehouseRole> roles) => roles.Distinct().OrderBy(r => r).ToList();

    /// <summary>The stored / fingerprinted order of a condition set (ordinal) — "sıralı acceptedStorageConditions" (pack §8.1).</summary>
    public static List<string> Canonical(IEnumerable<string> conditions) => conditions.Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToList();
}
