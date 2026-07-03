using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities;

/// <summary>
/// FEAT-TENANT-NAV-PREFS — a tenant's per-module sidebar customization: reorder (<see cref="SortOrder"/> override),
/// show/hide (<see cref="IsHidden"/>), and rename (<see cref="DisplayNameOverride"/>). One row per (TenantId,
/// ModuleCode). A module with NO preference row keeps the default catalog behavior. Modelled as a GlobalEntity with
/// an explicit <see cref="TenantId"/> (like <c>TenantModuleEntitlement</c>) so it is queried by an explicit tenant
/// id, independent of ambient tenant scope.
/// </summary>
public sealed class TenantNavPreference : GlobalEntity
{
    public Guid TenantId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>Reorder override. Null = use the module's catalog SortOrder (default position).</summary>
    public int? SortOrder { get; set; }

    /// <summary>Hide the module from the tenant's sidebar.</summary>
    public bool IsHidden { get; set; }

    /// <summary>Rename override. Null/blank = use the module's real display name.</summary>
    public string? DisplayNameOverride { get; set; }
}
