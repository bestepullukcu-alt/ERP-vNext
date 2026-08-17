using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.PlatformAdministrators.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.QueryHandlers;

public sealed class GetPlatformAdministratorStatsHandler
    : IRequestHandler<GetPlatformAdministratorStatsQuery, Response<PlatformAdministratorStatsDto>>
{
    private readonly IPlatformAdministratorRepository _repository;

    public GetPlatformAdministratorStatsHandler(IPlatformAdministratorRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<PlatformAdministratorStatsDto>> Handle(
        GetPlatformAdministratorStatsQuery request,
        CancellationToken ct)
    {
        var stats = await _repository.GetStatsAsync(ct);
        return Response<PlatformAdministratorStatsDto>.Success(
            new PlatformAdministratorStatsDto(
                stats.Total,
                stats.Active,
                stats.Suspended,
                stats.Disabled,
                stats.PendingInvitation));
    }
}
