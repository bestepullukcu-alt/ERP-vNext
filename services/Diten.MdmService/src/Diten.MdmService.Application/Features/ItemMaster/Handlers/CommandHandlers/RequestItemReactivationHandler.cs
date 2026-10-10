using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// FU04 S2: Deactivated → PendingActivation through the same approval (ActivationKind = Reactivation).
public sealed class RequestItemReactivationHandler(ItemActivationRequests requests)
    : IRequestHandler<RequestItemReactivationCommand, Response<ItemModels.ItemDto>>
{
    public Task<Response<ItemModels.ItemDto>> Handle(RequestItemReactivationCommand command, CancellationToken cancellationToken)
    {
        command.AuditDetails.Set(ItemAudit.ReasonProvided, !string.IsNullOrWhiteSpace(command.Request.Reason));
        command.AuditDetails.Set(ItemAudit.ReasonLength, command.Request.Reason?.Trim().Length ?? 0);
        return requests.StartAsync(command.ItemId, command.Request, ItemActivationKind.Reactivation, command.AuditDetails,
            cancellationToken);
    }
}
