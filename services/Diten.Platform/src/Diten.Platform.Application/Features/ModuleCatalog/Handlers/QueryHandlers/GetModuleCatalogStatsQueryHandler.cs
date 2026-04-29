using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModuleCatalogStatsQueryHandler : IRequestHandler<GetModuleCatalogStatsQuery, Response<ModuleCatalogStatsResponse>>
{
    private readonly IModuleCatalogRepository _repository;

    public GetModuleCatalogStatsQueryHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ModuleCatalogStatsResponse>> Handle(GetModuleCatalogStatsQuery request, CancellationToken ct)
    {
        var stats = await _repository.GetStatsAsync(ct);

        var response = new ModuleCatalogStatsResponse(
            Total: (int)stats.Values.Sum(),
            Active: (int)(stats.TryGetValue(ModuleCatalogStatus.Active, out var active) ? active : 0),
            Beta: (int)(stats.TryGetValue(ModuleCatalogStatus.Beta, out var beta) ? beta : 0),
            Preview: (int)(stats.TryGetValue(ModuleCatalogStatus.Preview, out var preview) ? preview : 0)
        );

        return Response<ModuleCatalogStatsResponse>.Success(response);
    }
}
