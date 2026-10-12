namespace Diten.Web.Services.Auth;

public sealed record AuthBridgeResult(
    bool Success,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    AuthBridgeUser? User,
    string? ErrorMessage,
    bool RequiresMfa = false,
    string? ChallengeId = null,
    string? MaskedDestination = null,
    string? Channel = null,
    DateTime? MfaExpiresAt = null,
    bool RequiresPasswordChange = false,
    bool ReauthRequired = false,
    int? StatusCode = null,
    // WP-USERS-ROLES-CLOSE-FIX1 item 2 (BL-496) — true when ErrorMessage is the reader's language (a code the bridge knows);
    // false = the service's own sentence, which a page must not show (the door says its own localized sentence instead).
    bool ErrorLocalized = false);

public sealed record AuthBridgeUser(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IEnumerable<string> Roles,
    Guid? TenantId);
