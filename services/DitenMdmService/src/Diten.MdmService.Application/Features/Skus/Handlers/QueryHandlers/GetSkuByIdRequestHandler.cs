using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Skus.Handlers.QueryHandlers;

internal sealed class GetSkuByIdRequestHandler : IRequestHandler<GetSkuByIdQuery, SkuDetailDto?>
{
    private readonly ISkuRepository _skuRepository;
    private readonly IItemRepository _itemRepository;
    private readonly ICompositionRepository _compositionRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetSkuByIdRequestHandler(
        ISkuRepository skuRepository,
        IItemRepository itemRepository,
        ICompositionRepository compositionRepository,
        IItemLookupRepository lookupRepository)
    {
        _skuRepository = skuRepository;
        _itemRepository = itemRepository;
        _compositionRepository = compositionRepository;
        _lookupRepository = lookupRepository;
    }

    public async Task<SkuDetailDto?> Handle(GetSkuByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _skuRepository.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null) return null;

        var item = await _itemRepository.GetByIdAsync(entity.ItemId, cancellationToken);
        var composition = await _compositionRepository.GetByIdAsync(entity.CompositionId, cancellationToken);
        var lifecycleStates = await _lookupRepository.GetLifecycleStatesAsync(cancellationToken);
        var state = lifecycleStates.FirstOrDefault(x => x.Id == entity.LifecycleStateId);

        return SkuMapping.ToDetailDto(
            entity,
            item?.Code ?? "N/A",
            item?.Name ?? "N/A",
            composition?.FormulationCode ?? "N/A",
            composition?.Name ?? "N/A",
            state?.Code ?? "N/A",
            state?.Name ?? "N/A"
        );
    }
}
