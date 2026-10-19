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
    private const string StaleProfileMessage = "The account profile was changed by another user. Reload and try again.";

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

        // BL-521 FIX4 item 3 — the record is written back whole, so it must be the version the client saw, and the write is
        // conditional on the version that was read. Before, the client's version was written with whatever this request
        // had read: naming the NEXT version right after being suspended wrote the pre-suspension snapshot back — an
        // administrator undoing its own suspension through its account settings.
        if (administrator.Version != request.Request.Version)
        {
            return Response<NoContent>.Fail(StaleProfileMessage, 409);
        }

        var loadedVersion = administrator.Version;
        administrator.DisplayName = request.Request.DisplayName.Trim();
        administrator.UpdatedBy = _currentUser.ActorName;

        var updated = await _repository.UpdateAsync(administrator, loadedVersion, ct);
        if (!updated)
        {
            return Response<NoContent>.Fail(StaleProfileMessage, 409);
        }

        return Response<NoContent>.Success(204);
    }
}
