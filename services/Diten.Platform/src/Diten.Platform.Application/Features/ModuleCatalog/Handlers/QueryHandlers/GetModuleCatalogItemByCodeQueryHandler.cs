using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.ModuleCatalog.Handlers.QueryHandlers;

public sealed class GetModuleCatalogItemByCodeQueryHandler
    : IRequestHandler<GetModuleCatalogItemByCodeQuery, Response<ModuleCatalogItemDto>>
{
    private readonly IModuleCatalogRepository _repository;

    public GetModuleCatalogItemByCodeQueryHandler(IModuleCatalogRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ModuleCatalogItemDto>> Handle(GetModuleCatalogItemByCodeQuery request, CancellationToken ct)
    {
        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(request.ModuleCode);
        var item = await _repository.GetByCodeAsync(canonicalCode, ct);
        return item is null
            ? Response<ModuleCatalogItemDto>.Fail("Module catalog item not found.", 404)
            : Response<ModuleCatalogItemDto>.Success(ModuleCatalogMapper.ToDto(item));
    }
}
