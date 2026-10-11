using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Security;
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
    private readonly IActorSafetyGuard _safetyGuard;
    private readonly ILogger<ResendPlatformAdministratorInviteHandler> _logger;

    public ResendPlatformAdministratorInviteHandler(
        IPlatformAdministratorRepository repository,
        IPlatformAdministratorProvisioningService provisioningService,
        ICurrentUserContext currentUser,
        IActorSafetyGuard safetyGuard,
        ILogger<ResendPlatformAdministratorInviteHandler> logger)
    {
        _safetyGuard = safetyGuard;
        _repository = repository;
        _provisioningService = provisioningService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Response<PlatformAdministratorInviteResultDto>> Handle(ResendPlatformAdministratorInviteCommand request, CancellationToken ct)
    {
        // BL-529 FIX2 — a setup link to an existing account is a reset (AuthService ends its password and sessions; it cannot
        // tell who asked, a server-to-server call): one's own account is refused here, before AuthService is called.
        var selfGuard = await _safetyGuard.EnsureNotSelfAsync(request.Id, AdminSafetyAction.ResendInvite, ct);
        if (selfGuard is not null)
        {
            return Response<PlatformAdministratorInviteResultDto>.Fail(selfGuard.Errors, selfGuard.StatusCode, selfGuard.ReasonCode);
        }

        var administrator = await _repository.GetByIdAsync(request.Id, ct);
        if (administrator is null)
        {
            return Response<PlatformAdministratorInviteResultDto>.Fail("Platform administrator not found.", 404);
        }

        // BL-521 FIX4 item 3 — refused BEFORE Auth is provisioned: the record is written back whole, so it must be the
        // version the client saw, and the write is conditional on the version that was read.
        if (administrator.Version != request.Request.Version)
        {
            return Response<PlatformAdministratorInviteResultDto>.Fail(AdminSafetyMessages.ChangedByAnotherUser, 409);
        }

        var loadedVersion = administrator.Version;
        var convergence = new PlatformAdministratorAuthConvergence(_repository, _provisioningService, _logger);

        // FIX5 item 11 — the record's status travels with the provision: an EXISTING account is switched on only for an
        // Active record (a suspended administrator's resend no longer re-enables its account).
        var provisioning = await _provisioningService.ProvisionAsync(new PlatformAdministratorProvisioningRequest(
            administrator.Email,
            administrator.UserName,
            administrator.DisplayName,
            administrator.ActorType.ToString(),
            administrator.Roles.Select(x => x.ToString()).ToList(),
            RequirePasswordChange: true,
            Status: administrator.Status.ToString())
        {
            AdministratorId = administrator.Id
        }, ct);

        var now = DateTimeOffset.UtcNow;
        if (administrator.InvitationStatus != AdministratorInvitationStatus.Accepted)
        {
            administrator.InvitationStatus = AdministratorInvitationStatus.Invited;
            administrator.InvitedAtUtc = now;
            administrator.InviteToken = PlatformAdministratorMutationSupport.NewInviteToken();
            administrator.InviteExpiresAtUtc = now.AddDays(7);
        }

        PlatformAdministratorMutationSupport.MarkUpdated(administrator, _currentUser);

        // BL-521 FIX5 item 5 — AUTH WAS PROVISIONED FROM THE RECORD AS LOADED, BEFORE THIS WRITE. A write that is refused
        // (another request saved in between), fails or never answers leaves Auth holding that loaded picture — roles,
        // status — which another request may already have replaced. As the update does: the record is read again and Auth
        // is converged to what it holds now; the loaded picture is never put back.
        bool updated;
        try
        {
            updated = await _repository.UpdateAsync(administrator, loadedVersion, ct);
        }
        catch
        {
            await convergence.ConvergeToRecordAsync(administrator.Id);
            throw;
        }

        if (!updated)
        {
            await convergence.ConvergeToRecordAsync(administrator.Id);
            return Response<PlatformAdministratorInviteResultDto>.Fail(AdminSafetyMessages.ChangedByAnotherUser, 409);
        }

        // FIX5 item 6 — identity only: the address is personal data and does not belong in the log.
        _logger.LogInformation(
            "Platform administrator invite resend queued for {AdministratorId} by {Actor}.",
            administrator.Id,
            _currentUser.ActorName);

        return Response<PlatformAdministratorInviteResultDto>.Success(
            new PlatformAdministratorInviteResultDto(administrator.Id, provisioning.SetupUrl, provisioning.EmailSent),
            200);
    }
}
