using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// FU04 S2: the maker takes the request back (the engine's cancel first; the item returns where the request found it).
public sealed class WithdrawItemApprovalHandler(ItemActivationRequests requests)
    : IRequestHandler<WithdrawItemApprovalCommand, Response<ItemModels.ItemDto>>
{
    public Task<Response<ItemModels.ItemDto>> Handle(WithdrawItemApprovalCommand command, CancellationToken cancellationToken) =>
        requests.WithdrawAsync(command.ItemId, command.Request, command.AuditDetails, cancellationToken);
}
