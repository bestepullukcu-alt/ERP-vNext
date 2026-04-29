using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class CreateDomainLandscapeCommandHandler : IRequestHandler<CreateDomainLandscapeCommand, DomainLandscapeDto>
{
    private readonly IDomainLandscapeRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public CreateDomainLandscapeCommandHandler(IDomainLandscapeRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<DomainLandscapeDto> Handle(CreateDomainLandscapeCommand request, CancellationToken cancellationToken)
    {
        var code = ModuleCatalogCodeNormalizer.NormalizeToCode(request.Code ?? request.Name);
        var existing = await _repository.GetByCodeAsync(code, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"Domain landscape code '{code}' already exists.");
        }

        var entity = new DomainLandscape
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = NormalizeNullable(request.Description),
            IsActive = request.IsActive,
            CreatedBy = ResolveActor(_currentUser)
        };

        entity = await _repository.CreateAsync(entity, cancellationToken);
        return Map(entity);
    }
}
