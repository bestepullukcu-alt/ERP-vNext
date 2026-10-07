using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// S1 skeleton: see ItemActivationSkeleton.
public sealed class RequestItemReactivationHandler(IItemRepository items)
    : IRequestHandler<RequestItemReactivationCommand, Response<ItemModels.ItemDto>>
{
    public Task<Response<ItemModels.ItemDto>> Handle(RequestItemReactivationCommand command, CancellationToken cancellationToken) =>
        ItemActivationSkeleton.RefuseAsync(items, command.ItemId, command.Request.ExpectedVersion!.Value,
            ItemTransition.RequestReactivation, cancellationToken);
}
