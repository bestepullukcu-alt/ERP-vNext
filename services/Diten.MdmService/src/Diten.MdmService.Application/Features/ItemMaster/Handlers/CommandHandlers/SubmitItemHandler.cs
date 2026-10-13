using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// FU04 S2: Draft → PendingActivation through the approval (ItemActivationRequests).
public sealed class SubmitItemHandler(ItemActivationRequests requests)
    : IRequestHandler<SubmitItemCommand, Response<ItemModels.ItemDto>>
{
    public Task<Response<ItemModels.ItemDto>> Handle(SubmitItemCommand command, CancellationToken cancellationToken) =>
        requests.StartAsync(command.ItemId, command.Request, ItemActivationKind.Activation, command.AuditDetails,
            cancellationToken);
}
