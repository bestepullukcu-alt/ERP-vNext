namespace Diten.AuthService.Application.Features.Users.Services;

/// <summary>
/// WP-USERS-ERROR-CODES-01 — the length limits of a user's fields, in ONE place. The validators read them here; the
/// Users form's <c>maxlength</c> and the MVC proxy's own check carry the same numbers
/// (<c>UserEditViewModel</c>), and a test holds the two sides equal.
/// </summary>
public static class UserFieldLimits
{
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 256;
}
