using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities;

/// <summary>
/// FEAT-NAVPREFS-DOMAINS — a tenant's per-DOMAIN sidebar customization: reorder (<see cref="SortOrder"/> override)
/// and rename (<see cref="DisplayNameOverride"/>) of a whole domain group. One row per (TenantId, DomainCode). A
/// domain with no preference row keeps the default (implicit catalog) order and its resolved display name. Modelled
/// as a GlobalEntity with an explicit <see cref="TenantId"/> (like <c>TenantNavPreference</c>) so it is queried by
/// an explicit tenant id, independent of ambient tenant scope.
/// </summary>
public sealed class TenantNavDomainPreference : GlobalEntity
{
    public Guid TenantId { get; set; }
    public string DomainCode { get; set; } = string.Empty;

    /// <summary>Reorder override for the domain group. Null = implicit (catalog) order.</summary>
    public int? SortOrder { get; set; }

    /// <summary>Rename override for the domain. Null/blank = the resolved domain display name.</summary>
    public string? DisplayNameOverride { get; set; }
}
