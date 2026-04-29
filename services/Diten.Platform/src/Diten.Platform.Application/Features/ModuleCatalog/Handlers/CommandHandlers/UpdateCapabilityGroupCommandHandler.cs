using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class UpdateCapabilityGroupCommandHandler : IRequestHandler<UpdateCapabilityGroupCommand, CapabilityGroupDto>
{
    private readonly ICapabilityGroupRepository _repository;
    private readonly IDomainLandscapeRepository _domainRepository;
    private readonly ISuitePlatformRepository _suiteRepository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateCapabilityGroupCommandHandler(
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

    public async Task<CapabilityGroupDto> Handle(UpdateCapabilityGroupCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("CapabilityGroup not found.");

        var domain = await _domainRepository.GetByIdAsync(request.DomainLandscapeId, cancellationToken)
            ?? throw new InvalidOperationException("Referenced domain landscape could not be found.");
            
        var suite = await _suiteRepository.GetByIdAsync(request.SuitePlatformId, cancellationToken)
            ?? throw new InvalidOperationException("Referenced suite platform could not be found.");

        if (suite.DomainLandscapeId != domain.Id)
            throw new InvalidOperationException("Suite platform does not belong to the selected domain landscape.");

        var changed = false;
        var newCode = ModuleCatalogCodeNormalizer.NormalizeToCode(request.Code ?? request.Name);
        
        if (entity.SuitePlatformId != suite.Id || entity.Code != newCode)
        {
            var existing = await _repository.GetByCodeAsync(suite.Id, newCode, cancellationToken);
            if (existing != null && existing.Id != entity.Id)
                throw new InvalidOperationException($"Capability group code '{newCode}' already exists in the selected suite.");
            
            if (entity.Code != newCode) { entity.Code = newCode; changed = true; }
            if (entity.SuitePlatformId != suite.Id) { entity.SuitePlatformId = suite.Id; changed = true; }
        }

        if (entity.DomainLandscapeId != domain.Id) { entity.DomainLandscapeId = domain.Id; changed = true; }

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
