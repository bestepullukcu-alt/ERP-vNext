using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Commands;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

// Admin reset (mirror of ResendUserInvitation, guard INVERTED): only for users who already completed
// setup. Issues a fresh 7-day set-password token, forces a password change, and re-sends the link.
// Pending invitations (MustChangePassword == true) are rejected — use Resend for those.
public sealed class AdminResetPasswordCommandHandler : IRequestHandler<AdminResetPasswordCommand, Response<InviteLinkResult>>
{
    private static readonly TimeSpan InvitationTokenLifetime = TimeSpan.FromDays(7);

    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly ITenantUserInvitationEmailService _invitationEmailService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<AdminResetPasswordCommandHandler> _logger;

    public AdminResetPasswordCommandHandler(
        IUserRepository userRepository,
        ITenantContext tenantContext,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        ITenantUserInvitationEmailService invitationEmailService,
        IHostEnvironment environment,
        ILogger<AdminResetPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _invitationEmailService = invitationEmailService;
        _environment = environment;
        _logger = logger;
    }

    public async Task<Response<InviteLinkResult>> Handle(AdminResetPasswordCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, _tenantContext.TenantId, ct);
        if (user is null) return Response<InviteLinkResult>.Fail("User not found.", 404);

        // Inverted guard: a still-pending invite should be re-sent, not "reset".
        if (user.MustChangePassword)
        {
            return Response<InviteLinkResult>.Fail("User invitation is still pending — use Resend instead.", 409);
        }

        var setupToken = _tokenService.GenerateRefreshToken();
        user.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.Add(InvitationTokenLifetime));
        user.RequirePasswordChange(null); // force change until the link is redeemed
        await _userRepository.UpdateForTenantAsync(user, _tenantContext.TenantId, ct);

        var setupUrl = _invitationEmailService.BuildTenantSetPasswordUrl(user.Email, setupToken);
        var emailSent = false;
        try
        {
            await _invitationEmailService.SendTenantUserInvitationAsync(user.Email, setupToken, ct);
            emailSent = true;
        }
        catch when (_environment.IsDevelopment())
        {
            // Dev without SMTP: swallow and rely on the logged link below.
        }

        _logger.LogInformation("Admin password reset issued. Id={Id} EmailSent={EmailSent}", user.Id, emailSent);
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("[DEV] Tenant set-password link for {Email}: {SetupUrl}", user.Email, setupUrl);
        }

        // Dev-only: surface the link so the admin can copy it; null in prod (never leaks).
        return Response<InviteLinkResult>.Success(new InviteLinkResult(_environment.IsDevelopment() ? setupUrl : null), 200);
    }
}
