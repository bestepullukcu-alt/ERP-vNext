using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

// Admin enable/disable, tenant-scoped. Disabling revokes refresh tokens so live sessions terminate.
public sealed class SetUserActiveStatusCommandHandler : IRequestHandler<SetUserActiveStatusCommand, Response<NoContent>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SetUserActiveStatusCommandHandler> _logger;

    public SetUserActiveStatusCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITenantContext tenantContext,
        ILogger<SetUserActiveStatusCommandHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(SetUserActiveStatusCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAndTenantAsync(request.Id, _tenantContext.TenantId, ct);
        if (user is null) return Response<NoContent>.Fail("User not found.", 404);

        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await _userRepository.UpdateAsync(user, ct);

        // Disabling must terminate active sessions immediately.
        if (!request.IsActive)
        {
            await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, _tenantContext.TenantId, ct);
        }

        _logger.LogInformation("User active status changed. Id={Id} IsActive={IsActive}", user.Id, request.IsActive);
        return Response<NoContent>.Success(204);
    }
}
