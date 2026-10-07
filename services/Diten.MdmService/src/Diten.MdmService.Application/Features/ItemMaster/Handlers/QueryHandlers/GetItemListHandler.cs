using Diten.MdmService.Application.Features.ItemMaster.Queries;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.QueryHandlers;

// The screen list (S5 consumes it): paged, ordered by code; q matches a code prefix or a name part (case-insensitive).
public sealed class GetItemListHandler(IItemRepository items) : IRequestHandler<GetItemListQuery, Response<ItemModels.ItemListDto>>
{
    public const int MaximumPageSize = 100;
    // FIX1 M3: (page - 1) * pageSize stays an int — a page past this is 400, never an overflow (500).
    public const int MaximumPage = 1_000_000;
    public const int MaximumQueryLength = 200;

    public async Task<Response<ItemModels.ItemListDto>> Handle(GetItemListQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page ?? 1;
        var pageSize = query.PageSize ?? 20;
        ItemMaterialType? type = null;
        ItemLifecycleStatus? status = null;
        if (page is < 1 or > MaximumPage || pageSize is < 1 or > MaximumPageSize || query.Q?.Length > MaximumQueryLength
            || query.MaterialType is not null && !TryType(query.MaterialType, out type)
            || query.Status is not null && !TryStatus(query.Status, out status))
            return Response<ItemModels.ItemListDto>.Fail("INVALID_PARAMETER", 400);

        var result = await items.GetPageAsync(
            new ItemPageRequest(page, pageSize, string.IsNullOrWhiteSpace(query.Q) ? null : query.Q.Trim(), type, status),
            cancellationToken);
        return Response<ItemModels.ItemListDto>.Success(new ItemModels.ItemListDto(
            result.Items.Select(ItemRules.ToDto).ToList(), result.Total, page, pageSize));
    }

    private static bool TryType(string code, out ItemMaterialType? type)
    {
        type = ItemRules.TryParseMaterialType(code, out var parsed) ? parsed : null;
        return type is not null;
    }

    private static bool TryStatus(string text, out ItemLifecycleStatus? status)
    {
        status = Enum.TryParse<ItemLifecycleStatus>(text, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : null;
        return status is not null;
    }
}
