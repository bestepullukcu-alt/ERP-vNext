namespace Diten.AuthService.Application.DTOs;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IEnumerable<string> Roles,
    Guid? TenantId
);
