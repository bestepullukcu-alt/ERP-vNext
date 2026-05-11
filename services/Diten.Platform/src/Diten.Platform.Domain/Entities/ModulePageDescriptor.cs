using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities;

public sealed class ModulePageDescriptor : TenantScopedEntity
{
    public required string ModuleCode { get; set; }
    public required string PageCode { get; set; }
    public required string DisplayName { get; set; }
    public required string RoutePath { get; set; }
    public string? RequiredPermission { get; set; }
    public string? ParentPageCode { get; set; }
    public bool IsNavigationVisible { get; set; } = true;
    public ModulePageType PageType { get; set; } = ModulePageType.List;
    public ModulePageStatus Status { get; set; } = ModulePageStatus.Draft;
    public int SortOrder { get; set; }
    public string? Description { get; set; }
}
