namespace Diten.AuthService.Api.Models;

public sealed record LoginRequest(string Email, string Password);
public sealed record TenantLoginRequest(string Email, string Password);
public sealed record PlatformLoginRequest(string Email, string Password);
public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public sealed record RefreshTokenRequest(string AccessToken, string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record AssignRoleRequest(Guid RoleId);
public sealed record AssignPermissionRequest(Guid PermissionId);
public sealed record CreateUserRequest(string Email, string Password, string FirstName, string LastName);
public sealed record UpdateUserRequest(string FirstName, string LastName, bool IsActive);
public sealed record CreateRoleRequest(string Name, string DisplayName, string? Description);
public sealed record UpdateRoleRequest(string DisplayName, string? Description);
