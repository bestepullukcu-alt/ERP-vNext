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
// BL-529 — a platform administrator resets another platform administrator's password (by e-mail, the account's key here).
public sealed record PlatformAdministratorResetRequest(string Email);
// BL-521 FIX5 item 11 — Status: the Platform record's status. An EXISTING account is switched on only for "Active" (the
// sync's rule) and its address is never confirmed here; none (an older Platform) leaves the account's state alone. A new
// account is created active and confirmed, as before.
public sealed record PlatformAdminProvisioningRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    IReadOnlyList<string> Roles,
    bool RequirePasswordChange = true,
    string? Status = null);
// BL-521 FIX4 item 1 — Email only FINDS the account: a platform administrator's e-mail never changes, so the sync never
// moves an account (FIX3's PreviousEmail is gone; a body that still carries it is not read).
// Item 5 — Status: the Platform record's status; the account is switched on only for "Active". Optional: an older Platform
// does not send it, and then the account's state is left alone.
public sealed record PlatformAdminSyncRequest(
    string Email,
    string UserName,
    string DisplayName,
    string ActorType,
    IReadOnlyList<string> Roles,
    string? Status = null);
public sealed record AssignRoleRequest(Guid RoleId);
public sealed record AssignPermissionRequest(Guid PermissionId);
// Password is optional: omit it to create the user as an invitation (set-password link emailed).
// WP-INFRA-AUTH-ACCOUNT-KIND-01 — AccountKind is optional (enum NAME: Unknown | Human | Service). Omitted ⇒ Unknown;
// supplied ⇒ the caller must also hold auth.users.account-kind.manage, else 403 PERM_DENIED.
// Email / FirstName / LastName are nullable ON THE WIRE only: a null or missing field must reach the validator (which
// answers with its USER_… code), not [ApiController]'s automatic, codeless 400. The command still gets a string.
public sealed record CreateUserRequest(string? Email, string? Password, string? FirstName, string? LastName, string? AccountKind = null);
// WP-INFRA-AUTH-ACCOUNT-KIND-01 — body of POST api/users/{id}/account-kind. Kind is the enum NAME, case-insensitive.
public sealed record SetAccountKindRequest(string Kind);
public sealed record SetTenantPasswordRequest(string Email, string Token, string NewPassword);

// WP-USERS-ROLES-CLOSE-FIX1 item 3 — the set-password page's read-only pre-check (UsersController.CheckSetPasswordLink).
public sealed record CheckSetPasswordLinkRequest(string? Email, string? Token);
// WP-AUTH-USER-KIND-UPDATE-01 — AccountKind is optional (enum NAME). Omitted ⇒ untouched; a CHANGE needs
// auth.users.account-kind.manage as well, else 403 PERM_DENIED (same rule as create).
// FirstName / LastName nullable on the wire for the same reason as CreateUserRequest.
// IsActive is nullable on the wire: a body WITHOUT it leaves the account as it is (a non-nullable bool would bind a
// missing field as false and switch the user off).
public sealed record UpdateUserRequest(string? FirstName, string? LastName, bool? IsActive, string? AccountKind = null);
public sealed record CreateRoleRequest(string Name, string DisplayName, string? Description);
public sealed record UpdateRoleRequest(string DisplayName, string? Description);

/// <summary>WP-EMAIL-LANG-01 — the ONLY field a user may set through <c>PUT api/auth/me/preferences</c>. There is no user
/// id, tenant id or any other field here on purpose: anything else in the body is not bound (no mass assignment).</summary>
public sealed record UpdateMyPreferencesRequest(string? PreferredLanguage)
{
    /// <summary>FIX1 — the body must be a JSON object that NAMES <c>preferredLanguage</c> (a string, or <c>null</c> to clear).
    /// <c>{}</c> or a misspelled name is refused (400), never read as "clear my choice". Other properties are ignored.</summary>
    public static bool TryRead(System.Text.Json.JsonElement body, out UpdateMyPreferencesRequest? request)
    {
        request = null;
        if (body.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return false;
        }

        // FIX2 — the name is read case-insensitively, as the framework's binding did ("PreferredLanguage" works too).
        var named = body.EnumerateObject()
            .Where(property => string.Equals(property.Name, "preferredLanguage", StringComparison.OrdinalIgnoreCase))
            .Select(property => (System.Text.Json.JsonElement?)property.Value)
            .FirstOrDefault();
        if (named is not { } value)
        {
            return false;
        }

        switch (value.ValueKind)
        {
            case System.Text.Json.JsonValueKind.Null:
                request = new UpdateMyPreferencesRequest((string?)null);
                return true;
            case System.Text.Json.JsonValueKind.String:
                request = new UpdateMyPreferencesRequest(value.GetString());
                return true;
            default:
                return false;
        }
    }
}
