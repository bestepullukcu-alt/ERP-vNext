namespace Diten.AuthService.Application.DTOs;

public sealed record AuthResponse(
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    UserDto? User,
    bool RequiresMfa = false,
    string? ChallengeId = null,
    string? MaskedDestination = null,
    string? Channel = null,
    DateTime? MfaExpiresAt = null
);
