using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class UpdateModuleDefinitionCommandHandler : IRequestHandler<UpdateModuleDefinitionCommand, ModuleDefinitionDetailDto>
{
    private readonly IModuleDefinitionRepository _repository;
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ISuitePlatformRepository _suiteRepository;
    private readonly ICapabilityGroupRepository _capabilityRepository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateModuleDefinitionCommandHandler(
        IModuleDefinitionRepository repository,
        IDomainLandscapeRepository domainRepository,
        ISuitePlatformRepository suiteRepository,
        ICapabilityGroupRepository capabilityRepository,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _domainRepository = domainRepository;
        _suiteRepository = suiteRepository;
        _capabilityRepository = capabilityRepository;
        _currentUser = currentUser;
    }

    public async Task<ModuleDefinitionDetailDto> Handle(UpdateModuleDefinitionCommand request, CancellationToken cancellationToken)
    {
        var normalizedModuleId = NormalizeModuleId(request.ModuleId);
        var existing = await _repository.GetByModuleIdAsync(normalizedModuleId, cancellationToken)
            ?? throw new InvalidOperationException($"Module with ID '{normalizedModuleId}' could not be found.");

        var domain = await _domainRepository.GetByIdAsync(request.DomainLandscapeId, cancellationToken)
            ?? throw new InvalidOperationException("Referenced domain landscape could not be found.");
        var suite = await _suiteRepository.GetByIdAsync(request.SuitePlatformId, cancellationToken)
            ?? throw new InvalidOperationException("Referenced suite platform could not be found.");
        var capability = await _capabilityRepository.GetByIdAsync(request.CapabilityGroupId, cancellationToken)
            ?? throw new InvalidOperationException("Referenced capability group could not be found.");

        if (suite.DomainLandscapeId != domain.Id || capability.DomainLandscapeId != domain.Id || capability.SuitePlatformId != suite.Id)
        {
            throw new InvalidOperationException("Module hierarchy references are inconsistent.");
        }

        var status = ParseStatus(request.Status);

        var changed = ApplyModuleUpdate(
            existing,
            domain.Id,
            suite.Id,
            capability.Id,
            request.DependencyGate,
            request.DeliveryOutcome,
            request.Placement,
            request.SupportModel,
            request.ModuleName.Trim(),
            status,
            request.IsPlatformCore,
            request.IsTenantAssignable,
            ResolveActor(_currentUser));

        if (changed)
        {
            await _repository.UpdateAsync(existing, cancellationToken);
        }

        return MapDetail(existing, domain, suite, capability);
    }
}
