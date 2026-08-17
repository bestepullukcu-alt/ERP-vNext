using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAccount.Queries;
using Diten.Platform.Application.Features.PlatformAdministrators;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAccount.Handlers.QueryHandlers;

public sealed class GetPlatformAccountProfileHandler
    : IRequestHandler<GetPlatformAccountProfileQuery, Response<PlatformAccountProfileDto>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public GetPlatformAccountProfileHandler(
        IPlatformAdministratorRepository repository,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<PlatformAccountProfileDto>> Handle(
        GetPlatformAccountProfileQuery request,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.Email))
        {
            return Response<PlatformAccountProfileDto>.Fail("Unauthorized.", 401);
        }

        var administrator = await _repository.GetByNormalizedEmailAsync(
            PlatformAdministratorParsing.NormalizeEmail(_currentUser.Email),
            ct);

        return administrator is null
            ? Response<PlatformAccountProfileDto>.Fail("Platform account profile not found.", 404)
            : Response<PlatformAccountProfileDto>.Success(PlatformAccountMapper.ToProfileDto(administrator));
    }
}
