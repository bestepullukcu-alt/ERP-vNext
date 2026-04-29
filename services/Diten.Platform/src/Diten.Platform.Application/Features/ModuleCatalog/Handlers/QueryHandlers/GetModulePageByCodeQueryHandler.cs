using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModulePageByCodeQueryHandler : IRequestHandler<GetModulePageByCodeQuery, ModulePageDefinitionDto>
{
    private readonly IModuleDefinitionRepository _moduleRepository;
    private readonly IModulePageDefinitionRepository _pageRepository;

    public GetModulePageByCodeQueryHandler(IModuleDefinitionRepository moduleRepository, IModulePageDefinitionRepository pageRepository)
    {
        _moduleRepository = moduleRepository;
        _pageRepository = pageRepository;
    }

    public async Task<ModulePageDefinitionDto> Handle(GetModulePageByCodeQuery request, CancellationToken cancellationToken)
    {
        var moduleId = NormalizeModuleId(request.ModuleId);
        _ = await _moduleRepository.GetByModuleIdAsync(moduleId, cancellationToken)
            ?? throw new InvalidOperationException($"ModuleId '{moduleId}' could not be found.");

        var pageCode = NormalizePageCode(request.PageCode);
        var page = await _pageRepository.GetByCodeAsync(moduleId, pageCode, cancellationToken)
            ?? throw new InvalidOperationException($"PageCode '{pageCode}' could not be found under module '{moduleId}'.");

        return Map(page);
    }
}
