namespace Diten.AuthService.Application.Features.ServiceIdentityTokens;

public sealed record ServiceIdentityTokenResponse(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    DateTimeOffset ExpiresAtUtc);
