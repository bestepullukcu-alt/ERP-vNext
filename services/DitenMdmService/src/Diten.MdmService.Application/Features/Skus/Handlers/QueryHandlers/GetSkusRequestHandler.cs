using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Skus.Handlers.QueryHandlers;

internal sealed class GetSkusRequestHandler : IRequestHandler<GetSkusQuery, IReadOnlyList<SkuListItemDto>>
{
    private readonly ISkuRepository _skuRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICompositionRepository _compositionRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetSkusRequestHandler(
        ISkuRepository skuRepository,
        IProductRepository productRepository,
        ICompositionRepository compositionRepository,
        IItemLookupRepository lookupRepository)
    {
        _skuRepository = skuRepository;
        _productRepository = productRepository;
        _compositionRepository = compositionRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<IReadOnlyList<SkuListItemDto>> Handle(GetSkusQuery request, CancellationToken cancellationToken)
    {
        var entities = await _skuRepository.GetAllAsync(cancellationToken);
        if (entities.Count == 0) return [];

        var products = await _productRepository.GetAllAsync(cancellationToken);
        var compositions = await _compositionRepository.GetAllAsync(cancellationToken);
        var lifecycleStates = await _lookupRepository.GetLifecycleStatesAsync(cancellationToken);

        var productCodeMap = products.ToDictionary(x => x.Id, x => x.Code);
        var productNameMap = products.ToDictionary(x => x.Id, x => x.Name);
        var compositionCodeMap = compositions.ToDictionary(x => x.Id, x => x.FormulationCode);
        var compositionNameMap = compositions.ToDictionary(x => x.Id, x => x.Name);
        var stateCodeMap = lifecycleStates.ToDictionary(x => x.Id, x => x.Code);
        var stateNameMap = lifecycleStates.ToDictionary(x => x.Id, x => x.Name);

        return entities.Select(e => SkuMapping.ToListDto(
            e,
            productCodeMap.GetValueOrDefault(e.ProductId, "N/A"),
            productNameMap.GetValueOrDefault(e.ProductId, "N/A"),
            compositionCodeMap.GetValueOrDefault(e.CompositionId, "N/A"),
            compositionNameMap.GetValueOrDefault(e.CompositionId, "N/A"),
            stateCodeMap.GetValueOrDefault(e.LifecycleStateId, "N/A"),
            stateNameMap.GetValueOrDefault(e.LifecycleStateId, "N/A")
        )).ToList();
    }
}
