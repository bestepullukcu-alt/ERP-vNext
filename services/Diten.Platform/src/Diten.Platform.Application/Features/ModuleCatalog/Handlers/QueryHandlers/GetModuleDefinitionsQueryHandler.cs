using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModuleDefinitionsQueryHandler : IRequestHandler<GetModuleDefinitionsQuery, ModuleDefinitionListResultDto>
{
    private readonly IModuleDefinitionRepository _repository;
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ISuitePlatformRepository _suiteRepository;
    private readonly ICapabilityGroupRepository _capabilityRepository;

    public GetModuleDefinitionsQueryHandler(
        IModuleDefinitionRepository repository,
        IDomainLandscapeRepository domainRepository,
        ISuitePlatformRepository suiteRepository,
        ICapabilityGroupRepository capabilityRepository)
    {
        _repository = repository;
        _domainRepository = domainRepository;
        _suiteRepository = suiteRepository;
        _capabilityRepository = capabilityRepository;
    }

    public async Task<ModuleDefinitionListResultDto> Handle(GetModuleDefinitionsQuery request, CancellationToken cancellationToken)
    {
        var query = new ModuleDefinitionQuery(
            request.Search,
            request.DomainLandscapeId,
            request.SuitePlatformId,
            request.CapabilityGroupId,
            request.Status,
            request.IsTenantAssignable,
            request.IsPlatformCore);

        var (items, totalCount) = await _repository.QueryAsync(query, cancellationToken);
        var domains = (await _domainRepository.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);
        var suites = (await _suiteRepository.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);
        var capabilities = (await _capabilityRepository.GetAllAsync(cancellationToken)).ToDictionary(x => x.Id);

        return new ModuleDefinitionListResultDto(
            items.Select(x => MapListItem(
                x,
                domains[x.DomainLandscapeId],
                suites[x.SuitePlatformId],
                capabilities[x.CapabilityGroupId]))
            .OrderBy(x => x.ModuleId)
            .ToArray(),
            totalCount);
    }
}
