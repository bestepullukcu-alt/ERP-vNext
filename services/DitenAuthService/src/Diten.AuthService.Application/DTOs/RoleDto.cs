namespace Diten.AuthService.Application.DTOs;

public sealed record RoleDto(
    Guid Id,
    string Name,
    string DisplayName,
    string? Description,
    bool IsSystem,
    int PermissionCount
);
