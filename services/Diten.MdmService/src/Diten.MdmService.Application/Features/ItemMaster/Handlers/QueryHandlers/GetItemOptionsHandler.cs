using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ItemMaster.Queries;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.QueryHandlers;

/// <summary>
/// MOD-0290-FU04 S5b (BL-600): the item lists' choices for the forms, from Platform's enumeration. Fail-closed like the
/// GSKU create-options: a list that cannot be read (outage, timeout, untrusted answer, no configuration) is refused by
/// its name — never answered as an empty list, so the screen says the list is unavailable instead of offering nothing.
/// </summary>
public sealed class GetItemOptionsHandler(IItemReferenceResolver resolver)
    : IRequestHandler<GetItemOptionsQuery, Response<ItemModels.ItemOptionsDto>>
{
    private static readonly string[] Sets =
        [ItemReferenceSets.MaterialType, ItemReferenceSets.ItemUom, ItemReferenceSets.StorageCondition];

    public async Task<Response<ItemModels.ItemOptionsDto>> Handle(GetItemOptionsQuery query, CancellationToken cancellationToken)
    {
        var listed = await resolver.EnumerateLatestAsync(Sets, cancellationToken);
        if (!listed.IsSuccessful)
            return Response<ItemModels.ItemOptionsDto>.Fail(listed.FailureCode ?? "REFERENCE_PROVIDER_UNAVAILABLE",
                listed.StatusCode is 401 or 504 ? listed.StatusCode : 503);
        var bySet = listed.Sets.ToDictionary(set => set.SetCode, StringComparer.Ordinal);
        if (Sets.Any(set => !bySet.TryGetValue(set, out var found) || found.Values.Count == 0))
            return Response<ItemModels.ItemOptionsDto>.Fail(ItemReferenceCheck.UntrustedAnswer, 503);

        IReadOnlyList<ItemModels.ItemOptionDto> Options(string set) => bySet[set].Values
            .OrderBy(value => value.SortOrder).ThenBy(value => value.ValueCode, StringComparer.Ordinal)
            .Select(value => new ItemModels.ItemOptionDto(value.ValueCode, value.DisplayName, value.SortOrder, value.Attributes))
            .ToList();
        return Response<ItemModels.ItemOptionsDto>.Success(new ItemModels.ItemOptionsDto(
            bySet[ItemReferenceSets.ItemUom].CatalogVersion,
            Options(ItemReferenceSets.MaterialType),
            Options(ItemReferenceSets.ItemUom),
            Options(ItemReferenceSets.StorageCondition)));
    }
}
