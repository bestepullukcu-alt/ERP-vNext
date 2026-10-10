using Diten.MdmService.Application.Common;
using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// FU04 S2 (S1 carry-over c): see RepairItemOrphanCodesCommand. A reserved-but-never-consumed code names no identity —
// it is counted, not burned (its creation key's repeat still consumes it).
public sealed class RepairItemOrphanCodesHandler(ITenantContext tenantContext, IItemOrphanCodeCensus census,
    ICodeReservationRepository reservations, IProductIdentityActorContext actor, TimeProvider clock)
    : IRequestHandler<RepairItemOrphanCodesCommand, Response<ItemOrphanCodeRepairResult>>
{
    public async Task<Response<ItemOrphanCodeRepairResult>> Handle(RepairItemOrphanCodesCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Limit is < 1 or > 100 || command.MinimumAgeMinutes < ItemOrphanCodes.MinimumAgeFloorMinutes
            || command.MinimumAgeMinutes > ItemOrphanCodes.MinimumAgeCeilingMinutes)
            return Response<ItemOrphanCodeRepairResult>.Fail("ITEM_ORPHAN_CODE_REPAIR_INVALID", 400);
        var cutoff = clock.GetUtcNow() - TimeSpan.FromMinutes(command.MinimumAgeMinutes);
        var found = await census.FindOrphanItemCodesAsync(tenantContext.TenantId, cutoff, command.Limit, cancellationToken);
        int burned = 0, notConsumed = 0, refused = 0;
        var codes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var reservation in found)
        {
            if (reservation.BindingState != CodeReservationBindingState.PendingIdentityWrite
                || reservation.ConsumedEntityId is not { } identityId)
            {
                notConsumed++;
                continue;
            }
            var result = await reservations.BurnIdentityBindingAsync(reservation.Id, identityId, reservation.Version,
                $"item-orphan-code-repair:{reservation.Id:D}", actor.ActorId, $"item-orphan-code-repair:{reservation.Id:D}",
                ItemOrphanCodes.BurnReason, cancellationToken);
            if (result.Succeeded) burned++;
            else
            {
                refused++;
                codes.Add(AuditCodes.CodeOrUnknown(result.ErrorCode));
            }
        }
        command.AuditDetails.Set(ItemAudit.OutcomeCode, burned > 0 ? "ITEM_ORPHAN_CODES_BURNED" : "ITEM_ORPHAN_CODES_NONE_BURNED");
        return Response<ItemOrphanCodeRepairResult>.Success(new(found.Count, burned, notConsumed, refused, codes.ToArray()));
    }
}
