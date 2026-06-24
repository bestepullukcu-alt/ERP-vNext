using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Commands;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

// Authed, tenant-scoped: re-issue a fresh 7-day set-password token for a still-pending invited user
// and re-send the invitation email. Tenant comes from context (NOT the request).
public sealed class ResendUserInvitationCommandHandler : IRequestHandler<ResendUserInvitationCommand, Response<InviteLinkResult>>
{
    private static readonly TimeSpan InvitationTokenLifetime = TimeSpan.FromDays(7);

    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly ITenantUserInvitationEmailService _invitationEmailService;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<ResendUserInvitationCommandHandler> _logger;

    public ResendUserInvitationCommandHandler(
        IUserRepository userRepository,
        ITenantContext tenantContext,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        ITenantUserInvitationEmailService invitationEmailService,
        IHostEnvironment environment,
        ILogger<ResendUserInvitationCommandHandler> logger)
    {
        _userRepository = userRepository;
        _tenantContext = tenantContext;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _invitationEmailService = invitationEmailService;
        _environment = environment;
        _logger = logger;
    }

    public async Task<Response<InviteLinkResult>> Handle(ResendUserInvitationCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, _tenantContext.TenantId, ct);
        if (user is null) return Response<InviteLinkResult>.Fail("User not found.", 404);

        // Resend only applies to a pending invitation. Once the user has set their password
        // (active + no change requirement) there is nothing to resend.
        if (!user.MustChangePassword)
        {
            return Response<InviteLinkResult>.Fail("User has already completed setup.", 409);
        }

        var setupToken = _tokenService.GenerateRefreshToken();
        user.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.Add(InvitationTokenLifetime));
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

        _logger.LogInformation("Tenant user invitation re-sent. Id={Id} EmailSent={EmailSent}", user.Id, emailSent);
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("[DEV] Tenant set-password link for {Email}: {SetupUrl}", user.Email, setupUrl);
        }

        // Dev-only: surface the link so the admin can copy it; null in prod (never leaks).
        return Response<InviteLinkResult>.Success(new InviteLinkResult(_environment.IsDevelopment() ? setupUrl : null), 200);
    }
}
