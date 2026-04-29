using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetCapabilityGroupsQueryHandler : IRequestHandler<GetCapabilityGroupsQuery, IReadOnlyList<CapabilityGroupDto>>
{
    private readonly ICapabilityGroupRepository _repository;

    public GetCapabilityGroupsQueryHandler(ICapabilityGroupRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<CapabilityGroupDto>> Handle(GetCapabilityGroupsQuery request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetAllAsync(cancellationToken);
        return items
            .Where(x => request.DomainLandscapeId == null || x.DomainLandscapeId == request.DomainLandscapeId)
            .Where(x => request.SuitePlatformId == null || x.SuitePlatformId == request.SuitePlatformId)
            .OrderBy(x => x.Name)
            .Select(Map)
            .ToArray();
    }
}
