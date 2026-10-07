using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// MOD-0290-FU04 S1 skeleton (the approval is S2, MOD-0023 with the quality position): the state and the version are
// checked, then the request is refused without a write — a PendingActivation no engine holds would be an approval
// nobody owns (§8.4 B). S2 replaces the refusal with the guarded engine start.
internal static class ItemActivationSkeleton
{
    public static async Task<Response<ItemModels.ItemDto>> RefuseAsync(IItemRepository items, Guid itemId,
        int expectedVersion, ItemTransition transition, CancellationToken cancellationToken)
    {
        var item = await items.GetByIdAsync(itemId, cancellationToken);
        if (item is null) return Response<ItemModels.ItemDto>.Fail(ItemErrors.NotFound, 404);
        if (!ItemLifecycle.Allows(item.LifecycleStatus, transition))
            return Response<ItemModels.ItemDto>.Fail(ItemErrors.TransitionInvalid, 409);
        if (item.Version != expectedVersion) return Response<ItemModels.ItemDto>.Fail(ItemErrors.ConcurrencyConflict, 409);
        return Response<ItemModels.ItemDto>.Fail(ItemErrors.ActivationApprovalUnavailable, 503);
    }
}
