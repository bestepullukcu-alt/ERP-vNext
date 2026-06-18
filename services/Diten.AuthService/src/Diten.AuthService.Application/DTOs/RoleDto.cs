namespace Diten.AuthService.Application.DTOs;

public sealed record RoleDto(
    Guid Id,
    string Name,
    string DisplayName,
    string? Description,
    bool IsSystem,
    int PermissionCount,
    // AG-V2 /Roles list enrichment — populated by GetAllRolesQueryHandler only.
    // Optional defaults keep the single-role handlers (GetById/Create/Update) untouched.
    int UserCount = 0,
    IReadOnlyDictionary<string, int>? ModulePermissions = null
);
