namespace Diten.AuthService.Application.Common.Exceptions;

/// <summary>
/// WP-AUTH-INVITED-LIFECYCLE-01 — the users e-mail unique index refused an insert: a live user of the same tenant took
/// the address between the handler's duplicate probe and its insert. Raised by the persistence layer in place of the
/// driver's E11000 so the application answers 409 USER_EMAIL_TAKEN instead of "An unexpected error occurred".
/// </summary>
public sealed class DuplicateUserEmailException : Exception
{
    public DuplicateUserEmailException(Exception inner)
        : base("A live user of this tenant already has this e-mail address.", inner)
    {
    }
}
