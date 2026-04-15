using Diten.MdmService.Application.Interfaces;
using Diten.MdmService.Domain.Entities;
using MediatR;

namespace Diten.MdmService.Application.Features.Skus.Handlers.QueryHandlers;

internal sealed class GetSkusRequestHandler : IRequestHandler<GetSkusQuery, IReadOnlyList<SkuListItemDto>>
{
    private readonly ISkuRepository _skuRepository;
    private readonly IItemRepository _itemRepository;
    private readonly ICompositionRepository _compositionRepository;
    private readonly IItemLookupRepository _lookupRepository;

    public GetSkusRequestHandler(
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

    public async Task<IReadOnlyList<SkuListItemDto>> Handle(GetSkusQuery request, CancellationToken cancellationToken)
    {
        var entities = await _skuRepository.GetAllAsync(cancellationToken);
        if (entities.Count == 0) return [];

        var items = await _itemRepository.GetAllAsync(cancellationToken);
        var compositions = await _compositionRepository.GetAllAsync(cancellationToken);
        var lifecycleStates = await _lookupRepository.GetLifecycleStatesAsync(cancellationToken);

        var itemCodeMap = items.ToDictionary(x => x.Id, x => x.Code);
        var itemNameMap = items.ToDictionary(x => x.Id, x => x.Name);
        var compositionCodeMap = compositions.ToDictionary(x => x.Id, x => x.FormulationCode);
        var compositionNameMap = compositions.ToDictionary(x => x.Id, x => x.Name);
        var stateCodeMap = lifecycleStates.ToDictionary(x => x.Id, x => x.Code);
        var stateNameMap = lifecycleStates.ToDictionary(x => x.Id, x => x.Name);

        return entities.Select(e => SkuMapping.ToListDto(
            e,
            itemCodeMap.GetValueOrDefault(e.ItemId, "N/A"),
            itemNameMap.GetValueOrDefault(e.ItemId, "N/A"),
            compositionCodeMap.GetValueOrDefault(e.CompositionId, "N/A"),
            compositionNameMap.GetValueOrDefault(e.CompositionId, "N/A"),
            stateCodeMap.GetValueOrDefault(e.LifecycleStateId, "N/A"),
            stateNameMap.GetValueOrDefault(e.LifecycleStateId, "N/A")
        )).ToList();
    }
}
