using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class CreateSuitePlatformCommandHandler : IRequestHandler<CreateSuitePlatformCommand, SuitePlatformDto>
{
    private readonly ISuitePlatformRepository _repository;
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ICurrentUserContext _currentUser;

    public CreateSuitePlatformCommandHandler(
        ISuitePlatformRepository repository,
        IDomainLandscapeRepository domainRepository,
        ICurrentUserContext currentUser)
    {
        _repository = repository;
        _domainRepository = domainRepository;
        _currentUser = currentUser;
    }

    public async Task<SuitePlatformDto> Handle(CreateSuitePlatformCommand request, CancellationToken cancellationToken)
    {
        var domain = await _domainRepository.GetByIdAsync(request.DomainLandscapeId, cancellationToken)
            ?? throw new InvalidOperationException("Referenced domain landscape could not be found.");

        var code = ModuleCatalogCodeNormalizer.NormalizeToCode(request.Code ?? request.Name);
        var existing = await _repository.GetByCodeAsync(domain.Id, code, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Suite platform code '{code}' already exists in the selected domain.");
        }

        var entity = new SuitePlatform
        {
            Code = code,
            Name = request.Name.Trim(),
            DomainLandscapeId = domain.Id,
            Description = NormalizeNullable(request.Description),
            IsActive = request.IsActive,
            CreatedBy = ResolveActor(_currentUser)
        };

        entity = await _repository.CreateAsync(entity, cancellationToken);
        return Map(entity);
    }
}
