using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class UpdateSuitePlatformCommandHandler : IRequestHandler<UpdateSuitePlatformCommand, SuitePlatformDto>
{
    private readonly ISuitePlatformRepository _repository;
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateSuitePlatformCommandHandler(ISuitePlatformRepository repository, IDomainLandscapeRepository domainRepository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _domainRepository = domainRepository;
        _currentUser = currentUser;
    }

    public async Task<SuitePlatformDto> Handle(UpdateSuitePlatformCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("SuitePlatform not found.");

        var domain = await _domainRepository.GetByIdAsync(request.DomainLandscapeId, cancellationToken)
            ?? throw new InvalidOperationException("Referenced domain landscape could not be found.");

        var changed = false;
        var newCode = ModuleCatalogCodeNormalizer.NormalizeToCode(request.Code ?? request.Name);
        
        if (entity.DomainLandscapeId != domain.Id || entity.Code != newCode)
        {
            var existing = await _repository.GetByCodeAsync(domain.Id, newCode, cancellationToken);
            if (existing != null && existing.Id != entity.Id) 
                throw new InvalidOperationException($"Suite platform code '{newCode}' already exists in the selected domain.");
            
            if (entity.Code != newCode) { entity.Code = newCode; changed = true; }
            if (entity.DomainLandscapeId != domain.Id) { entity.DomainLandscapeId = domain.Id; changed = true; }
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
