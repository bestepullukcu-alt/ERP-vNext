using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITenantContext tenantContext,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            throw new ArgumentException("Geçersiz kullanıcı ID.");

        var user = await _userRepository.GetByIdAndTenantAsync(userId, _tenantContext.TenantId, ct);
        if (user == null) throw new KeyNotFoundException("Kullanıcı bulunamadı.");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Mevcut şifre yanlış.");

        var newHashedPassword = _passwordHasher.Hash(request.NewPassword);
        user.UpdatePassword(newHashedPassword);

        await _userRepository.UpdateAsync(user, ct);
        await _refreshTokenRepository.RevokeAllByUserAsync(userId, _tenantContext.TenantId, ct);

        _logger.LogInformation("Kullanıcı şifresini değiştirdi. UserId={UserId}", userId);
        return Unit.Value;
    }
}
