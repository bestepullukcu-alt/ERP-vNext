using MediatR;

namespace Diten.MdmService.Application.Features.Items;

public sealed record GetAllItemsQuery : IRequest<IReadOnlyList<ItemListItemDto>>;

public sealed record GetItemByIdQuery(Guid Id) : IRequest<ItemDetailDto?>;
