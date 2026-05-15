using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAccount.Commands;
using Diten.Platform.Application.Features.PlatformAdministrators;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAccount.Handlers.CommandHandlers;

public sealed class UpdatePlatformAccountSettingsHandler
    : IRequestHandler<UpdatePlatformAccountSettingsCommand, Response<NoContent>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public UpdatePlatformAccountSettingsHandler(
        IPlatformAdministratorRepository repository,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(
        UpdatePlatformAccountSettingsCommand request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.Email))
        {
            return Response<NoContent>.Fail("Unauthorized.", 401);
        }

        var administrator = await _repository.GetByNormalizedEmailAsync(
            PlatformAdministratorParsing.NormalizeEmail(_currentUser.Email),
            ct);
        if (administrator is null)
        {
            return Response<NoContent>.Fail("Platform account profile not found.", 404);
        }

        administrator.DisplayName = request.Request.DisplayName.Trim();
        administrator.UpdatedBy = _currentUser.ActorName;

        var updated = await _repository.UpdateAsync(administrator, request.Request.Version, ct);
        if (!updated)
        {
            return Response<NoContent>.Fail("The account profile was changed by another user. Reload and try again.", 409);
        }

        return Response<NoContent>.Success(204);
    }
}
