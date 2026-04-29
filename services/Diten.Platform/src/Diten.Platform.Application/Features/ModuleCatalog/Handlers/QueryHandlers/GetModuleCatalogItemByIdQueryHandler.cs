using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModuleCatalogItemByIdQueryHandler
    : IRequestHandler<GetModuleCatalogItemByIdQuery, Response<ModuleCatalogItemDto>>
{
    private readonly IModuleCatalogRepository _repository;

    public GetModuleCatalogItemByIdQueryHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ModuleCatalogItemDto>> Handle(GetModuleCatalogItemByIdQuery request, CancellationToken ct)
    {
        var item = await _repository.GetByIdAsync(request.Id, ct);
        return item is null
            ? Response<ModuleCatalogItemDto>.Fail("Module catalog item not found.", 404)
            : Response<ModuleCatalogItemDto>.Success(ModuleCatalogMapper.ToDto(item));
    }
}
