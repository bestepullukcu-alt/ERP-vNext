using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ItemMaster.Queries;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.QueryHandlers;

// Another tenant's item answers as "none" (404): the repository is tenant-bounded.
// S5: with the reader's actions (ItemActions) when an actor is known.
public sealed class GetItemByIdHandler(IItemRepository items, IProductIdentityLifecycleActorContext? actor = null)
    : IRequestHandler<GetItemByIdQuery, Response<ItemModels.ItemDto>>
{
    public async Task<Response<ItemModels.ItemDto>> Handle(GetItemByIdQuery query, CancellationToken cancellationToken) =>
        await items.GetByIdAsync(query.Id, cancellationToken) is { } item
            ? Response<ItemModels.ItemDto>.Success(ItemRules.ToDto(item, detail: true) with
            {
                AvailableActions = actor is null ? null : ItemActions.For(item.LifecycleStatus, actor,
                    item.ActivationWorkflowBinding?.SubmitterSubjectId)
            })
            : Response<ItemModels.ItemDto>.Fail(ItemErrors.NotFound, 404);
}
