using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class UpdateDomainLandscapeCommandHandler : IRequestHandler<UpdateDomainLandscapeCommand, DomainLandscapeDto>
{
    private readonly IDomainLandscapeRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateDomainLandscapeCommandHandler(IDomainLandscapeRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<DomainLandscapeDto> Handle(UpdateDomainLandscapeCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"DomainLandscape not found.");

        var changed = false;
        var newCode = ModuleCatalogCodeNormalizer.NormalizeToCode(request.Code ?? request.Name);
        if (entity.Code != newCode)
        {
            var existing = await _repository.GetByCodeAsync(newCode, cancellationToken);
            if (existing != null) throw new InvalidOperationException($"Domain landscape code '{newCode}' already exists.");
            entity.Code = newCode;
            changed = true;
        }

        var trimmedName = request.Name.Trim();
        if (entity.Name != trimmedName) { entity.Name = trimmedName; changed = true; }
        
        var newDesc = NormalizeNullable(request.Description);
        if (entity.Description != newDesc) { entity.Description = newDesc; changed = true; }
        
        if (entity.IsActive != request.IsActive) { entity.IsActive = request.IsActive; changed = true; }

        if (changed)
        {
            entity.UpdatedBy = ResolveActor(_currentUser);
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            entity.Version++;
            await _repository.UpdateAsync(entity, cancellationToken);
        }

        return Map(entity);
    }
}
