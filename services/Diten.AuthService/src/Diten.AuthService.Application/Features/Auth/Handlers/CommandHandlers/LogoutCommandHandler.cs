using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ILogger<LogoutCommandHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Unit> Handle(LogoutCommand request, CancellationToken ct)
    {
        var existingToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (existingToken is null)
        {
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        var actorType = principal.FindFirst("actor_type")?.Value;
        var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value;
        var tokenTenantId = principal.FindFirst("tenant_id")?.Value;

        if (!Guid.TryParse(subject, out var accessTokenUserId) || accessTokenUserId != existingToken.UserId)
        {
            throw new UnauthorizedAccessException("Access token user mismatch.");
        }

        if (!Guid.TryParse(tokenTenantId, out var accessTokenTenantId) || accessTokenTenantId != existingToken.TenantId)
        {
            throw new UnauthorizedAccessException("Access token tenant mismatch.");
        }

        if (!string.Equals(existingToken.ActorType, actorType, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access token actor mismatch.");
        }

        if (existingToken.RevokedAt is null)
        {
            existingToken.Revoke(revokedByIp: request.RequestIp, revokedReason: "logout");
            await _refreshTokenRepository.UpdateAsync(existingToken, ct);
        }

        _logger.LogInformation(
            "Kullanıcı çıkış yaptı. UserId={UserId} TenantId={TenantId} ActorType={ActorType}",
            existingToken.UserId,
            existingToken.TenantId,
            existingToken.ActorType);
        return Unit.Value;
    }
}
