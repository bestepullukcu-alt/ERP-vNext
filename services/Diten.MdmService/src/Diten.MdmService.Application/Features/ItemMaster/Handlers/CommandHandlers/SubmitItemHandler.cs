using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// S1 skeleton: see ItemActivationSkeleton.
public sealed class SubmitItemHandler(IItemRepository items) : IRequestHandler<SubmitItemCommand, Response<ItemModels.ItemDto>>
{
    public Task<Response<ItemModels.ItemDto>> Handle(SubmitItemCommand command, CancellationToken cancellationToken) =>
        ItemActivationSkeleton.RefuseAsync(items, command.ItemId, command.Request.ExpectedVersion!.Value,
            ItemTransition.Submit, cancellationToken);
}
