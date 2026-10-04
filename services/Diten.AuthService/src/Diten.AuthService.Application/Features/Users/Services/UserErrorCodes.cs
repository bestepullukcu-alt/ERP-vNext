using Diten.AuthService.Application.Common;

namespace Diten.AuthService.Application.Features.Users.Services;

/// <summary>
/// WP-USERS-ERROR-CODES-01 (BL-450) — every stable code a refusal of the tenant Users screen's commands carries
/// (create, update, delete, enable/disable, resend invitation, admin password reset), in ONE place, in the shape of
/// <see cref="PasswordErrorCodes"/>. AuthService is a headless API: the English sentence stays in <c>errors</c> for API
/// consumers and logs, the code travels in the envelope's <c>errorCodes</c>, and the screen says it in the reader's
/// language (index.js <c>ERROR_CODE_KEYS</c> → UsersIndex.*.resx, seven languages).
///
/// <para>⚠ One situation, one code; one code, one situation. Changing a VALUE is a breaking contract change — the
/// older constants (<see cref="UserLifecycle.EmailTakenCode"/>, <c>DeleteUserCommandHandler.SelfDeleteCode</c>, …) keep
/// their names and read their value from here. <c>UserErrorCodeBridgeGuardTests</c> reads this class by reflection and
/// fails when a code has no key on the screen or a key has no text in one of the seven languages.</para>
/// </summary>
public static class UserErrorCodes
{
    /// <summary>A live user of the same tenant already has this e-mail address.</summary>
    public const string EmailTaken = "USER_EMAIL_TAKEN";

    /// <summary>An administrator tried to activate an account whose invitation was never accepted.</summary>
    public const string InvitationPending = "USER_INVITATION_PENDING";

    /// <summary>The subscription plan's user limit is reached (params <c>max</c>, <c>current</c> when known).</summary>
    public const string QuotaExceeded = "USER_QUOTA_EXCEEDED";

    /// <summary>The caller tried to delete the account they are signed in with.</summary>
    public const string DeleteSelf = "USER_DELETE_SELF";

    /// <summary>The target is the last account that can create users; deleting it locks the tenant out.</summary>
    public const string DeleteLastSteward = "USER_DELETE_LAST_STEWARD";

    /// <summary>The caller tried to deactivate the account they are signed in with.</summary>
    public const string DeactivateSelf = "USER_DEACTIVATE_SELF";

    /// <summary>No such user in the caller's tenant (missing, deleted, or another tenant's — never told apart).</summary>
    public const string NotFound = "USER_NOT_FOUND";

    /// <summary>The supplied account kind is not one of the defined names.</summary>
    public const string AccountKindInvalid = "USER_ACCOUNT_KIND_INVALID";

    /// <summary>Resend was asked for a user who has already set their password: there is no invitation to resend.</summary>
    public const string SetupAlreadyCompleted = "USER_SETUP_ALREADY_COMPLETED";

    /// <summary>
    /// A password reset was asked for a user whose <c>MustChangePassword</c> is set — a password-setup step is still
    /// waiting. Three situations share it: an invited user who never set a password, a user whose earlier reset link
    /// was never redeemed, and a tenant administrator provisioned with a temporary password who has not changed it.
    /// (Wider than <see cref="InvitationPending"/>, which is only the never-accepted invitation — so the screen's
    /// sentence must not say "has not set a password yet".)
    /// </summary>
    public const string PasswordSetupPending = "USER_PASSWORD_SETUP_PENDING";

    /// <summary>
    /// BL-529 — the caller tried to reset the password of the account they are signed in with. The reset ends the old
    /// password and every session at once: from that seat there would be no way back in until the e-mail arrives. One's
    /// own password is changed with "Change password", never reset.
    /// </summary>
    public const string ResetSelf = "USER_RESET_SELF";

    /// <summary>
    /// BL-529 — the account's password kept changing while the reset was being written (the user's own change, another
    /// administrator's reset); nothing stale was written. Asking again resets it.
    /// </summary>
    public const string ResetConflict = "USER_RESET_CONFLICT";

    /// <summary>
    /// The caller supplied an account kind without <c>auth.users.account-kind.manage</c>. The value predates this list
    /// (WP-INFRA-AUTH-ACCOUNT-KIND-01) and has consumers, so it keeps its name instead of the <c>USER_</c> shape.
    /// </summary>
    public const string AccountKindPermissionDenied = "PERM_DENIED";

    // ── the create/edit validators (400) ────────────────────────────────────────────────────────────────
    public const string EmailRequired = "USER_EMAIL_REQUIRED";
    public const string EmailInvalid = "USER_EMAIL_INVALID";
    public const string EmailTooLong = "USER_EMAIL_TOO_LONG";
    public const string FirstNameRequired = "USER_FIRST_NAME_REQUIRED";
    public const string FirstNameTooLong = "USER_FIRST_NAME_TOO_LONG";
    public const string LastNameRequired = "USER_LAST_NAME_REQUIRED";
    public const string LastNameTooLong = "USER_LAST_NAME_TOO_LONG";

    public static Response<T> NotFoundRefusal<T>()
        => Response<T>.Fail("User not found.", [new ResponseError(NotFound)], 404);
}
