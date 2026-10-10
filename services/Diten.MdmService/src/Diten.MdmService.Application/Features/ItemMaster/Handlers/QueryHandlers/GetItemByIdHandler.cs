using Diten.MdmService.Application.Features.ItemMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.QueryHandlers;

// Another tenant's item answers as "none" (404): the repository is tenant-bounded.
public sealed class GetItemByIdHandler(IItemRepository items) : IRequestHandler<GetItemByIdQuery, Response<ItemModels.ItemDto>>
{
    public async Task<Response<ItemModels.ItemDto>> Handle(GetItemByIdQuery query, CancellationToken cancellationToken) =>
        await items.GetByIdAsync(query.Id, cancellationToken) is { } item
            ? Response<ItemModels.ItemDto>.Success(ItemRules.ToDto(item, detail: true))
            : Response<ItemModels.ItemDto>.Fail(ItemErrors.NotFound, 404);
}
