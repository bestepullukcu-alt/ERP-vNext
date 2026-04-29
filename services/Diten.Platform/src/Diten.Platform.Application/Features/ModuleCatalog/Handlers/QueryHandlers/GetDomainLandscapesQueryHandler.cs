using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetDomainLandscapesQueryHandler : IRequestHandler<GetDomainLandscapesQuery, IReadOnlyList<DomainLandscapeDto>>
{
    private readonly IDomainLandscapeRepository _repository;

    public GetDomainLandscapesQueryHandler(IDomainLandscapeRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<DomainLandscapeDto>> Handle(GetDomainLandscapesQuery request, CancellationToken cancellationToken)
    {
        return (await _repository.GetAllAsync(cancellationToken))
            .OrderBy(x => x.Name)
            .Select(Map)
            .ToArray();
    }
}
