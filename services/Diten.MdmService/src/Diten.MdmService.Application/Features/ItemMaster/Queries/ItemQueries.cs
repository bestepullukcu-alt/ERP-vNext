using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Queries;

// Reads are not audited (AUD-001 §7: master data, no personal data).
public sealed record GetItemByIdQuery(Guid Id) : IRequest<Response<ItemModels.ItemDto>>;

public sealed record GetItemListQuery(int? Page, int? PageSize, string? Q, string? MaterialType, string? Status)
    : IRequest<Response<ItemModels.ItemListDto>>;

// MOD-0290-FU04 S5b (BL-600): the choices the item form and the GSKU / LSKU / Finished Good stock forms offer.
public sealed record GetItemOptionsQuery : IRequest<Response<ItemModels.ItemOptionsDto>>;
