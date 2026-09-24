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
public sealed record TenantForcedChangePasswordRequest(string CurrentPassword, string NewPassword, bool RememberMe = false);
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
// Password is optional: omit it to create the user as an invitation (set-password link emailed).
// WP-INFRA-AUTH-ACCOUNT-KIND-01 — AccountKind is optional (enum NAME: Unknown | Human | Service). Omitted ⇒ Unknown;
// supplied ⇒ the caller must also hold auth.users.account-kind.manage, else 403 PERM_DENIED.
public sealed record CreateUserRequest(string Email, string? Password, string FirstName, string LastName, string? AccountKind = null);
// WP-INFRA-AUTH-ACCOUNT-KIND-01 — body of POST api/users/{id}/account-kind. Kind is the enum NAME, case-insensitive.
public sealed record SetAccountKindRequest(string Kind);
public sealed record SetTenantPasswordRequest(string Email, string Token, string NewPassword);
// WP-AUTH-USER-KIND-UPDATE-01 — AccountKind is optional (enum NAME). Omitted ⇒ untouched; a CHANGE needs
// auth.users.account-kind.manage as well, else 403 PERM_DENIED (same rule as create).
public sealed record UpdateUserRequest(string FirstName, string LastName, bool IsActive, string? AccountKind = null);
public sealed record CreateRoleRequest(string Name, string DisplayName, string? Description);
public sealed record UpdateRoleRequest(string DisplayName, string? Description);
