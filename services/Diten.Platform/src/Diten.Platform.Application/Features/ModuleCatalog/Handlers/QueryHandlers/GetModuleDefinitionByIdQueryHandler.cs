using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModuleDefinitionByIdQueryHandler : IRequestHandler<GetModuleDefinitionByIdQuery, ModuleDefinitionDetailDto?>
{
    private readonly IModuleDefinitionRepository _repository;
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ISuitePlatformRepository _suiteRepository;
    private readonly ICapabilityGroupRepository _capabilityRepository;

    public GetModuleDefinitionByIdQueryHandler(
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

    public async Task<ModuleDefinitionDetailDto?> Handle(GetModuleDefinitionByIdQuery request, CancellationToken cancellationToken)
    {
        var module = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (module == null)
        {
            return null;
        }

        return await MapDetailAsync(module, cancellationToken);
    }

    private async Task<ModuleDefinitionDetailDto> MapDetailAsync(ModuleDefinition module, CancellationToken ct)
    {
        var domain = await _domainRepository.GetByIdAsync(module.DomainLandscapeId, ct)
            ?? throw new InvalidOperationException("Module domain landscape reference is missing.");
        var suite = await _suiteRepository.GetByIdAsync(module.SuitePlatformId, ct)
            ?? throw new InvalidOperationException("Module suite platform reference is missing.");
        var capability = await _capabilityRepository.GetByIdAsync(module.CapabilityGroupId, ct)
            ?? throw new InvalidOperationException("Module capability group reference is missing.");

        return MapDetail(module, domain, suite, capability);
    }
}
