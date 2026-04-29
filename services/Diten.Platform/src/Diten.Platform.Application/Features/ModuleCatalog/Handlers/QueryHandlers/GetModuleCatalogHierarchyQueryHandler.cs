using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModuleCatalogHierarchyQueryHandler : IRequestHandler<GetModuleCatalogHierarchyQuery, ModuleCatalogHierarchyDto>
{
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ISuitePlatformRepository _suiteRepository;
    private readonly ICapabilityGroupRepository _capabilityRepository;
    private readonly IModuleDefinitionRepository _moduleRepository;

    public GetModuleCatalogHierarchyQueryHandler(
        IDomainLandscapeRepository domainRepository,
        ISuitePlatformRepository suiteRepository,
        ICapabilityGroupRepository capabilityRepository,
        IModuleDefinitionRepository moduleRepository)
    {
        _domainRepository = domainRepository;
        _suiteRepository = suiteRepository;
        _capabilityRepository = capabilityRepository;
        _moduleRepository = moduleRepository;
    }

    public async Task<ModuleCatalogHierarchyDto> Handle(GetModuleCatalogHierarchyQuery request, CancellationToken cancellationToken)
    {
        var domains = await _domainRepository.GetAllAsync(cancellationToken);
        var suites = await _suiteRepository.GetAllAsync(cancellationToken);
        var capabilities = await _capabilityRepository.GetAllAsync(cancellationToken);
        var modules = await _moduleRepository.GetAllAsync(cancellationToken);

        return new ModuleCatalogHierarchyDto(
            domains.OrderBy(x => x.Name).Select(Map).ToArray(),
            suites.OrderBy(x => x.Name).Select(Map).ToArray(),
            capabilities.OrderBy(x => x.Name).Select(Map).ToArray(),
            new ModuleCatalogSummaryDto(
                domains.Count,
                suites.Count,
                capabilities.Count,
                modules.Count,
                modules.Count(x => x.IsTenantAssignable),
                modules.Count(x => x.IsPlatformCore),
                modules.Count(x => x.Status is ModuleLifecycleStatus.Deprecated or ModuleLifecycleStatus.Retired)));
    }
}
