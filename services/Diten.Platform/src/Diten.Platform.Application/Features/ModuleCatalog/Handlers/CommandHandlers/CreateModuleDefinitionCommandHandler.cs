using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class CreateModuleDefinitionCommandHandler : IRequestHandler<CreateModuleDefinitionCommand, ModuleDefinitionDetailDto>
{
    private readonly IModuleDefinitionRepository _repository;
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ISuitePlatformRepository _suiteRepository;
    private readonly ICapabilityGroupRepository _capabilityRepository;
    private readonly ICurrentUserContext _currentUser;

    public CreateModuleDefinitionCommandHandler(
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

    public async Task<ModuleDefinitionDetailDto> Handle(CreateModuleDefinitionCommand request, CancellationToken cancellationToken)
    {
        var refs = await ResolveReferencesAsync(
            request.DomainLandscapeId,
            request.SuitePlatformId,
            request.CapabilityGroupId,
            cancellationToken);

        var normalizedModuleId = NormalizeModuleId(request.ModuleId);
        var existing = await _repository.GetByModuleIdAsync(normalizedModuleId, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"ModuleId '{normalizedModuleId}' already exists.");
        }

        var status = ParseStatus(request.Status);
        var entity = new ModuleDefinition
        {
            ModuleId = normalizedModuleId,
            ModuleName = request.ModuleName.Trim(),
            DomainLandscapeId = refs.Domain.Id,
            SuitePlatformId = refs.Suite.Id,
            CapabilityGroupId = refs.Capability.Id,
            DependencyGate = NormalizeNullable(request.DependencyGate),
            DeliveryOutcome = NormalizeNullable(request.DeliveryOutcome),
            Placement = NormalizeNullable(request.Placement),
            SupportModel = NormalizeNullable(request.SupportModel),
            Status = status,
            IsPlatformCore = request.IsPlatformCore,
            IsTenantAssignable = request.IsPlatformCore ? false : request.IsTenantAssignable,
            CreatedBy = ResolveActor(_currentUser)
        };

        entity = await _repository.CreateAsync(entity, cancellationToken);
        return MapDetail(entity, refs.Domain, refs.Suite, refs.Capability);
    }

    private async Task<(DomainLandscape Domain, SuitePlatform Suite, CapabilityGroup Capability)> ResolveReferencesAsync(
        Guid domainId,
        Guid suiteId,
        Guid capabilityId,
        CancellationToken ct)
    {
        var domain = await _domainRepository.GetByIdAsync(domainId, ct)
            ?? throw new InvalidOperationException("Referenced domain landscape could not be found.");
        var suite = await _suiteRepository.GetByIdAsync(suiteId, ct)
            ?? throw new InvalidOperationException("Referenced suite platform could not be found.");
        var capability = await _capabilityRepository.GetByIdAsync(capabilityId, ct)
            ?? throw new InvalidOperationException("Referenced capability group could not be found.");

        if (suite.DomainLandscapeId != domain.Id || capability.DomainLandscapeId != domain.Id || capability.SuitePlatformId != suite.Id)
        {
            throw new InvalidOperationException("Module hierarchy references are inconsistent.");
        }

        return (domain, suite, capability);
    }
}
