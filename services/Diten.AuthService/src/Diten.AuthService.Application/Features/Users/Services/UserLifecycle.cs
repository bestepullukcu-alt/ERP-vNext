using Diten.AuthService.Application.Common;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Features.Users.Services;

/// <summary>
/// WP-AUTH-INVITED-LIFECYCLE-01 — the account's lifecycle as the Users screen presents it, and the one transition
/// rule on top of it. Nothing here is stored: every value is derived from the <see cref="User"/> facts.
///
/// <list type="bullet">
/// <item><b>Status</b>: <c>Invited</c> (never set a password — <see cref="User.IsInvitationPending"/>) ·
/// <c>Inactive</c> (switched off) · <c>Active</c>. Invited wins over <see cref="User.IsActive"/>: an invited account
/// cannot sign in, so calling it Active would be the lie the owner found in the list.</item>
/// <item><b>Activation</b>: an invited account is activated by its owner redeeming the set-password link, never by an
/// administrator. Both administrator doors — <c>POST api/users/{id}/enable</c> and <c>PUT api/users/{id}</c> with
/// <c>isActive: true</c> — ask <see cref="RefusesActivation"/> and answer 409 <see cref="InvitationPendingCode"/>.
/// The screen hides the action too, but the screen is not the defence.</item>
/// </list>
/// </summary>
public static class UserLifecycle
{
    public const string StatusInvited = "Invited";
    public const string StatusInactive = "Inactive";
    public const string StatusActive = "Active";

    /// <summary>Stable code: an administrator tried to activate an account whose invitation is still pending.</summary>
    public const string InvitationPendingCode = "USER_INVITATION_PENDING";

    /// <summary>Stable code: a live (not deleted) user of the same tenant already has this e-mail address.</summary>
    public const string EmailTakenCode = "USER_EMAIL_TAKEN";

    public static string StatusOf(User user)
        => user.IsInvitationPending() ? StatusInvited : user.IsActive ? StatusActive : StatusInactive;

    /// <summary>
    /// True when <paramref name="requestedActive"/> would switch an INVITED account on. Re-sending the current value is
    /// not a transition: an invited account that is already (wrongly) active is not refused for staying so.
    /// </summary>
    public static bool RefusesActivation(User user, bool requestedActive)
        => requestedActive && !user.IsActive && user.IsInvitationPending();

    public static Response<T> InvitationPendingRefusal<T>()
        => Response<T>.Fail(
            "This user has not accepted the invitation yet; the account activates when they set their password. Resend the invitation instead.",
            [new ResponseError(InvitationPendingCode)],
            409);

    public static Response<T> EmailTakenRefusal<T>()
        => Response<T>.Fail("Email is already in use.", [new ResponseError(EmailTakenCode)], 409);
}
