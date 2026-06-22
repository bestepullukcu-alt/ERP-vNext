using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities;

/// <summary>
/// Operator-managed module service. Replaces the static <c>ModuleCatalogService</c> enum as the source of the
/// Module Catalog "Service" dropdown. The enum is retained only as the canonical-default seed source (see ModuleServiceSeed).
/// </summary>
public sealed class ModuleService : GlobalEntity
{
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
