using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// MOD-0290-FU04 §4.1 / §8.1: a Draft is edited freely. On an Active / Deactivated item only the name and the
// description change without approval; an empty locked field may be filled once (O-8, audited), a filled one is the
// approved critical correction (a later slice) and is refused (ITEM_CRITICAL_FIELD_LOCKED). PendingActivation and
// Retired are not edited. The code is never in the request (ITEM_FIELD_FORBIDDEN).
public sealed class UpdateItemHandler : IRequestHandler<UpdateItemCommand, Response<ItemModels.ItemDto>>
{
    private readonly IItemRepository _items;
    private readonly IItemReferenceResolver _references;
    private readonly IProductIdentityActorContext _actor;

    public UpdateItemHandler(IItemRepository items, IItemReferenceResolver references, IProductIdentityActorContext actor)
    {
        _items = items;
        _references = references;
        _actor = actor;
    }

    public async Task<Response<ItemModels.ItemDto>> Handle(UpdateItemCommand command, CancellationToken cancellationToken)
    {
        var item = await _items.GetByIdAsync(command.ItemId, cancellationToken);
        if (item is null) return Response<ItemModels.ItemDto>.Fail(ItemErrors.NotFound, 404);
        if (!ItemLifecycle.Allows(item.LifecycleStatus, ItemTransition.Update))
            return Response<ItemModels.ItemDto>.Fail(ItemErrors.TransitionInvalid, 409);
        var expectedVersion = command.Request.ExpectedVersion!.Value;
        if (item.Version != expectedVersion) return Response<ItemModels.ItemDto>.Fail(ItemErrors.ConcurrencyConflict, 409);
        if (ItemRules.Read(command.Request) is not { } values)
            return Response<ItemModels.ItemDto>.Fail([ItemErrors.ReferenceValueInvalid, "material-type"], 422);

        if (item.LifecycleStatus != ItemLifecycleStatus.Draft && ItemRules.LockedFieldChanged(item, values) is { } locked)
            return Response<ItemModels.ItemDto>.Fail([ItemErrors.CriticalFieldLocked, locked], 409);
        if (ItemRules.ConversionShapeFailure(values.UomConversions) is { } shape)
            return Response<ItemModels.ItemDto>.Fail([ItemErrors.UomConversionInvalid, shape], 422);
        var references = await ItemReferenceCheck.ResolveAsync(_references, values, item, cancellationToken);
        if (references.Proof is not { } proof)
            return references.Detail is null
                ? Response<ItemModels.ItemDto>.Fail(references.ErrorCode!, references.StatusCode)
                : Response<ItemModels.ItemDto>.Fail([references.ErrorCode!, references.Detail], references.StatusCode);
        if (ItemRules.StockableIncomplete(values)) return Response<ItemModels.ItemDto>.Fail(ItemErrors.StockBehaviourIncomplete, 422);

        var normalizedName = ItemRules.NormalizeName(values.Name);
        var nameKey = ItemRules.NameKey(values.MaterialType, normalizedName);
        if (nameKey != item.NameKey && await _items.NameKeyTakenAsync(nameKey, item.Id, cancellationToken))
            return Response<ItemModels.ItemDto>.Fail(ItemErrors.NameConflict, 409);

        item.Name = values.Name;
        item.NameNormalized = normalizedName;
        item.NameKey = nameKey;
        item.Description = values.Description;
        item.MaterialType = values.MaterialType;
        item.MaterialTypeSelection = proof.MaterialType;
        item.BaseUomCode = values.BaseUomCode;
        item.BaseUomSelection = proof.BaseUom;
        item.UomConversions = values.UomConversions.ToList();
        item.StorageConditionCodes = values.StorageConditionCodes.ToList();
        item.StorageConditionSelections = proof.StorageConditions.ToList();
        item.LotControlled = values.LotControlled;
        item.Stockable = values.Stockable;
        item.ShelfLifeDays = values.ShelfLifeDays;
        item.RetestDays = values.RetestDays;
        item.MinRemainingShelfLifeDays = values.MinRemainingShelfLifeDays;
        item.UpdatedBy = _actor.ActorId;
        return ItemWrites.Map(await _items.ReplaceAsync(item, expectedVersion, cancellationToken));
    }
}

internal static class ItemWrites
{
    public static Response<ItemModels.ItemDto> Map(ItemWriteResult result) =>
        result switch
        {
            { Succeeded: true, Item: { } written } => Response<ItemModels.ItemDto>.Success(ItemRules.ToDto(written)),
            { WriteOutcomeAmbiguous: true } => Response<ItemModels.ItemDto>.Fail(ItemErrors.StoreUnavailable, 503),
            { ErrorCode: ItemErrors.NameConflict } => Response<ItemModels.ItemDto>.Fail(ItemErrors.NameConflict, 409),
            _ => Response<ItemModels.ItemDto>.Fail(result.ErrorCode ?? ItemErrors.ConcurrencyConflict, 409)
        };
}
