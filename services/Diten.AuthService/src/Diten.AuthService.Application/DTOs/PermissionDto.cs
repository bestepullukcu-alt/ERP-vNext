namespace Diten.AuthService.Application.DTOs;

public sealed record PermissionDto(
    Guid Id,
    string Module,
    string Resource,
    string Action,
    string Key,
    string DisplayName,
    string? Description
);
