namespace Diten.Web.Services.Auth;

public sealed record AuthBridgeResult(
    bool Success,
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    AuthBridgeUser? User,
    string? ErrorMessage);

public sealed record AuthBridgeUser(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IEnumerable<string> Roles,
    Guid? TenantId);
