using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetSuitePlatformsQueryHandler : IRequestHandler<GetSuitePlatformsQuery, IReadOnlyList<SuitePlatformDto>>
{
    private readonly ISuitePlatformRepository _repository;

    public GetSuitePlatformsQueryHandler(ISuitePlatformRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<SuitePlatformDto>> Handle(GetSuitePlatformsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items
            .Where(x => request.DomainLandscapeId == null || x.DomainLandscapeId == request.DomainLandscapeId)
            .OrderBy(x => x.Name)
            .Select(Map)
            .ToArray();
    }
}
