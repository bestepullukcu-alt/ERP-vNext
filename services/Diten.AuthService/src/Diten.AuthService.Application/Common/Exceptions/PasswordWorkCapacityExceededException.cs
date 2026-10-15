namespace Diten.AuthService.Application.Common.Exceptions;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 FIX5 addendum item 1 — every password-hash slot of this process is busy and the short queue is
/// full (or the wait ran out): the request is answered 429 (<c>AUTH_TOO_MANY_REQUESTS</c>), never kept waiting.
/// </summary>
public sealed class PasswordWorkCapacityExceededException()
    : Exception("Too many requests. Try again later.");
