using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModuleDefinitionByModuleIdQueryHandler : IRequestHandler<GetModuleDefinitionByModuleIdQuery, ModuleDefinitionDetailDto?>
{
    private readonly IModuleDefinitionRepository _repository;
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ISuitePlatformRepository _suiteRepository;
    private readonly ICapabilityGroupRepository _capabilityRepository;

    public GetModuleDefinitionByModuleIdQueryHandler(
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

    public async Task<ModuleDefinitionDetailDto?> Handle(GetModuleDefinitionByModuleIdQuery request, CancellationToken cancellationToken)
    {
        var module = await _repository.GetByModuleIdAsync(NormalizeModuleId(request.ModuleId), cancellationToken);
        if (module == null)
        {
            return null;
        }

        var domain = await _domainRepository.GetByIdAsync(module.DomainLandscapeId, cancellationToken)
            ?? throw new InvalidOperationException("Module domain landscape reference is missing.");
        var suite = await _suiteRepository.GetByIdAsync(module.SuitePlatformId, cancellationToken)
            ?? throw new InvalidOperationException("Module suite platform reference is missing.");
        var capability = await _capabilityRepository.GetByIdAsync(module.CapabilityGroupId, cancellationToken)
            ?? throw new InvalidOperationException("Module capability group reference is missing.");

        return MapDetail(module, domain, suite, capability);
    }
}
