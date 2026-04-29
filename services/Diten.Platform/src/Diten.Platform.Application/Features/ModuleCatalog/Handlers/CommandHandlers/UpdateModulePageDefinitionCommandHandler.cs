using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class UpdateModulePageDefinitionCommandHandler : IRequestHandler<UpdateModulePageDefinitionCommand, ModulePageDefinitionDto>
{
    private readonly IModuleDefinitionRepository _moduleRepository;
    private readonly IModulePageDefinitionRepository _pageRepository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateModulePageDefinitionCommandHandler(
        IModuleDefinitionRepository moduleRepository,
        IModulePageDefinitionRepository pageRepository,
        ICurrentUserContext currentUser)
    {
        _moduleRepository = moduleRepository;
        _pageRepository = pageRepository;
        _currentUser = currentUser;
    }

    public async Task<ModulePageDefinitionDto> Handle(UpdateModulePageDefinitionCommand request, CancellationToken cancellationToken)
    {
        var moduleId = NormalizeModuleId(request.ModuleId!);
        var module = await _moduleRepository.GetByModuleIdAsync(moduleId, cancellationToken)
            ?? throw new InvalidOperationException($"ModuleId '{moduleId}' could not be found.");

        var pageCode = NormalizePageCode(request.PageCode!);
        var entity = await _pageRepository.GetByCodeAsync(module.ModuleId, pageCode, cancellationToken)
            ?? throw new InvalidOperationException($"PageCode '{pageCode}' could not be found under module '{module.ModuleId}'.");

        if (await _pageRepository.ExistsByCodeAsync(module.ModuleId, pageCode, entity.Id, cancellationToken))
        {
            throw new InvalidOperationException($"PageCode '{pageCode}' already exists under module '{module.ModuleId}'.");
        }

        entity.PageName = request.PageName.Trim();
        entity.Description = NormalizeNullable(request.Description);
        entity.RoutePath = NormalizeRoutePath(request.RoutePath);
        entity.PageType = ParsePageType(request.PageType);
        entity.RequiredPermissionKey = NormalizeNullable(request.RequiredPermissionKey);
        entity.IsNavigationCandidate = request.IsNavigationCandidate;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = ResolveActor(_currentUser);
        entity.Version++;

        await _pageRepository.UpdateAsync(entity, cancellationToken);
        return Map(entity);
    }
}
