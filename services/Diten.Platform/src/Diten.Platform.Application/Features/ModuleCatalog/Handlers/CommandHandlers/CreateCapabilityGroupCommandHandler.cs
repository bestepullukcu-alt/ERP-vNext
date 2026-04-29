using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class CreateCapabilityGroupCommandHandler : IRequestHandler<CreateCapabilityGroupCommand, CapabilityGroupDto>
{
    private readonly ICapabilityGroupRepository _repository;
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ISuitePlatformRepository _suiteRepository;
    private readonly ICurrentUserContext _currentUser;

    public CreateCapabilityGroupCommandHandler(
        ICapabilityGroupRepository repository,
        IDomainLandscapeRepository domainRepository,
        ISuitePlatformRepository suiteRepository,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _domainRepository = domainRepository;
        _suiteRepository = suiteRepository;
        _currentUser = currentUser;
    }

    public async Task<CapabilityGroupDto> Handle(CreateCapabilityGroupCommand request, CancellationToken cancellationToken)
    {
        var domain = await _domainRepository.GetByIdAsync(request.DomainLandscapeId, cancellationToken)
            ?? throw new InvalidOperationException("Referenced domain landscape could not be found.");
        var suite = await _suiteRepository.GetByIdAsync(request.SuitePlatformId, cancellationToken)
            ?? throw new InvalidOperationException("Referenced suite platform could not be found.");

        if (suite.DomainLandscapeId != domain.Id)
        {
            throw new InvalidOperationException("Suite platform does not belong to the selected domain landscape.");
        }

        var code = ModuleCatalogCodeNormalizer.NormalizeToCode(request.Code ?? request.Name);
        var existing = await _repository.GetByCodeAsync(suite.Id, code, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Capability group code '{code}' already exists in the selected suite.");
        }

        var entity = new CapabilityGroup
        {
            Code = code,
            Name = request.Name.Trim(),
            DomainLandscapeId = domain.Id,
            SuitePlatformId = suite.Id,
            Description = NormalizeNullable(request.Description),
            IsActive = request.IsActive,
            CreatedBy = ResolveActor(_currentUser)
        };

        entity = await _repository.CreateAsync(entity, cancellationToken);
        return Map(entity);
    }
}
