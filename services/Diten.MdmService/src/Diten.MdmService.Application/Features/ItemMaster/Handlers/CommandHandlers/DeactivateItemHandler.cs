using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// MOD-0290-FU04 §8.1: Active → Deactivated, no approval. Not chosen for new receipts; existing stock may leave (the
// inventory side applies the movement rule, §8.2).
public sealed class DeactivateItemHandler : IRequestHandler<DeactivateItemCommand, Response<ItemModels.ItemDto>>
{
    private readonly IItemRepository _items;
    private readonly IProductIdentityActorContext _actor;

    public DeactivateItemHandler(IItemRepository items, IProductIdentityActorContext actor)
    {
        _items = items;
        _actor = actor;
    }

    public async Task<Response<ItemModels.ItemDto>> Handle(DeactivateItemCommand command, CancellationToken cancellationToken)
    {
        // FIX1 item 2 / FIX1-EK: whether a reason was given (never its text), refused or not.
        command.AuditDetails.Set(ItemAudit.ReasonProvided, !string.IsNullOrWhiteSpace(command.Request.Reason));
        var item = await _items.GetByIdAsync(command.ItemId, cancellationToken);
        if (item is null) return Response<ItemModels.ItemDto>.Fail(ItemErrors.NotFound, 404);
        if (!ItemLifecycle.Allows(item.LifecycleStatus, ItemTransition.Deactivate))
            return Response<ItemModels.ItemDto>.Fail(ItemErrors.TransitionInvalid, 409);
        var expectedVersion = command.Request.ExpectedVersion!.Value;
        if (item.Version != expectedVersion) return Response<ItemModels.ItemDto>.Fail(ItemErrors.ConcurrencyConflict, 409);

        var before = ItemAudit.Snapshot(item);
        item.LifecycleStatus = ItemLifecycle.Target(item.LifecycleStatus, ItemTransition.Deactivate, item.ActivationKind);
        item.DeactivationReason = command.Request.Reason!.Trim();
        item.DeactivatedAt = DateTimeOffset.UtcNow;
        item.UpdatedBy = _actor.ActorId;
        var written = await _items.UpdateFieldsAsync(item, expectedVersion, cancellationToken);
        if (written.Succeeded) ItemAudit.RecordChanges(command.AuditDetails, before, item, draft: true);
        return ItemWrites.Map(written);
    }
}
