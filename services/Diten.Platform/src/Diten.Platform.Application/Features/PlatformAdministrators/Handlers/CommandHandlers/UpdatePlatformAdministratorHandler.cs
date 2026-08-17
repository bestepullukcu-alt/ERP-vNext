using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class UpdatePlatformAdministratorHandler : IRequestHandler<UpdatePlatformAdministratorCommand, Response<NoContent>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly IPlatformAdministratorProvisioningService _provisioningService;
    private readonly ICurrentUserContext _currentUser;

    public UpdatePlatformAdministratorHandler(
        IPlatformAdministratorRepository repository,
        IPlatformAdministratorProvisioningService provisioningService,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _provisioningService = provisioningService;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(UpdatePlatformAdministratorCommand request, CancellationToken ct)
    {
        var administrator = await _repository.GetByIdAsync(request.Id, ct);
        if (administrator is null)
        {
            return Response<NoContent>.Fail("Platform administrator not found.", 404);
        }

        var normalizedEmail = PlatformAdministratorParsing.NormalizeEmail(request.Request.Email);
        if (await _repository.ExistsByEmailAsync(normalizedEmail, administrator.Id, ct))
        {
            return Response<NoContent>.Fail("Administrator email already exists.", 409);
        }

        var normalizedUserName = PlatformAdministratorParsing.NormalizeUserName(request.Request.UserName);
        if (await _repository.ExistsByUserNameAsync(normalizedUserName, administrator.Id, ct))
        {
            return Response<NoContent>.Fail("Administrator username already exists.", 409);
        }

        administrator.Email = normalizedEmail;
        administrator.NormalizedEmail = normalizedEmail;
        administrator.UserName = request.Request.UserName.Trim();
        administrator.NormalizedUserName = normalizedUserName;
        administrator.DisplayName = request.Request.DisplayName.Trim();
        administrator.Status = PlatformAdministratorParsing.ParseStatus(request.Request.Status);
        administrator.Roles = PlatformAdministratorParsing.ParseRoles(request.Request.Roles).ToList();
        PlatformAdministratorMutationSupport.ApplyScope(
            administrator,
            PlatformAdministratorParsing.ParseActorType(request.Request.ActorType),
            request.Request.PartnerId,
            request.Request.AllowedTenantIds);
        PlatformAdministratorMutationSupport.MarkUpdated(administrator, _currentUser);

        var updated = await _repository.UpdateAsync(administrator, request.Request.Version, ct);
        if (!updated)
        {
            return Response<NoContent>.Fail("The administrator was changed by another user. Reload and try again.", 409);
        }

        await _provisioningService.SyncAsync(new PlatformAdministratorProvisioningSyncRequest(
            administrator.Email,
            administrator.UserName,
            administrator.DisplayName,
            administrator.ActorType.ToString(),
            administrator.Roles.Select(x => x.ToString()).ToList()), ct);

        return Response<NoContent>.Success(204);
    }
}
