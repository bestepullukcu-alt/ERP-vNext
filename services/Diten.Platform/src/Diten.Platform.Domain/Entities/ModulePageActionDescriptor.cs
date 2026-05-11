using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Domain.Entities;

public sealed class ModulePageActionDescriptor : TenantScopedEntity
{
    public Guid PageDescriptorId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string PageCode { get; set; } = string.Empty;
    public string ActionCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public ModulePageActionType ActionType { get; set; } = ModulePageActionType.Toolbar;
    public int SortOrder { get; set; }
    public bool IsDangerous { get; set; }
    public bool IsToolbarAction { get; set; }
    public bool IsRowAction { get; set; }
    public ModulePageActionStatus Status { get; set; } = ModulePageActionStatus.Draft;
    public string? Description { get; set; }
}
