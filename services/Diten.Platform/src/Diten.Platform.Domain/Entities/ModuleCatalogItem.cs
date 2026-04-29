using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities;

public sealed class ModuleCatalogItem : GlobalEntity
{
    public string ModuleCode { get; set; } = string.Empty;
    public string ModuleName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Domain { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string? Category { get; set; }
    public ModuleCatalogStatus Status { get; set; } = ModuleCatalogStatus.Draft;
    public string ModuleVersion { get; set; } = "1.0.0";
    public bool IsCoreModule { get; set; }
    public bool IsTenantAssignable { get; set; }
    public int SortOrder { get; set; }
}
