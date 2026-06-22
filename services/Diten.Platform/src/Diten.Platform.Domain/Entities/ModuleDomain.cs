using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities;

/// <summary>
/// Operator-managed module domain. Replaces the static <c>ModuleCatalogDomain</c> enum as the source of the
/// Module Catalog "Domain" dropdown. The enum is retained only as the seed source (see ModuleDomainSeed).
/// </summary>
public sealed class ModuleDomain : GlobalEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
