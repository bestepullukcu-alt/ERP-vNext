using Diten.MdmService.Application.Contracts;
using Diten.MdmService.Application.Contracts.Inventory;
using Diten.MdmService.Application.Features.ItemMaster.Commands;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.MdmService.Application.Features.ItemMaster.Handlers.CommandHandlers;

// MOD-0290-FU04 §8.3: only a Deactivated item retires, and only when inventory says it holds no stock anywhere in the
// tenant (every legal entity, every stock status; a negative balance is not zero). Non-zero → 409; no answer (outage,
// budget, 5xx, unreadable, partial coverage) → 503, fail-closed. Both refusals leave the item Deactivated and are
// audited (path b, Outcome = Failed). Retired is final; its code is never reused, its name may be used again.
public sealed class RetireItemHandler : IRequestHandler<RetireItemCommand, Response<ItemModels.ItemDto>>
{
    // §8.3 item 5: the MOD-0048 provider's budget.
    public static readonly TimeSpan StockCheckBudget = TimeSpan.FromSeconds(2);
    public const string SkuLevelItem = "Item";

    private readonly IItemRepository _items;
    private readonly IInventoryStockAvailabilityClient _stock;
    private readonly IProductIdentityActorContext _actor;
    private readonly ILogger<RetireItemHandler> _logger;

    public RetireItemHandler(IItemRepository items, IInventoryStockAvailabilityClient stock,
        IProductIdentityActorContext actor, ILogger<RetireItemHandler> logger)
    {
        _items = items;
        _stock = stock;
        _actor = actor;
        _logger = logger;
    }

    public async Task<Response<ItemModels.ItemDto>> Handle(RetireItemCommand command, CancellationToken cancellationToken)
    {
        var item = await _items.GetByIdAsync(command.ItemId, cancellationToken);
        if (item is null) return Response<ItemModels.ItemDto>.Fail(ItemErrors.NotFound, 404);
        if (item.LifecycleStatus == ItemLifecycleStatus.Active)
            return Response<ItemModels.ItemDto>.Fail(ItemErrors.RetireRequiresDeactivation, 409);
        if (!ItemLifecycle.Allows(item.LifecycleStatus, ItemTransition.Retire))
            return Response<ItemModels.ItemDto>.Fail(ItemErrors.TransitionInvalid, 409);
        var expectedVersion = command.Request.ExpectedVersion!.Value;
        if (item.Version != expectedVersion) return Response<ItemModels.ItemDto>.Fail(ItemErrors.ConcurrencyConflict, 409);

        var check = await CheckStockAsync(item.Id, cancellationToken);
        if (check.Outcome != InventoryStockCheckOutcome.Zero)
        {
            var code = check.Outcome == InventoryStockCheckOutcome.NonZero
                ? ItemErrors.RetireStockNotZero
                : ItemErrors.RetireStockCheckUnavailable;
            _logger.LogWarning("Item {ItemId} retirement refused: {Code} ({Cause}).", item.Id, code, check.Cause ?? "-");
            return check.Outcome == InventoryStockCheckOutcome.NonZero
                ? Response<ItemModels.ItemDto>.Fail(code, 409)
                : Response<ItemModels.ItemDto>.Fail([code, check.Cause ?? "STOCK_CHECK_UNAVAILABLE"], 503);
        }

        item.LifecycleStatus = ItemLifecycle.Target(item.LifecycleStatus, ItemTransition.Retire, item.ActivationKind);
        item.NameKey = null;
        item.RetirementReason = command.Request.Reason!.Trim();
        item.RetiredAt = DateTimeOffset.UtcNow;
        item.UpdatedBy = _actor.ActorId;
        return ItemWrites.Map(await _items.ReplaceAsync(item, expectedVersion, cancellationToken));
    }

    private async Task<InventoryStockCheck> CheckStockAsync(Guid itemId, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(StockCheckBudget);
        try
        {
            return await _stock.CheckNonZeroStockAsync(itemId, SkuLevelItem, budget.Token)
                   ?? InventoryStockCheck.Unavailable("STOCK_CHECK_NO_ANSWER");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return InventoryStockCheck.Unavailable("STOCK_CHECK_BUDGET_EXHAUSTED");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Item {ItemId} stock check failed.", itemId);
            return InventoryStockCheck.Unavailable("STOCK_CHECK_FAILED");
        }
    }
}
