using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class ResendPlatformAdministratorInviteHandler : IRequestHandler<ResendPlatformAdministratorInviteCommand, Response<PlatformAdministratorInviteResultDto>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly IPlatformAdministratorProvisioningService _provisioningService;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<ResendPlatformAdministratorInviteHandler> _logger;

    public ResendPlatformAdministratorInviteHandler(
        IPlatformAdministratorRepository repository,
        IPlatformAdministratorProvisioningService provisioningService,
        ICurrentUserContext currentUser,
        ILogger<ResendPlatformAdministratorInviteHandler> logger)
    {
        _repository = repository;
        _provisioningService = provisioningService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Response<PlatformAdministratorInviteResultDto>> Handle(ResendPlatformAdministratorInviteCommand request, CancellationToken ct)
    {
        var administrator = await _repository.GetByIdAsync(request.Id, ct);
        if (administrator is null)
        {
            return Response<PlatformAdministratorInviteResultDto>.Fail("Platform administrator not found.", 404);
        }

        var provisioning = await _provisioningService.ProvisionAsync(new PlatformAdministratorProvisioningRequest(
            administrator.Email,
            administrator.UserName,
            administrator.DisplayName,
            administrator.ActorType.ToString(),
            administrator.Roles.Select(x => x.ToString()).ToList(),
            RequirePasswordChange: true), ct);

        var now = DateTimeOffset.UtcNow;
        if (administrator.InvitationStatus != AdministratorInvitationStatus.Accepted)
        {
            administrator.InvitationStatus = AdministratorInvitationStatus.Invited;
            administrator.InvitedAtUtc = now;
            administrator.InviteToken = PlatformAdministratorMutationSupport.NewInviteToken();
            administrator.InviteExpiresAtUtc = now.AddDays(7);
        }

        PlatformAdministratorMutationSupport.MarkUpdated(administrator, _currentUser);

        var updated = await _repository.UpdateAsync(administrator, request.Request.Version, ct);
        if (!updated)
        {
            return Response<PlatformAdministratorInviteResultDto>.Fail("The administrator was changed by another user. Reload and try again.", 409);
        }

        _logger.LogInformation(
            "Platform administrator invite resend queued for {Email} by {Actor}.",
            administrator.Email,
            _currentUser.ActorName);

        return Response<PlatformAdministratorInviteResultDto>.Success(
            new PlatformAdministratorInviteResultDto(administrator.Id, provisioning.SetupUrl, provisioning.EmailSent),
            200);
    }
}
