using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.ModuleCatalog.Commands;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.CommandHandlers;

public sealed class CreateModulePageDefinitionCommandHandler : IRequestHandler<CreateModulePageDefinitionCommand, ModulePageDefinitionDto>
{
    private readonly IModuleDefinitionRepository _moduleRepository;
    private readonly IModulePageDefinitionRepository _pageRepository;
    private readonly ICurrentUserContext _currentUser;

    public CreateModulePageDefinitionCommandHandler(
        IModuleDefinitionRepository moduleRepository,
        IModulePageDefinitionRepository pageRepository,
        ICurrentUserContext currentUser)
    {
        _moduleRepository = moduleRepository;
        _pageRepository = pageRepository;
        _currentUser = currentUser;
    }

    public async Task<ModulePageDefinitionDto> Handle(CreateModulePageDefinitionCommand request, CancellationToken cancellationToken)
    {
        var moduleId = NormalizeModuleId(request.ModuleId!);
        var module = await _moduleRepository.GetByModuleIdAsync(moduleId, cancellationToken)
            ?? throw new InvalidOperationException($"ModuleId '{moduleId}' could not be found.");

        var pageCode = NormalizePageCode(request.PageCode);
        if (await _pageRepository.ExistsByCodeAsync(module.ModuleId, pageCode, null, cancellationToken))
        {
            throw new InvalidOperationException($"PageCode '{pageCode}' already exists under module '{module.ModuleId}'.");
        }

        var entity = new ModulePageDefinition
        {
            ModuleId = module.ModuleId,
            PageCode = pageCode,
            PageName = request.PageName.Trim(),
            Description = NormalizeNullable(request.Description),
            RoutePath = NormalizeRoutePath(request.RoutePath),
            PageType = ParsePageType(request.PageType),
            RequiredPermissionKey = NormalizeNullable(request.RequiredPermissionKey),
            IsNavigationCandidate = request.IsNavigationCandidate,
            IsActive = request.IsActive,
            CreatedBy = ResolveActor(_currentUser)
        };

        entity = await _pageRepository.CreateAsync(entity, cancellationToken);
        return Map(entity);
    }
}
