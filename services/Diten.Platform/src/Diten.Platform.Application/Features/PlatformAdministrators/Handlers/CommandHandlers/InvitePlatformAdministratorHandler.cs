using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class InvitePlatformAdministratorHandler : IRequestHandler<InvitePlatformAdministratorCommand, Response<PlatformAdministratorInviteResultDto>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly IPlatformAdministratorProvisioningService _provisioningService;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<InvitePlatformAdministratorHandler> _logger;

    public InvitePlatformAdministratorHandler(
        IPlatformAdministratorRepository repository,
        IPlatformAdministratorProvisioningService provisioningService,
        ICurrentUserContext currentUser,
        ILogger<InvitePlatformAdministratorHandler> logger)
    {
        _repository = repository;
        _provisioningService = provisioningService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<Response<PlatformAdministratorInviteResultDto>> Handle(InvitePlatformAdministratorCommand request, CancellationToken ct)
    {
        var normalizedEmail = PlatformAdministratorParsing.NormalizeEmail(request.Request.Email);
        if (await _repository.ExistsByEmailAsync(normalizedEmail, null, ct))
        {
            return Response<PlatformAdministratorInviteResultDto>.Fail("Administrator email already exists.", 409);
        }

        var normalizedUserName = PlatformAdministratorParsing.NormalizeUserName(request.Request.UserName);
        if (await _repository.ExistsByUserNameAsync(normalizedUserName, null, ct))
        {
            return Response<PlatformAdministratorInviteResultDto>.Fail("Administrator username already exists.", 409);
        }

        var actorType = PlatformAdministratorParsing.ParseActorType(request.Request.ActorType);
        var provisioning = await _provisioningService.ProvisionAsync(new PlatformAdministratorProvisioningRequest(
            normalizedEmail,
            request.Request.UserName.Trim(),
            request.Request.DisplayName.Trim(),
            request.Request.ActorType,
            request.Request.Roles,
            request.Request.RequirePasswordChange), ct);

        var now = DateTimeOffset.UtcNow;
        var administrator = new PlatformAdministrator
        {
            Email = normalizedEmail,
            NormalizedEmail = normalizedEmail,
            UserName = request.Request.UserName.Trim(),
            NormalizedUserName = normalizedUserName,
            DisplayName = request.Request.DisplayName.Trim(),
            Status = AdministratorStatus.Active,
            Roles = PlatformAdministratorParsing.ParseRoles(request.Request.Roles).ToList(),
            InvitationStatus = AdministratorInvitationStatus.PendingInvitation,
            InvitedAtUtc = now,
            InviteToken = PlatformAdministratorMutationSupport.NewInviteToken(),
            InviteExpiresAtUtc = now.AddDays(7),
            CreatedBy = _currentUser.ActorName
        };

        PlatformAdministratorMutationSupport.ApplyScope(
            administrator,
            actorType,
            request.Request.PartnerId,
            request.Request.AllowedTenantIds);

        await _repository.CreateAsync(administrator, ct);

        _logger.LogInformation(
            "Platform administrator invite queued for {Email} by {Actor}.",
            administrator.Email,
            _currentUser.ActorName);

        return Response<PlatformAdministratorInviteResultDto>.Success(
            new PlatformAdministratorInviteResultDto(administrator.Id, provisioning.SetupUrl, provisioning.EmailSent),
            201);
    }
}
