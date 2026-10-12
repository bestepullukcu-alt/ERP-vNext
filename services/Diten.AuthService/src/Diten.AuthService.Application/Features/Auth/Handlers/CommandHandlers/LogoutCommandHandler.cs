using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Response<NoContent>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    // MOD-0022 E1 — revokes KS-3 e-signature windows; optional so direct constructions stay valid (DI always supplies it).
    private readonly IESignReauthTicketService? _eSignTickets;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ILogger<LogoutCommandHandler> logger,
        IESignReauthTicketService? eSignTickets = null)
    {
        _eSignTickets = eSignTickets;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(LogoutCommand request, CancellationToken ct)
    {
        var existingToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (existingToken is null)
        {
            return Response<NoContent>.Fail("Invalid refresh token.", 401);
        }

        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        var actorType = principal.FindFirst("actor_type")?.Value;
        var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value;
        var tokenTenantId = principal.FindFirst("tenant_id")?.Value;

        if (!Guid.TryParse(subject, out var accessTokenUserId) || accessTokenUserId != existingToken.UserId)
        {
            return Response<NoContent>.Fail("Access token user mismatch.", 401);
        }

        if (!Guid.TryParse(tokenTenantId, out var accessTokenTenantId) || accessTokenTenantId != existingToken.TenantId)
        {
            return Response<NoContent>.Fail("Access token tenant mismatch.", 401);
        }

        if (!string.Equals(existingToken.ActorType, actorType, StringComparison.OrdinalIgnoreCase))
        {
            return Response<NoContent>.Fail("Access token actor mismatch.", 401);
        }

        if (existingToken.RevokedAt is null)
        {
            existingToken.Revoke(revokedByIp: request.RequestIp, revokedReason: "logout");
            await _refreshTokenRepository.UpdateAsync(existingToken, ct);
        }

        if (_eSignTickets is not null)
        {
            await _eSignTickets.RevokeWindowsAsync(existingToken.TenantId, existingToken.UserId, "logout", ct);
        }

        _logger.LogInformation(
            "User logged out. UserId={UserId} TenantId={TenantId} ActorType={ActorType}",
            existingToken.UserId,
            existingToken.TenantId,
            existingToken.ActorType);
        return Response<NoContent>.Success(204);
    }
}
