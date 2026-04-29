using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;
using static Diten.Platform.Application.Features.ModuleCatalog.Handlers.ModuleCatalogMappings;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModulePageByIdQueryHandler : IRequestHandler<GetModulePageByIdQuery, ModulePageDefinitionDto>
{
    private readonly IModulePageDefinitionRepository _pageRepository;

    public GetModulePageByIdQueryHandler(IModulePageDefinitionRepository pageRepository)
    {
        _pageRepository = pageRepository;
    }

    public async Task<ModulePageDefinitionDto> Handle(GetModulePageByIdQuery request, CancellationToken cancellationToken)
    {
        var page = await _pageRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Page definition '{request.Id}' could not be found.");

        return Map(page);
    }
}
