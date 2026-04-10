using Diten.MdmService.Application.Interfaces;
using MediatR;

namespace Diten.MdmService.Application.Features.Skus.Handlers.QueryHandlers;

internal sealed class GetSkuByIdRequestHandler : IRequestHandler<GetSkuByIdQuery, SkuDetailDto?>
{
    private readonly ISkuRepository _skuRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICompositionRepository _compositionRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetSkuByIdRequestHandler(
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

    public async Task<SkuDetailDto?> Handle(GetSkuByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _skuRepository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null) return null;

        var product = await _productRepository.GetByIdAsync(entity.ProductId, cancellationToken);
        var composition = await _compositionRepository.GetByIdAsync(entity.CompositionId, cancellationToken);
        var lifecycleStates = await _lookupRepository.GetLifecycleStatesAsync(cancellationToken);
        var state = lifecycleStates.FirstOrDefault(x => x.Id == entity.LifecycleStateId);

        return SkuMapping.ToDetailDto(
            entity,
            product?.Code ?? "N/A",
            product?.Name ?? "N/A",
            composition?.FormulationCode ?? "N/A",
            composition?.Name ?? "N/A",
            state?.Code ?? "N/A",
            state?.Name ?? "N/A"
        );
    }
}
