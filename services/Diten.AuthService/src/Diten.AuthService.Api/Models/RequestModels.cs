namespace Diten.AuthService.Api.Models;

public sealed record LoginRequest(string Email, string Password, bool RememberMe = false);
public sealed record TenantLoginRequest(string Email, string Password, bool RememberMe = false);
public sealed record VerifyMfaRequest(string ChallengeId, string Code);
public sealed record ResendMfaRequest(string ChallengeId);
public sealed record PlatformLoginRequest(string Email, string Password, bool RememberMe = false);
public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public sealed record RefreshTokenRequest(string AccessToken, string RefreshToken);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record PlatformForcedChangePasswordRequest(string CurrentPassword, string NewPassword, bool RememberMe = false);
public sealed record PlatformForgotPasswordRequest(string Email);
public sealed record PlatformResetPasswordRequest(string Email, string Token, string NewPassword);
public sealed record PlatformAdminProvisioningRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    IReadOnlyList<string> Roles,
    bool RequirePasswordChange = true);
public sealed record PlatformAdminSyncRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    IReadOnlyList<string> Roles);
public sealed record AssignRoleRequest(Guid RoleId);
public sealed record AssignPermissionRequest(Guid PermissionId);
public sealed record CreateUserRequest(string Email, string Password, string FirstName, string LastName);
public sealed record UpdateUserRequest(string FirstName, string LastName, bool IsActive);
public sealed record CreateRoleRequest(string Name, string DisplayName, string? Description);
public sealed record UpdateRoleRequest(string DisplayName, string? Description);
