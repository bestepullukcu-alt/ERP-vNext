namespace Diten.Platform.Application.Features.ModulePages;

public sealed record ModulePageActionDescriptorDto(
    Guid Id,
    Guid PageDescriptorId,
    string ModuleCode,
    string PageCode,
    string ActionCode,
    string DisplayName,
    string PermissionKey,
    string ActionType,
    int SortOrder,
    bool IsDangerous,
    bool IsToolbarAction,
    bool IsRowAction,
    string Status,
    string? Description);

public sealed record CreateModulePageActionDescriptorRequest(
    string ActionCode,
    string DisplayName,
    string PermissionKey,
    string ActionType,
    int? SortOrder,
    bool IsDangerous,
    bool IsToolbarAction,
    bool IsRowAction,
    string Status,
    string? Description);

public sealed record UpdateModulePageActionDescriptorRequest(
    string ActionCode,
    string DisplayName,
    string PermissionKey,
    string ActionType,
    int? SortOrder,
    bool IsDangerous,
    bool IsToolbarAction,
    bool IsRowAction,
    string Status,
    string? Description);

public static class ModulePageActionDescriptorMapper
{
    public static ModulePageActionDescriptorDto ToDto(Diten.Platform.Domain.Entities.ModulePageActionDescriptor descriptor) =>
        new(
            descriptor.Id,
            descriptor.PageDescriptorId,
            descriptor.ModuleCode,
            descriptor.PageCode,
            descriptor.ActionCode,
            descriptor.DisplayName,
            descriptor.PermissionKey,
            descriptor.ActionType.ToString(),
            descriptor.SortOrder,
            descriptor.IsDangerous,
            descriptor.IsToolbarAction,
            descriptor.IsRowAction,
            descriptor.Status.ToString(),
            descriptor.Description);
}
