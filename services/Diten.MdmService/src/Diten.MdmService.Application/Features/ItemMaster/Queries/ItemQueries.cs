using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Queries;

// Reads are not audited (AUD-001 §7: master data, no personal data).
public sealed record GetItemByIdQuery(Guid Id) : IRequest<Response<ItemModels.ItemDto>>;

public sealed record GetItemListQuery(int? Page, int? PageSize, string? Q, string? MaterialType, string? Status)
    : IRequest<Response<ItemModels.ItemListDto>>;
