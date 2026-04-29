using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModulePagesQueryHandler : IRequestHandler<GetModulePagesQuery, IReadOnlyList<ModulePageDefinitionDto>>
{
    private readonly IModuleDefinitionRepository _moduleRepository;
    private readonly IModulePageDefinitionRepository _pageRepository;

    public GetModulePagesQueryHandler(IModuleDefinitionRepository moduleRepository, IModulePageDefinitionRepository pageRepository)
    {
        _moduleRepository = moduleRepository;
        _pageRepository = pageRepository;
    }

    public async Task<IReadOnlyList<ModulePageDefinitionDto>> Handle(GetModulePagesQuery request, CancellationToken cancellationToken)
    {
        var moduleId = NormalizeModuleId(request.ModuleId);
        _ = await _moduleRepository.GetByModuleIdAsync(moduleId, cancellationToken)
            ?? throw new InvalidOperationException($"ModuleId '{moduleId}' could not be found.");

        var pages = await _pageRepository.GetByModuleIdAsync(moduleId, cancellationToken);
        return pages.Select(Map).ToArray();
    }
}
