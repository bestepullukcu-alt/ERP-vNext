using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetAssignableModuleCatalogItemsQueryHandler
    : IRequestHandler<GetAssignableModuleCatalogItemsQuery, Response<IReadOnlyList<ModuleCatalogListItemDto>>>
{
    private readonly IModuleCatalogRepository _repository;

    public GetAssignableModuleCatalogItemsQueryHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<ModuleCatalogListItemDto>>> Handle(GetAssignableModuleCatalogItemsQuery request, CancellationToken ct)
    {
        var items = await _repository.GetAssignableAsync(ct);
        return Response<IReadOnlyList<ModuleCatalogListItemDto>>.Success(items.Select(ModuleCatalogMapper.ToListDto).ToList());
    }
}
